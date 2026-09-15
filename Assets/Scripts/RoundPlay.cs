using System;
using UnityEngine;

namespace FpsManager
{
    public enum RoundPhase { Preparation, Setup, Execute, PostPlant, Ended }

    public enum RoundOutcome { None, TerroristsEliminated, CounterTerroristsEliminated, BombExploded, BombDefused, TimeExpired }

    // What a player is doing right now. Derived state, recomputed every tick.
    public enum PlayerTask { Idle, MoveToLane, PushSite, PlantBomb, RecoverBomb, HoldSite, DefendSite, Rotate, FallBack, Regroup, Retake, Defuse }

    // Test starting values, not balanced numbers. Round and bomb times follow the real
    // game; the radii are sized for this 100 x 100 map.
    [Serializable]
    public class RoundSettings
    {
        public float roundSeconds = 115f;
        public float executeDelaySeconds = 25f;  // latest moment terrorists leave their lane
        public float plantSeconds = 3.2f;
        public float defuseSeconds = 10f;        // no defuse kits yet
        public float bombSeconds = 40f;
        public float siteRadius = 12f;           // counts as being on the site
        // Attackers are counted over a wider circle than defenders: a push is spread over
        // the approach, and a defender who only counts bodies already standing on the site
        // has left it far too late to leave.
        public float threatRadius = 20f;
        public float interactRadius = 2.5f;      // reach for planting, defusing, picking the bomb up
        public float clearRadius = 20f;          // a visible enemy this close to the site stops a plant
        public float holdRadius = 6f;            // spread of the holding positions around a site
    }

    // Map specific anchor points. Built by the map code, not hard coded here.
    public sealed class MapLayout
    {
        public Vector2[] Sites;        // plant spots
        public string[] SiteNames;
        public Vector2[] Staging;      // per site: where defenders fall back to and retake from
        public Vector2[][] HoldRing;   // per site: spread positions around it
        public int SiteCount { get { return Sites.Length; } }
    }

    public struct PlayerObjective
    {
        public Vector2 destination;
        public Vector2 watch;      // direction to face once standing still
        public PlayerTask task;
        public bool valid;
        // Break off and move even with an enemy in sight. Without this a defender can
        // never leave a site, because seeing anyone pins them in a firefight. A player
        // breaking off does not shoot while they move: firing on the move is not
        // modelled, so the retreat costs them their gun until they are set again.
        public bool disengage;
    }

    // Round rules and the team level decisions that drive them.
    //
    // Every decision here uses only what a team has actually seen, through VisionSystem,
    // never the true positions of the other side. The one exception is a team's own
    // players, which it always knows.
    public sealed class RoundDirector
    {
        readonly RoundSettings settings;
        readonly MapLayout layout;
        readonly int count;
        readonly PlayerObjective[] objectives;
        readonly int[] slot;
        readonly bool[] ready=new bool[10];              // stable index of a player within their team
        DeterministicRandom random;

        public RoundPhase Phase { get; private set; }
        public RoundOutcome Outcome { get; private set; }
        public float Clock { get; private set; }
        public int TargetSite { get; private set; }
        public int PlantedSite { get; private set; }
        public int Carrier { get; private set; }
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
            objectives = new PlayerObjective[count];
            slot = new int[count];
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
            Clock = 0f; PlantProgress = 0f; DefuseProgress = 0f; BombTimer = 0f;
            PlantedSite = -1; Carrier = -1; BombDropped = false; TargetSite = 0;
            for (int i = 0; i < count; i++) { objectives[i] = new PlayerObjective(); ready[i]=false; }
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
                ready[i] |= Vector2.Distance(positions[i],homeAnchor[i]) <= settings.interactRadius*2 || Clock >= 9+slot[i]*2;
            UpdateBombCarrier(positions, team, ctTeam, combat);
            if (Phase == RoundPhase.Setup && ReadyToExecute(positions, team, ctTeam, homeAnchor, combat))
                Phase = RoundPhase.Execute;
            if (Phase == RoundPhase.PostPlant) UpdatePlantedBomb(delta, positions, team, ctTeam, combat);
            else UpdatePlanting(delta, positions, team, tTeam, vision, combat);
            if (Phase == RoundPhase.Ended) return;

            for (int i = 0; i < count; i++)
                objectives[i] = combat.Alive(i)
                    ? (team[i] == ctTeam
                        ? DefenderObjective(i, positions, team, ctTeam, homeAnchor, composure, vision, combat)
                        : AttackerObjective(i, positions, team, ctTeam, homeAnchor, vision, combat))
                    : new PlayerObjective();

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
                if (team[i] == ctTeam || !combat.Alive(i)) continue;
                if (Vector2.Distance(positions[i], BombPosition) > settings.interactRadius) continue;
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
            if (Carrier < 0 || Phase != RoundPhase.Execute) { PlantProgress = 0f; return; }
            bool inPlace = Vector2.Distance(positions[Carrier], layout.Sites[TargetSite]) <= settings.interactRadius;
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

        void UpdatePlantedBomb(float delta, Vector2[] positions, int[] team, int ctTeam, CombatSystem combat)
        {
            BombTimer -= delta;
            if (BombTimer <= 0f) { BombTimer = 0f; End(RoundOutcome.BombExploded); return; }
            int defuser = -1;
            for (int i = 0; i < count; i++)
            {
                if (team[i] != ctTeam || !combat.Alive(i) || combat.Engaging(i)) continue;
                if (Vector2.Distance(positions[i], BombPosition) > settings.interactRadius) continue;
                defuser = i; break;
            }
            if (defuser < 0) { DefuseProgress = 0f; return; }
            DefuseProgress += delta;
            if (DefuseProgress >= settings.defuseSeconds) End(RoundOutcome.BombDefused);
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
            // Everyone else spreads around the site: covering the approaches before the
            // plant is the same job as holding them afterwards.
            objective.task = PlantedSite >= 0 ? PlayerTask.HoldSite : PlayerTask.PushSite;
            objective.destination = HoldSpot(site, slot[player]);
            objective.watch = WatchDirection(team[player], site, objective.destination, team, vision, combat, false);
            return objective;
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

            int home = HomeSite(homeAnchor[player]);
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

            // The retake decision. A defender compares the enemies their team has actually
            // seen on this site against the team mates they can count there. Composure buys
            // patience: a calm player holds while a body down, a very calm one while two.
            int seen = KnownEnemiesNear(ctTeam, home, team, vision, combat);
            int friends = LivingFriendsNear(player, home, positions, team, ctTeam, combat);
            int tolerance = composure[player] >= 90 ? 1 : 0;
            if (seen > friends + tolerance)
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
            objective.task = PlayerTask.DefendSite;
            objective.destination = homeAnchor[player];
            objective.watch = WatchDirection(ctTeam, home, homeAnchor[player], team, vision, combat, true);
            return objective;
        }

        Vector2 HoldSpot(int site, int teamSlot)
        {
            var ring = layout.HoldRing[site];
            return ring[teamSlot % ring.Length];
        }

        int HomeSite(Vector2 anchor)
        {
            for (int i = 0; i < layout.SiteCount; i++)
                if (Vector2.Distance(anchor, layout.Sites[i]) <= settings.siteRadius) return i;
            return -1;
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
