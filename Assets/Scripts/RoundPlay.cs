using System;
using UnityEngine;

namespace FpsManager
{
    public enum RoundPhase { Preparation, Setup, Execute, PostPlant, Ended }

    public enum RoundOutcome { None, TerroristsEliminated, CounterTerroristsEliminated, BombExploded, BombDefused, TimeExpired }

    // What a player is doing right now. Derived state, recomputed every tick.
    public enum PlayerTask { Idle, MoveToLane, PushSite, PlantBomb, RecoverBomb, HoldSite, DefendSite, Rotate, FallBack, Regroup, Retake, Defuse, Trade, Lurk, Patrol }

    // Test starting values, not balanced numbers. Round and bomb times follow the real
    // game; the radii are sized for this 100 x 100 map.
    [Serializable]
    public class RoundSettings
    {
        public float roundSeconds = 115f;
        public float executeDelaySeconds = 25f;  // latest moment terrorists leave their lane
        public float plantSeconds = 3.2f;
        public float defuseSeconds = 10f;        // kits halve this duration
        public float bombSeconds = 40f;
        public float siteRadius = 12f;           // counts as being on the site
        // Attackers are counted over a wider circle than defenders: a push is spread over
        // the approach, and a defender who only counts bodies already standing on the site
        // has left it far too late to leave.
        public float threatRadius = 20f;
        public float interactRadius = 2.5f;      // reach for planting, defusing, picking the bomb up
        public float clearRadius = 20f;          // a visible enemy this close to the site stops a plant
        public float holdRadius = 6f;            // spread of the holding positions around a site
        public float lossMemory = 12f;           // how long a team mate falling here still counts
        public float dangerMemory = 30f;          // death location persists long enough for investigation
        public float tradeWindow = 6f;           // how long a team mate falling is still worth trading for
    }

    // Map specific anchor points. Built by the map code, not hard coded here.
    public sealed class MapLayout
    {
        public Vector2[][][] FlankRoutes; // per target site, alternate-side and mid routes
        public Vector2 Mid=new Vector2(45,38);
        public Vector2 AttackerSpawn;
        public Vector2[] Sites;        // plant spots
        public string[] SiteNames;
        public Vector2[] Staging;      // per site: where defenders fall back to and retake from
        public Vector2[][] StackPost, StackPeek; // two exposed guards, three covered trade posts
        public Vector2[][] HoldRing;   // per site: spread positions around it
        public Vector2[][] Approaches; // per site: the mouths attackers come through
        public Vector2[][] PeekPost;   // per site, per approach: holds that mouth
        public Vector2[][] CoverPost;  // per site, per approach: same angle, no line of sight
        public int SiteCount { get { return Sites.Length; } }
    }

    public struct PlayerObjective
    {
        public Vector2 destination;
        public Vector2 watch;      // direction to face once standing still
        public PlayerTask task;
        public bool valid;
        // Where to duck when reloading, blinded or hurt. Same angle, no line of sight.
        public Vector2 cover;
        public bool hasCover;
        // Break off and move even with an enemy in sight. Without this a defender can
        // never leave a site, because seeing anyone pins them in a firefight. A player
        // breaking off does not shoot while they move: firing on the move is not
        // modelled, so the retreat costs them their gun until they are set again.
        public bool disengage;
        public bool plannedExecute; public Vector2 utilityTarget,utilityBlockTarget;
        public bool stackHold, stackHidden, forwardAdvance;
        public bool cautious; // Approach a known friendly-loss area, not a known enemy.
    }

    // Round rules and the team level decisions that drive them.
    //
    // Every decision here uses only what a team has actually seen, through VisionSystem,
    // never the true positions of the other side. The one exception is a team's own
    // players, which it always knows.
    public sealed partial class RoundDirector
    {
        readonly RoundSettings settings;
        readonly MapLayout layout;
        readonly int count;
        readonly PlayerObjective[] objectives;
        readonly int[] slot;              // stable index of a player within their team
        readonly bool[] ready=new bool[10];
        readonly bool[] standing;         // alive as of the previous tick, for spotting losses
        readonly Vector2[] lossPosition;
        readonly int[] supportSite;
        readonly float[] supportUntil;
        readonly float[] lossClock;       // round clock when this player fell, -1 while alive
        DeterministicRandom random;

        public MatchSounds SoundIntel;
        public int StrategyTeam=-1; public TeamStrategy Strategy;
        public RoundPhase Phase { get; private set; }
        public RoundOutcome Outcome { get; private set; }
        public float Clock { get; private set; }
        public int TargetSite { get; private set; }
        public int PlantedSite { get; private set; }
        public int Carrier { get; private set; }
        public Func<int,bool> InteractionAllowed;
        public bool BombDropped { get; private set; }
        public Vector2 BombPosition { get; private set; }
        public float PlantProgress { get; private set; }
        public float DefuseProgress { get; private set; }
        public float BombTimer { get; private set; }
        public RoundSettings Settings { get { return settings; } }
        public MapLayout Layout { get { return layout; } }

        public RoundDirector(RoundSettings settings, MapLayout layout, int playerCount)
        {
            if (layout == null) throw new ArgumentNullException("layout");
            this.settings = settings ?? new RoundSettings();
            this.layout = layout;
            count = playerCount;
            supportSite=new int[count]; supportUntil=new float[count];
            objectives = new PlayerObjective[count];
            slot = new int[count];
            standing = new bool[count];
            lossPosition = new Vector2[count];
            lossClock = new float[count];
            Phase = RoundPhase.Preparation;
            Outcome = RoundOutcome.None;
            PlantedSite = -1;
            Carrier = -1;
            TargetSite = 0;
        }

        public PlayerObjective Objective(int player) { return objectives[player]; }
        public bool Running { get { return Phase == RoundPhase.Setup || Phase == RoundPhase.Execute || Phase == RoundPhase.PostPlant; } }
        public string SiteName(int site) { return site < 0 ? "-" : layout.SiteNames[site]; }

        public void Reset()
        {
            Phase = RoundPhase.Preparation;
            Outcome = RoundOutcome.None;
            PreservingEquipment=null; SoundIntel=null; SidePlansEnabled=false; ResetTactics(); HasDefuseKit=null;DefuseSafe=null;PreferredDefuser=null;CommittedDefuse=false;BombReachable=null;Defuser=-1;lastDropper=-1;reclaimAt=0;
            Clock = 0f; PlantProgress = 0f; DefuseProgress = 0f; BombTimer = 0f;
            PlantedSite = -1; Carrier = -1; BombDropped = false; TargetSite = 0;
            for (int i = 0; i < count; i++)
            {
                objectives[i] = new PlayerObjective(); ready[i] = false; supportSite[i]=-1; supportUntil[i]=0;
                standing[i] = true; lossClock[i] = -1f; lossPosition[i] = Vector2.zero;
            }
        }

        // ctTeam is the team index currently playing counter terrorist.
        public void Begin(int seed, int[] team, int ctTeam)
        {
            Reset();
            random = new DeterministicRandom(seed);
            // The attacking side picks a site. This is a placeholder for the prepared
            // tactics of step 6; for now it is one deterministic draw per round.
            TargetSite = (int)(random.NextUInt() % (uint)layout.SiteCount);
            int ctSlot = 0, tSlot = 0;
            for (int i = 0; i < count; i++) slot[i] = team[i] == ctTeam ? ctSlot++ : tSlot++;
            for (int i = 0; i < count; i++)
                if (team[i] != ctTeam && Carrier < 0) Carrier = i;
            Phase = RoundPhase.Setup;
        }

        public void Tick(float delta, Vector2[] positions, int[] team, int ctTeam, Vector2[] homeAnchor,
                         int[] composure, VisionSystem vision, CombatSystem combat)
        {
            if (!Running) return;
            Clock += delta;
            int tTeam = 1 - ctTeam;

            for(int i=0;i<count;i++) if(team[i]!=ctTeam && combat.Alive(i))
                ready[i] |= Vector2.Distance(positions[i],homeAnchor[i]) <= settings.interactRadius*2 || Clock >= (9+slot[i]*2)*(team[i]==StrategyTeam?(Strategy==TeamStrategy.Aggressive?.7f:Strategy==TeamStrategy.Defensive?1.25f:1):1);
            TrackLosses(positions, combat);
            UpdateBombCarrier(positions, team, ctTeam, combat);
            if (Phase == RoundPhase.Setup && ReadyToExecute(positions, team, ctTeam, homeAnchor, combat))
                Phase = RoundPhase.Execute;
            if (Phase == RoundPhase.PostPlant) UpdatePlantedBomb(delta, positions, team, ctTeam, combat);
            else UpdatePlanting(delta, positions, team, tTeam, vision, combat);
            if (Phase == RoundPhase.Ended) return;

            AssignBackup(positions,team,ctTeam,homeAnchor,vision,combat);
            for (int i = 0; i < count; i++)
                objectives[i] = combat.Alive(i)
                    ? (team[i] == ctTeam
                        ? DefenderObjective(i, positions, team, ctTeam, homeAnchor, composure, vision, combat)
                        : AttackerObjective(i, positions, team, ctTeam, homeAnchor, vision, combat))
                    : new PlayerObjective();

            UpdateSidePlans(positions,team,ctTeam,homeAnchor,vision,combat);
            CheckEnding(team, ctTeam, combat);
        }

        void End(RoundOutcome outcome)
        {
            Phase = RoundPhase.Ended;
            Outcome = outcome;
        }

        void CheckEnding(int[] team, int ctTeam, CombatSystem combat)
        {
            int ctAlive = 0, tAlive = 0;
            for (int i = 0; i < count; i++)
            {
                if (!combat.Alive(i)) continue;
                if (team[i] == ctTeam) ctAlive++; else tAlive++;
            }
            // A planted bomb keeps the round alive: wiping the attackers does not win it,
            // and wiping the defenders wins it at once because nobody can defuse.
            if (ctAlive == 0) { End(RoundOutcome.CounterTerroristsEliminated); return; }
            if (tAlive == 0 && PlantedSite < 0) { End(RoundOutcome.TerroristsEliminated); return; }
            if (PlantedSite < 0 && Clock >= settings.roundSeconds) End(RoundOutcome.TimeExpired);
        }

        // Where and when each player fell. A death is something both teams observe, so
        // using it in a decision does not leak hidden information.
        void TrackLosses(Vector2[] positions, CombatSystem combat)
        {
            for (int i = 0; i < count; i++)
            {
                if (!standing[i] || combat.Alive(i)) continue;
                standing[i] = false;
                lossPosition[i] = positions[i];
                lossClock[i] = Clock;
                // A new casualty reopens a previously searched neighbourhood.
                for(int scout=0;scout<count;scout++)if(Vector2.Distance(patrolCentre[scout],positions[i])<=14)
                {patrolStep[scout]=0;patrolPause[scout]=0;}
            }
        }

        public int RecentLossesAt(int site, int forTeam, int[] team)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                if (lossClock[i] < 0f || team[i] != forTeam) continue;
                if (Clock - lossClock[i] > settings.lossMemory) continue;
                if (Vector2.Distance(lossPosition[i], layout.Sites[site]) > settings.threatRadius) continue;
                total++;
            }
            return total;
        }

        void UpdateBombCarrier(Vector2[] positions, int[] team, int ctTeam, CombatSystem combat)
        {
            if (PlantedSite >= 0) return;
            if (Carrier >= 0 && !combat.Alive(Carrier))
            {
                BombPosition = positions[Carrier];
                BombDropped = true; Carrier = -1; PlantProgress = 0f;
            }
            if (Carrier >= 0) { BombPosition = positions[Carrier]; return; }
            if (!BombDropped) return;
            for (int i = 0; i < count; i++)
            {
                if (team[i] == ctTeam || !combat.Alive(i)||(i==lastDropper&&Clock<reclaimAt)||(BombReachable!=null&&!BombReachable(i,BombPosition))) continue;
                if ((BombReachable==null&&InteractionAllowed!=null&&!InteractionAllowed(i))||Vector2.Distance(positions[i], BombPosition) > settings.interactRadius) continue;
                Carrier = i; BombDropped = false; return;
            }
        }

        // Attackers leave their lane together, or when the clock forces them out.
        bool ReadyToExecute(Vector2[] positions, int[] team, int ctTeam, Vector2[] homeAnchor, CombatSystem combat)
        {
            for(int i=0;i<count;i++) if(team[i]!=ctTeam && combat.Alive(i) && ready[i]) return true;
            return false;
        }

        void UpdatePlanting(float delta, Vector2[] positions, int[] team, int tTeam, VisionSystem vision, CombatSystem combat)
        {
            if (Carrier < 0 || Phase != RoundPhase.Execute || (PreservingEquipment!=null&&PreservingEquipment(Carrier))) { PlantProgress = 0f; return; }
            bool inPlace = (InteractionAllowed==null||InteractionAllowed(Carrier))&&Vector2.Distance(positions[Carrier], layout.Sites[TargetSite]) <= settings.interactRadius;
            // Planting is interrupted by having something to shoot at, or by the team
            // currently seeing a defender near the site. Both are observed facts.
            bool clear = !combat.Engaging(Carrier) && VisibleEnemiesNear(tTeam, TargetSite, team, vision, combat) == 0;
            if (!inPlace || !clear) { PlantProgress = 0f; return; }
            PlantProgress += delta;
            if (PlantProgress < settings.plantSeconds) return;
            PlantedSite = TargetSite;
            BombPosition = layout.Sites[TargetSite];
            BombTimer = settings.bombSeconds;
            BombDropped = false; Carrier = -1; PlantProgress = 0f;
            Phase = RoundPhase.PostPlant;
        }

        public Func<int,bool> DefuseSafe;
        public Func<int,bool> PreservingEquipment;
        public Func<int> PreferredDefuser;
        public bool CommittedDefuse {get;private set;}

        public void InterruptDefuse(int i){if(Defuser==i){Defuser=-1;DefuseProgress=0;CommittedDefuse=false;}}
        bool CanDefuse(int i,Vector2[] positions,int[] team,int ct,CombatSystem combat){
            return team[i]==ct&&combat.Alive(i)&&(PreservingEquipment==null||!PreservingEquipment(i))&&((CommittedDefuse&&Defuser==i)||(DefuseSafe!=null?DefuseSafe(i):!combat.Engaging(i)))
                &&(InteractionAllowed==null||InteractionAllowed(i))&&Vector2.Distance(positions[i],BombPosition)<=settings.interactRadius;
        }
        void UpdatePlantedBomb(float delta, Vector2[] positions, int[] team, int ctTeam, CombatSystem combat)
        {
            BombTimer -= delta;
            if (BombTimer <= 0f) { BombTimer = 0f; End(RoundOutcome.BombExploded); return; }
            int preferred=PreferredDefuser!=null?PreferredDefuser():-1;
            int defuser = Defuser;
            if(defuser>=0&&!CanDefuse(defuser,positions,team,ctTeam,combat))defuser=-1;
            for (int i = 0; i < count; i++)
            {
                if(Defuser>=0&&defuser==Defuser)break;
                if(preferred>=0&&i!=preferred)continue;
                if(!CanDefuse(i,positions,team,ctTeam,combat))continue;
                if ((InteractionAllowed!=null&&!InteractionAllowed(i))||Vector2.Distance(positions[i], BombPosition) > settings.interactRadius) continue;
                if(defuser<0||(HasDefuseKit!=null&&HasDefuseKit(i)&&!HasDefuseKit(defuser)))defuser=i;
            }
            if(defuser!=Defuser){DefuseProgress=0;Defuser=defuser;CommittedDefuse=defuser>=0&&defuser==preferred;}
            if (defuser < 0) { DefuseProgress = 0f; return; }
            DefuseProgress += delta;
            if (DefuseProgress >= DefuseDuration) End(RoundOutcome.BombDefused);
        }

        PlayerObjective AttackerObjective(int player, Vector2[] positions, int[] team, int ctTeam, Vector2[] homeAnchor, VisionSystem vision, CombatSystem combat)
        {
            var objective = new PlayerObjective();
            objective.valid = true;
            int site = PlantedSite >= 0 ? PlantedSite : TargetSite;

            if (PlantedSite < 0 && Carrier < 0 && BombDropped && NearestLiving(BombPosition, positions, team, ctTeam, false, combat) == player)
            {
                objective.task = PlayerTask.RecoverBomb;
                objective.destination = BombPosition;
                objective.watch = layout.Sites[site] - BombPosition;
                return objective;
            }
            if (LurkObjective(player,positions,team,ctTeam,combat,out objective)) return objective;
            objective.valid=true;
            if (!ready[player] && Phase != RoundPhase.PostPlant)
            {
                objective.task = PlayerTask.MoveToLane;
                objective.destination = homeAnchor[player];
                objective.watch = layout.Sites[site] - homeAnchor[player];
                return objective;
            }
            if (PlantedSite < 0 && player == Carrier)
            {
                objective.task = PlayerTask.PlantBomb;
                objective.destination = layout.Sites[site];
                objective.watch = layout.Staging[site] - layout.Sites[site];
                return objective;
            }
            // Take the angle that just killed a team mate instead of walking to an empty
            // holding spot. The defender who won that duel is still there and still aimed
            // at it, and nobody trading means a site take dies one player at a time.
            Vector2 tradeSpot;
            if (PlantedSite < 0 && player != Carrier && TradeSpot(site, team[player], team, out tradeSpot))
            {
                objective.task = PlayerTask.Trade;
                objective.destination = tradeSpot;
                objective.watch = layout.Sites[site] - tradeSpot;
                return objective;
            }
            // Everyone else spreads around the site: covering the approaches before the
            // plant is the same job as holding them afterwards.
            objective.task = PlantedSite >= 0 ? PlayerTask.HoldSite : PlayerTask.PushSite;
            objective.destination = HoldSpot(site, slot[player]);
            objective.watch = WatchDirection(team[player], site, objective.destination, team, vision, combat, false);
            return objective;
        }

        // Team death locations remain useful without a surviving witness.
        bool LossRisk(int site,int teamId,int[] teams,out Vector2 point)
        {
            point=Vector2.zero; float newest=-1;
            for(int i=0;i<count;i++)
            {
                if(teams[i]!=teamId||lossClock[i]<0||Clock-lossClock[i]>settings.dangerMemory||dangerCleared[i]||NearestSite(lossPosition[i])!=site)continue;
                if(lossClock[i]>newest){newest=lossClock[i];point=lossPosition[i];}
            }
            return newest>=0;
        }
        // Count confirmed sightings or inferred pressure without inventing an enemy position.
        int BackupThreat(int site,int viewer,int[] team,VisionSystem vision)
        {
            int total=0;
            for(int i=0;i<count;i++)
            {
                if(team[i]==viewer) continue;
                var info=vision.Knowledge(viewer,i);
                if(info.known&&!info.anonymous&&info.age<=3&&Vector2.Distance(info.lastKnownPosition,layout.Sites[site])<=settings.threatRadius) total++;
            }
            Vector2 danger; if(LossRisk(site,viewer,team,out danger)) total=Math.Max(total,2);
            return SoundIntel!=null&&SoundIntel.AttackSignal(viewer,layout.Sites[site],settings.threatRadius,settings.siteRadius)?Math.Max(total,2):total;
        }
        void AssignBackup(Vector2[] positions,int[] team,int ct,Vector2[] anchors,VisionSystem vision,CombatSystem combat)
        {
            for(int i=0;i<count;i++)
            {
                if(supportSite[i]<0) continue;
                if(PlantedSite>=0||!combat.Alive(i)||Clock>=supportUntil[i]) supportSite[i]=-1;
                else if(BackupThreat(supportSite[i],ct,team,vision)>=2) { Vector2 risk; supportUntil[i]=LossRisk(supportSite[i],ct,team,out risk)?Math.Max(supportUntil[i],Clock+6):Clock+4; }
            }
            if(PlantedSite>=0) return;
            for(int site=0;site<layout.SiteCount;site++)
            {
                int attackers=BackupThreat(site,ct,team,vision);
                if(attackers<2) continue;
                int assigned=0;
                for(int i=0;i<count;i++) if(team[i]==ct&&combat.Alive(i)&&(supportSite[i]>=0?supportSite[i]:HomeSite(anchors[i]))==site) assigned++;
                int needed=attackers+1-assigned;
                while(needed-->0)
                {
                    int best=-1; float nearest=float.MaxValue;
                    for(int i=0;i<count;i++)
                    {
                        if(team[i]!=ct||!combat.Alive(i)||supportSite[i]>=0||combat.Engaging(i)) continue;
                        int home=HomeSite(anchors[i]); if(home==site) continue;
                        if(home>=0)
                        {
                            if(BackupThreat(home,ct,team,vision)>=2) continue;
                            int guards=0;
                            for(int j=0;j<count;j++) if(team[j]==ct&&combat.Alive(j)&&supportSite[j]<0&&HomeSite(anchors[j])==home) guards++;
                            if(guards<=1) continue;
                        }
                        float distance=Vector2.Distance(positions[i],layout.Sites[site]);
                        if(distance<nearest) { nearest=distance; best=i; }
                    }
                    if(best<0) break;
                    Vector2 risk; supportSite[best]=site; supportUntil[best]=Clock+(LossRisk(site,ct,team,out risk)?Math.Max(12,nearest/2.4f+6):4);
                }
            }
        }
        PlayerObjective DefenderObjective(int player, Vector2[] positions, int[] team, int ctTeam, Vector2[] homeAnchor,
                                          int[] composure, VisionSystem vision, CombatSystem combat)
        {
            var objective = new PlayerObjective();
            objective.valid = true;

            if (PlantedSite >= 0)
            {
                bool atBomb = Vector2.Distance(positions[player], BombPosition) <= settings.interactRadius;
                objective.task = atBomb ? PlayerTask.Defuse : PlayerTask.Retake;
                objective.destination = BombPosition;
                objective.watch = layout.Staging[PlantedSite] - BombPosition;
                return objective;
            }

            int home = supportSite[player]>=0?supportSite[player]:HomeSite(homeAnchor[player]);
            if(supportSite[player]>=0)
            {
                Vector2 danger;
                if(LossRisk(home,ctTeam,team,out danger))
                {
                    objective=Investigate(player,positions[player],danger,ctTeam,team,combat.Engaging(player)||KnownEnemiesNear(ctTeam,home,team,vision,combat)>0);
                    objective.watch=Vector2.Distance(positions[player],danger)>1?danger-positions[player]:WatchDirection(ctTeam,home,danger,team,vision,combat,true);
                    return objective;
                }
                int approach=slot[player]%layout.PeekPost[home].Length;
                Vector2 post=layout.PeekPost[home][approach];
                if(Vector2.Distance(positions[player],post)>settings.interactRadius)
                {
                    objective.task=PlayerTask.Rotate; objective.destination=post;
                    objective.watch=WatchDirection(ctTeam,home,post,team,vision,combat,true);
                    return objective;
                }
            }
            if (home < 0)
            {
                // Not posted on a site. Rotate to wherever the team has actually seen the
                // most attackers, and otherwise hold the assigned spot.
                int busiest = BusiestSite(ctTeam, team, vision, combat);
                if (busiest >= 0)
                {
                    objective.task = PlayerTask.Rotate;
                    objective.destination = layout.Staging[busiest];
                    objective.watch = layout.Sites[busiest] - layout.Staging[busiest];
                    return objective;
                }
                objective.task = PlayerTask.DefendSite;
                objective.destination = homeAnchor[player];
                objective.watch = WatchDirection(ctTeam, NearestSite(homeAnchor[player]), homeAnchor[player], team, vision, combat, true);
                return objective;
            }

            // The retake decision. A defender weighs what the team has actually seen on
            // this site against the team mates they can count there. Composure buys one
            // body of patience.
            //
            // Counting live contacts alone is not enough once attackers arrive one at a
            // time: a defender is almost never looking at a crowd, so the odds never read
            // as bad even while the site is being taken apart. Team mates dropping here is
            // the other observed fact, and it is the one a real player acts on.
            int seen = KnownEnemiesNear(ctTeam, home, team, vision, combat);
            int friends = LivingFriendsNear(player, home, positions, team, ctTeam, combat);
            int down = RecentLossesAt(home, team[player], team);
            int tolerance = composure[player] >= 90 ? 1 : 0;
            if(team[player]==StrategyTeam)tolerance+=Strategy==TeamStrategy.Defensive?-1:Strategy==TeamStrategy.Aggressive?1:0;
            // No guard is needed for the quiet case: a living defender always counts
            // themselves, so with nothing seen and nobody lost the sum cannot win.
            if (seen + down > 0 && seen + down > friends + tolerance)
            {
                // Break off only while there is ground to give up. Once back at the
                // regroup point the player sets again and fights from there, otherwise
                // falling back would just mean standing in the open unarmed.
                bool travelling = Vector2.Distance(positions[player], layout.Staging[home]) > settings.interactRadius * 2f;
                objective.task = travelling ? PlayerTask.FallBack : PlayerTask.Regroup;
                objective.disengage = travelling;
                objective.destination = layout.Staging[home];
                objective.watch = layout.Sites[home] - layout.Staging[home];
                objective.valid = true;
                return objective;
            }
            // Split the defenders of a site across its different ways in, so two people
            // are not staring down the same corridor while a third is unwatched.
            int mouth = supportSite[player]>=0?slot[player]%layout.PeekPost[home].Length:ApproachShare(player, home, team, ctTeam, homeAnchor, combat);
            objective.task = PlayerTask.DefendSite;
            objective.destination = layout.PeekPost[home][mouth];
            objective.cover = layout.CoverPost[home][mouth];
            objective.hasCover = true;
            objective.watch = layout.Approaches[home][mouth] - objective.destination;
            return objective;
        }

        // Which way in this player is responsible for: their rank among the defenders
        // posted on the same site, wrapped over however many mouths that site has.
        int ApproachShare(int player, int site, int[] team, int ctTeam, Vector2[] homeAnchor, CombatSystem combat)
        {
            int rank = 0;
            for (int i = 0; i < player; i++)
            {
                if (team[i] != team[player] || !combat.Alive(i)) continue;
                if (HomeSite(homeAnchor[i]) == site) rank++;
            }
            return rank % layout.Approaches[site].Length;
        }

        // The most recent place a team mate fell near this site, if it is recent enough to
        // be worth pushing onto. Where a friend died is observed, not hidden information.
        bool TradeSpot(int site, int forTeam, int[] team, out Vector2 spot)
        {
            spot = Vector2.zero;
            float newest = -1f;
            for (int i = 0; i < count; i++)
            {
                if (lossClock[i] < 0f || team[i] != forTeam) continue;
                if (Clock - lossClock[i] > settings.tradeWindow) continue;
                if (Vector2.Distance(lossPosition[i], layout.Sites[site]) > settings.threatRadius) continue;
                if (lossClock[i] <= newest) continue;
                newest = lossClock[i]; spot = lossPosition[i];
            }
            return newest >= 0f;
        }

        Vector2 HoldSpot(int site, int teamSlot)
        {
            var ring = layout.HoldRing[site];
            return ring[teamSlot % ring.Length];
        }

        int HomeSite(Vector2 anchor)
        {
            int nearest=NearestSite(anchor);
            return Vector2.Distance(anchor,layout.Sites[nearest])<=settings.siteRadius?nearest:-1;
        }

        // Where a player standing still should be looking. The last contact the team holds
        // on this site comes first; with nothing to go on they watch the ground the other
        // side has to arrive over. Anchors sit almost on top of their site, so pointing at
        // the site centre would leave them facing an arbitrary direction.
        Vector2 WatchDirection(int viewerTeam, int site, Vector2 post, int[] team, VisionSystem vision, CombatSystem combat, bool defending)
        {
            int nearest = -1; float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (team[i] == viewerTeam || !combat.Alive(i)) continue;
                var contact = vision.Knowledge(viewerTeam, i);
                if (!contact.known) continue;
                if (Vector2.Distance(contact.lastKnownPosition, layout.Sites[site]) > settings.threatRadius) continue;
                float distance = Vector2.Distance(contact.lastKnownPosition, post);
                if (distance < best) { best = distance; nearest = i; }
            }
            if (nearest >= 0) return vision.Knowledge(viewerTeam, nearest).lastKnownPosition - post;
            // Defenders arrive from their staging point, so attackers come from the far
            // side of the site. Attackers holding after a plant watch the staging point,
            // because that is where a retake forms up.
            Vector2 approach = defending
                ? layout.Sites[site] + (layout.Sites[site] - layout.Staging[site])
                : layout.Staging[site];
            Vector2 direction = approach - post;
            return direction.sqrMagnitude > .01f ? direction : layout.Sites[site] - layout.Staging[site];
        }

        int BusiestSite(int viewerTeam, int[] team, VisionSystem vision, CombatSystem combat)
        {
            int best = -1, most = 0;
            for (int i = 0; i < layout.SiteCount; i++)
            {
                int seen = KnownEnemiesNear(viewerTeam, i, team, vision, combat);
                if (seen > most) { most = seen; best = i; }
            }
            return best;
        }

        int NearestSite(Vector2 point)
        {
            int best = 0; float distance = float.MaxValue;
            for (int i = 0; i < layout.SiteCount; i++)
            {
                float candidate = Vector2.Distance(point, layout.Sites[i]);
                if (candidate < distance) { distance = candidate; best = i; }
            }
            return best;
        }

        int NearestLiving(Vector2 point, Vector2[] positions, int[] team, int ctTeam, bool counterTerrorist, CombatSystem combat)
        {
            int best = -1; float distance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!combat.Alive(i)) continue;
                if ((team[i] == ctTeam) != counterTerrorist) continue;
                float candidate = Vector2.Distance(positions[i], point);
                if (candidate < distance) { distance = candidate; best = i; }
            }
            return best;
        }

        // Enemies the viewing team has a contact on, counted at their last known position.
        int KnownEnemiesNear(int viewerTeam, int site, int[] team, VisionSystem vision, CombatSystem combat)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                if (team[i] == viewerTeam || !combat.Alive(i)) continue;
                var contact = vision.Knowledge(viewerTeam, i);
                if (!contact.known) continue;
                if (Vector2.Distance(contact.lastKnownPosition, layout.Sites[site]) <= settings.threatRadius) total++;
            }
            return total;
        }

        int VisibleEnemiesNear(int viewerTeam, int site, int[] team, VisionSystem vision, CombatSystem combat)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                if (team[i] == viewerTeam || !combat.Alive(i)) continue;
                var contact = vision.Knowledge(viewerTeam, i);
                if (!contact.visible) continue;
                if (Vector2.Distance(contact.lastKnownPosition, layout.Sites[site]) <= settings.clearRadius) total++;
            }
            return total;
        }

        int LivingFriendsNear(int player, int site, Vector2[] positions, int[] team, int ctTeam, CombatSystem combat)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                if (team[i] != team[player] || !combat.Alive(i)) continue;
                if (Vector2.Distance(positions[i], layout.Sites[site]) <= settings.threatRadius) total++;
            }
            return total;
        }
    }
}
