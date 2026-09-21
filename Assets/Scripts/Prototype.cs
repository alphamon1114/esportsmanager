using System;
using System.Collections.Generic;
using UnityEngine;

namespace FpsManager
{
#if UNITY_5_3_OR_NEWER
    [DefaultExecutionOrder(200)]
#endif
    public partial class Prototype : MonoBehaviour
    {
        public Database Data { get; private set; }
        readonly List<GameObject> actors = new List<GameObject>();
        readonly List<Material> materials = new List<Material>();
        Camera mapCamera, eyeCamera;
        RenderTexture mapTexture, eyeTexture;
        string AlliedTeamId = "spirit";
        int selected, ctTeam;
        PlayerMatchState[] matchState;
        public RoundOutcome LastRoundOutcome { get; private set; }
        public int CompletedRounds { get; private set; }
        public PlayerMatchState MatchState(int player) { return matchState[player]; }
        readonly List<Rect> obstacles = new List<Rect>();
        DeploymentNavigation navigation;
        VisionSystem vision;
        readonly VisionSettings visionSettings = new VisionSettings();
        readonly int[] teamIndex = new int[10];
        readonly Vector2[] visionPositions = new Vector2[10];
        readonly Vector2[] visionFacing = new Vector2[10];
        bool fogOfWar = false;
        bool showDebugInfo = false;
        CombatSystem combat;
        readonly CombatSettings combatSettings = new CombatSettings();
        readonly WeaponProfile weapon = new WeaponProfile();
        readonly bool[] alive = new bool[10];
        readonly bool[] arrived = new bool[10];
        readonly int[] aimStats = new int[10];
        readonly int[] composureStats = new int[10];
        Material deadMaterial;
        int roundSeed = 12345;
        RoundDirector director;
        PlayerAutonomy autonomy;
        public PlayerAutonomy Autonomy { get { return autonomy; } }
        readonly RoundSettings roundSettings = new RoundSettings();
        MapLayout layout;
        bool roundMode;
        readonly Vector2[] homeAnchor = new Vector2[10];
        readonly Vector2[] destinations = new Vector2[10];
        readonly float[] moveSpeed = new float[10];
        readonly PeekMovement[] peeking = new PeekMovement[10];
        readonly MovementAim[] movementAim = new MovementAim[10];
        readonly bool[] routeValid = new bool[10];
        readonly float[] repathDelay = new float[10];
        readonly bool[] moving = new bool[10];
        readonly int[] assignments = { 0, 1, 2, 0, 2, 0, 1, 2, 0, 2 };
        readonly List<List<Vector2>> routes = new List<List<Vector2>>();
        readonly int[] routeSteps = new int[10];
        readonly Vector2[] ctZones = { new Vector2(18,22), new Vector2(65,38), new Vector2(83,33) };
        readonly Vector2[] tZones = { new Vector2(27,55), new Vector2(45,61), new Vector2(89,63) };
        bool deploymentStarted, paused;
        float elapsed;
        Material ctMaterial, tMaterial;
        string preparationError;
        public bool DeploymentComplete { get; private set; }
        Vector2 scroll;
        string error;
        readonly Vector2[] ctPositions = { new Vector2(50,9), new Vector2(53,9), new Vector2(56,9), new Vector2(51.5f,12), new Vector2(54.5f,12) };
        readonly Vector2[] tPositions = { new Vector2(26,88), new Vector2(30,88), new Vector2(34,88), new Vector2(28,91), new Vector2(32,91) };

        public void Initialize()
        {
            if (Data != null) return;
            var asset = Resources.Load<TextAsset>("teams");
            if (asset == null) throw new InvalidOperationException("Resources/teams.json is missing.");
            Data = JsonUtility.FromJson<Database>(ConditionRules.MigrateJson(asset.text));
            if (Data.teams.Length != 2 || Data.players.Length != 10) throw new InvalidOperationException("Expected two teams and ten players.");
            matchState=new PlayerMatchState[Data.players.Length];
            for(int i=0;i<matchState.Length;i++) matchState[i]=new PlayerMatchState();
            BuildMap();
            BuildElevation();
            navigation = new DeploymentNavigation(obstacles);
            for (int i = 0; i < Data.players.Length; i++) teamIndex[i] = Data.players[i].teamId == Data.teams[0].id ? 0 : 1;
            vision = new VisionSystem(navigation, visionSettings, Data.players.Length);
            combat = new CombatSystem(combatSettings, weapon, Data.players.Length);
            for (int i = 0; i < Data.players.Length; i++)
            {
                aimStats[i] = Data.players[i].stats.aim;
                composureStats[i] = EffectiveStats(i).composure;
                routes.Add(new List<Vector2>());
            }
            layout = BuildLayout();
            director = new RoundDirector(roundSettings, layout, Data.players.Length);
            ctMaterial=Material(new Color(.18f,.65f,1)); tMaterial=Material(new Color(1,.62f,.18f));
            deadMaterial=Material(new Color(.30f,.30f,.32f));
            mapTexture = new RenderTexture(900,900,16);
            eyeTexture = new RenderTexture(800,450,16);
            mapCamera = NewCamera("Tactical camera", mapTexture);
            mapCamera.transform.position = new Vector3(50,120,50);
            mapCamera.transform.rotation = Quaternion.Euler(90,0,0);
            mapCamera.orthographic = true;
            mapCamera.cullingMask=~(1<<9);
            mapCamera.orthographicSize = 53;
            eyeCamera = NewCamera("Selected player camera", eyeTexture);
            var displayCamera=NewCamera("Broadcast display",null);displayCamera.cullingMask=0;displayCamera.depth=-10;
            eyeCamera.fieldOfView = 85;
            eyeCamera.nearClipPlane = .08f;
            eyeCamera.cullingMask = ~(1 << 8);
            var output = NewCamera("Display", null);
            output.cullingMask = 0;
            output.depth = -10;
            for (int i=0;i<Data.players.Length;i++)
            {
                var actor = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                actor.name = Data.players[i].handle;
                actor.transform.SetParent(transform);
                actor.transform.localScale = new Vector3(1.2f,1,1.2f);
                actors.Add(actor);
            }
            PlaceTeams();
        }
        void Start() { try { Initialize(); ActivateMatchMap("de_inferno");PlaceTeams(); ShuffleSpawnPlayers(); PrepareIglOrders(); OpenMainMenu(); } catch(Exception ex) { error=ex.Message; Debug.LogException(ex); } }
        Material Material(Color color)
        {
            var m = new Material(Shader.Find("Unlit/Color")); m.color=color; materials.Add(m); return m;
        }
        GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            if(name=="Wall"||name=="Cover") { var rect=new Rect(position.x-scale.x/2,100-position.z-scale.z/2,scale.x,scale.z);obstacles.Add(rect);elevation.Add(rect,position.y-scale.y/2,position.y+scale.y/2); }
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name;
            obj.transform.SetParent(transform); obj.transform.position=position; obj.transform.localScale=scale;
            obj.GetComponent<Renderer>().sharedMaterial=material;arenaObjects.Add(obj); return obj;
        }
        Camera NewCamera(string name, RenderTexture target)
        {
            var obj=new GameObject(name); obj.transform.SetParent(transform);
            var c=obj.AddComponent<Camera>(); c.targetTexture=target;
            c.clearFlags=CameraClearFlags.SolidColor; c.backgroundColor=new Color(.06f,.08f,.11f); return c;
        }
        Vector3 World(Vector2 p, float height=1) { return new Vector3(p.x,height,100-p.y); }
        void BuildMap()
        {
            var floor=Material(new Color(.32f,.35f,.38f)); var wall=Material(new Color(.13f,.17f,.21f));
            var cover=Material(new Color(.46f,.36f,.24f));
            Box("Ground",new Vector3(50,-.5f,50),new Vector3(108,1,108),floor);
            Vector2[][] paths={
                new[]{new Vector2(18,20),new Vector2(37,20),new Vector2(37,10),new Vector2(55,10),new Vector2(55,21),new Vector2(83,21),new Vector2(83,33)},
                new[]{new Vector2(26,89),new Vector2(15,75),new Vector2(15,63),new Vector2(27,54),new Vector2(27,37),new Vector2(18,37),new Vector2(18,20)},
                new[]{new Vector2(26,89),new Vector2(45,76),new Vector2(45,38),new Vector2(65,38),new Vector2(65,21)},
                new[]{new Vector2(45,50),new Vector2(65,50),new Vector2(83,33)},
                new[]{new Vector2(26,89),new Vector2(72,89),new Vector2(89,76),new Vector2(89,56),new Vector2(92,43),new Vector2(83,33)},
                new[]{new Vector2(45,70),new Vector2(89,70)}
            };
            for(int x=0;x<100;x+=2) for(int y=0;y<100;y+=2)
            {
                var p=new Vector2(x+1,y+1); bool open=false;
                foreach(var path in paths) for(int k=1;k<path.Length;k++)
                {
                    var a=path[k-1]; var d=path[k]-a;
                    if(Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude))<4.5f) open=true;
                }
                if(p.x>8&&p.x<29&&p.y>11&&p.y<30 || p.x>73&&p.x<94&&p.y>24&&p.y<42) open=true;
                if(!open) Box("Wall",World(p,2),new Vector3(2,4,2),wall);
            }
            Box("B plant area",World(new Vector2(18,20),.025f),new Vector3(10,.05f,9),Material(new Color(.28f,.48f,.55f)));
            Box("A plant area",World(new Vector2(83,33),.025f),new Vector3(10,.05f,9),Material(new Color(.65f,.43f,.23f)));
            foreach(var p in new[]{new Vector2(13,16),new Vector2(25,25),new Vector2(79,29),new Vector2(89,37),new Vector2(24,44)})
                Box("Cover",World(p,1.25f),new Vector3(3,2.5f,3),cover);
        }
        // Anchors the round logic needs, derived from the map rather than hard coded, so
        // that changing BuildMap keeps the tactics pointing at real ground.
        MapLayout BuildLayout()
        {
            var sites = new[] { new Vector2(83,33), new Vector2(18,20) };   // A, B plant areas
            var result = new MapLayout();
            result.Sites = sites;
            result.SiteNames = new[] { "A", "B" };
            result.AttackerSpawn=new Vector2(30,89);
            result.FlankRoutes=new[]{
                new[]{new[]{new Vector2(27,54),new Vector2(18,20),new Vector2(37,20),new Vector2(55,10),new Vector2(65,21),new Vector2(83,21)},
                      new[]{new Vector2(45,50),new Vector2(45,38),new Vector2(65,38),new Vector2(65,21),new Vector2(83,21)}},
                new[]{new[]{new Vector2(89,63),new Vector2(83,33),new Vector2(65,21),new Vector2(55,10),new Vector2(37,10),new Vector2(18,20)},
                      new[]{new Vector2(45,50),new Vector2(45,38),new Vector2(65,38),new Vector2(65,21),new Vector2(55,10),new Vector2(37,10),new Vector2(18,20)}}
            };
            result.Staging = new Vector2[sites.Length];
            result.HoldRing = new Vector2[sites.Length][];
            result.Approaches = new Vector2[sites.Length][];
            result.PeekPost = new Vector2[sites.Length][];
            result.CoverPost = new Vector2[sites.Length][];
            var spawn = new Vector2(0,0);
            foreach(var p in ctPositions) spawn += p;
            spawn /= ctPositions.Length;
            for(int i=0;i<sites.Length;i++)
            {
                result.Staging[i] = BackAlongApproach(spawn, sites[i], 16f);
                result.HoldRing[i] = Ring(sites[i], roundSettings.holdRadius, 5);
                result.Approaches[i] = EntryMouths(sites[i]);
                result.PeekPost[i] = new Vector2[result.Approaches[i].Length];
                result.CoverPost[i] = new Vector2[result.Approaches[i].Length];
                for(int m=0;m<result.Approaches[i].Length;m++)
                {
                    Vector2 peek, cover;
                    HoldingPair(sites[i], result.Approaches[i][m], out peek, out cover);
                    result.PeekPost[i][m] = peek;
                    result.CoverPost[i][m] = cover;
                }
            }
            return result;
        }
        // The mouths attackers actually come through, taken from their own routes rather
        // than guessed. A site with three ways in gets three of them, so defenders can
        // cover different ones instead of all staring down the same line.
        Vector2[] EntryMouths(Vector2 site)
        {
            var mouths=new List<Vector2>();
            foreach(var lane in tZones)
            {
                List<Vector2> route;
                try { route=navigation.Route(lane,site,new List<Vector2>()); }
                catch(InvalidOperationException) { continue; }
                Vector2 mouth=lane;
                for(int i=route.Count-1;i>=0;i--)
                {
                    mouth=route[i];
                    if(Vector2.Distance(route[i],site)>roundSettings.siteRadius) break;
                }
                bool duplicate=false;
                foreach(var existing in mouths) if(Vector2.Distance(existing,mouth)<8f) duplicate=true;
                if(!duplicate) mouths.Add(mouth);
            }
            if(mouths.Count==0) mouths.Add(site);
            return mouths.ToArray();
        }
        // A place to hold one mouth from: somewhere on the site that can see it, paired
        // with a spot a step away that cannot. The player lives on the first and ducks to
        // the second while reloading or blinded.
        void HoldingPair(Vector2 site, Vector2 mouth, out Vector2 peek, out Vector2 cover)
        {
            peek=site; cover=site;
            float best=float.MinValue;
            for(float dx=-10f;dx<=10f;dx+=1.5f) for(float dy=-10f;dy<=10f;dy+=1.5f)
            {
                var candidate=site+new Vector2(dx,dy);
                float toMouth=Vector2.Distance(candidate,mouth);
                if(toMouth<4f||toMouth>32f) continue;
                if(!navigation.Clear(candidate,candidate)||!navigation.Clear(site,candidate)) continue;
                if(!navigation.SightClear(candidate,mouth)) continue;
                Vector2 shelter=candidate; bool sheltered=false;
                for(int step=0;step<8&&!sheltered;step++)
                {
                    float angle=step*45f*Mathf.Deg2Rad;
                    var spot=candidate+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*3f;
                    if(!navigation.Clear(spot,spot)||!navigation.Clear(candidate,spot)) continue;
                    if(navigation.SightClear(spot,mouth)) continue;
                    shelter=spot; sheltered=true;
                }
                // Close to the mouth beats far from it, and a spot with somewhere to duck
                // beats one without. Staying near the site keeps the bomb covered.
                float score=(sheltered?40f:0f)-toMouth-Vector2.Distance(candidate,site)*.5f;
                if(score<=best) continue;
                best=score; peek=candidate; cover=sheltered?shelter:candidate;
            }
        }
        // Walks back along the defenders' own approach to find where they regroup. Using
        // the route rather than a fixed offset keeps the spot reachable and behind them.
        Vector2 BackAlongApproach(Vector2 from, Vector2 site, float distance)
        {
            List<Vector2> route;
            try { route = navigation.Route(from, site, new List<Vector2>()); }
            catch(InvalidOperationException) { return site; }
            float walked=0;
            for(int i=route.Count-1;i>0;i--)
            {
                float segment=Vector2.Distance(route[i],route[i-1]);
                if(walked+segment>=distance)
                {
                    float share=(distance-walked)/Mathf.Max(segment,.001f);
                    Vector2 point=route[i]+(route[i-1]-route[i])*share;
                    return navigation.Clear(point,point)?point:route[i-1];
                }
                walked+=segment;
            }
            return route.Count>0?route[0]:site;
        }
        Vector2[] Ring(Vector2 centre, float radius, int slots)
        {
            var result=new Vector2[slots];
            for(int i=0;i<slots;i++)
            {
                float angle=360f/slots*i*Mathf.Deg2Rad;
                var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                result[i]=centre;
                for(float r=radius;r>=1f;r-=.5f)
                {
                    var candidate=centre+direction*r;
                    if(!navigation.Clear(candidate,candidate)||!navigation.Clear(centre,candidate)) continue;
                    result[i]=candidate; break;
                }
            }
            return result;
        }
        void ClearRoutes()
        {
            for(int i=0;i<routes.Count;i++) routes[i]=new List<Vector2>();
            Array.Clear(routeSteps,0,routeSteps.Length);
            for(int i=0;i<routeValid.Length;i++) { routeValid[i]=false; repathDelay[i]=0f; }
        }
        // Re-paths only when the goal actually moved, and at most twice a second per
        // player: Route is a full grid search and objectives change rarely.
        void MoveTo(int player, Vector2 destination)
        {
            if(sourceArena!=null){SourceMoveTo(player,destination);return;}
            if(routeValid[player]&&(destination-destinations[player]).sqrMagnitude<1f) return;
            if(repathDelay[player]>0f) return;
            destinations[player]=destination; repathDelay[player]=.5f;
            try
            {
                if(roundMode&&teamIndex[player]!=ctTeam&&Vector2.Distance(MapPosition(player),destination)>18)
                {
                    var threats=new List<Vector2>();
                    for(int enemy=0;enemy<10;enemy++)if(teamIndex[enemy]!=teamIndex[player])
                    {
                        var info=vision.Knowledge(teamIndex[player],enemy);
                        if(info.known&&!info.anonymous&&info.age<3)threats.Add(info.lastKnownPosition);
                    }
                    routes[player]=navigation.CombatRoute(MapPosition(player),destination,layout.AttackerSpawn,threats);
                }
                else routes[player]=navigation.Clear(MapPosition(player),destination)?new List<Vector2>{destination}:navigation.Route(MapPosition(player),destination,new List<Vector2>());
                routeSteps[player]=0; routeValid[player]=true;
            }
            catch(InvalidOperationException) { routeValid[player]=false; }
        }
        void StepRoute(int player, float tick)
        {
            if(sourceArena!=null){SourceStepRoute(player,tick);return;}
            Vector2 previousPosition=MapPosition(player);
            float speed=5f;
            if(roundMode)
            {
                float desired=autonomy!=null&&autonomy.Walking[player]?2.4f:AutomaticMatch&&combat.KnifeOut(player)?5.75f:5f;
                // Brake only at the final destination, not at every navigation corner.
                if(routeSteps[player]==routes[player].Count-1)
                    desired=Mathf.Min(desired,Mathf.Max(.6f,Vector2.Distance(previousPosition,routes[player][routeSteps[player]])*6f));
                float change=tick*24f;
                moveSpeed[player]+=Mathf.Clamp(desired-moveSpeed[player],-change,change);
                speed=moveSpeed[player];
            }
            float remaining=tick*speed;
            while(remaining>0&&routeSteps[player]<routes[player].Count)
            {
                Vector3 target=World(routes[player][routeSteps[player]]),position=actors[player].transform.position;
                if(ElevatedMatch)target.y=position.y;
                Vector3 direction=target-position;
                float distance=direction.magnitude;
                if(!roundMode&&distance>.001f) actors[player].transform.rotation=Quaternion.RotateTowards(actors[player].transform.rotation,Quaternion.LookRotation(direction),360*tick);
                float step=Mathf.Min(remaining,distance);
                actors[player].transform.position=Vector3.MoveTowards(position,target,step);GroundActor(player);
                remaining-=step;
                if(distance<=step+.001f) routeSteps[player]++; else break;
            }
            if(roundMode&&autonomy!=null) autonomy.Footstep(player,MapPosition(player),Vector2.Distance(previousPosition,MapPosition(player)));
        }
        // Navigation writes position only. Aim tracks a known world point, including
        // behind the direction of travel when strafing or backing into cover.
        void AimWhileMoving(int player, PlayerObjective order, float tick)
        {
            Vector2 position=MapPosition(player), fallback=position+MapFacing(player)*12;
            bool holding=order.task==PlayerTask.DefendSite||order.task==PlayerTask.HoldSite;
            if(order.valid&&holding&&Vector2.Distance(position,order.destination)<8&&order.watch.sqrMagnitude>.001f)
                fallback=position+order.watch.normalized*12;
            else if(routeSteps[player]<routes[player].Count)
            {
                int next=routeSteps[player];
                if(Vector2.Distance(position,routes[player][next])<3&&next+1<routes[player].Count) next++;
                fallback=routes[player][next];
            }
            else if(order.valid&&order.watch.sqrMagnitude>.001f) fallback=position+order.watch.normalized*12;
            if(AutomaticMatch)fallback=navigation.VisibleAimPoint(position,ExpectedAngle(player,order,tick));
            FaceWatch(player,SharedAimDirection(player,fallback),tick);

        }
        void FaceWatch(int player, Vector2 watch, float tick)
        {
            if(UnifiedRoundAim)
            {
                if(aimApplied[player])return;aimApplied[player]=true;
                if(combat.FocusTarget(player)>=0)watch=combat.FocusPoint(player)-MapPosition(player);
                else if(ImmediateCue(player,out var threatPoint))watch=threatPoint-MapPosition(player);
                else if(combat.Spamming(player))watch=combat.SuppressionPoint(player)-MapPosition(player);
                else if(clutchSearch[player]!=null&&clutchSearch[player].Active)watch=clutchSearch[player].Point-MapPosition(player);
            }
            if(watch.sqrMagnitude<.0001f) return;
            if(UnifiedRoundAim&&combat.FocusTarget(player)<0){var point=MapPosition(player)+watch;combat.PreAimHeight(player,MapFloor(point,PlayerHeight(player)),watch.magnitude,tick,aimStats[player]);}
            if(UnifiedRoundAim&&combat.PhysicalBullets&&combat.FocusTarget(player)>=0){
                var facing=combat.HumanFacing(player,MapPosition(player),MapFacing(player),MapPosition(player)+watch,tick,aimStats[player]);
                actors[player].transform.rotation=Quaternion.LookRotation(new Vector3(facing.x,0,-facing.y));return;
            }
            var target=Quaternion.LookRotation(new Vector3(watch.x,0,-watch.y));
            actors[player].transform.rotation=Quaternion.RotateTowards(actors[player].transform.rotation,target,(UnifiedRoundAim?120+Mathf.Clamp01(aimStats[player]/100f)*60:180)*tick);
        }
        public void PlaceTeams()
        {
            ResetEquipmentSave();ResetElevation();
            ClearGroundWeapons();ClearBombEquipment();
            deploymentStarted=false; paused=false; elapsed=0; DeploymentComplete=false; roundMode=false;
            ClearRoutes(); Array.Clear(moveSpeed,0,moveSpeed.Length); preparationError=null;
            autonomy=null; SyncUtilityVisuals();
            if(vision!=null) { vision.Reset(); vision.LegacyFootsteps=true; vision.ExtraSight=null; vision.Blinded=null; }
            if(combat!=null) { combat.PhysicalBullets=false;combat.BulletClear=null;combat.BulletTraced=null;combat.UnifiedAim=false; combat.DamageDealt=null; combat.VisualShot=null; ResetShotPresentation(); combat.AmmoEnabled=false; combat.Killed=null; combat.ShotFired=null; combat.ReloadStarted=null;combat.ReloadAllowed=null; combat.ShotMissed=null; combat.FollowupChosen=null; }
            if(combat!=null) { for(int i=0;i<10;i++) combat.Equip(i,null); combat.Reset(roundSeed); }
            if(director!=null) director.Reset();
            for(int i=0;i<alive.Length;i++) { alive[i]=true; arrived[i]=false; moving[i]=false; }
            Array.Clear(homeAnchor,0,homeAnchor.Length);
            Array.Clear(destinations,0,destinations.Length);
            Array.Clear(visionPositions,0,visionPositions.Length);
            Array.Clear(visionFacing,0,visionFacing.Length);
            int ct=0,t=0;
            for(int i=0;i<actors.Count;i++)
            {
                bool isCt=Data.players[i].teamId==Data.teams[ctTeam].id;
                var p=isCt?ctPositions[ct++]:tPositions[t++];
                actors[i].transform.position=World(p);SourceSpawn(i);
                var target=isCt?new Vector2(45,76):new Vector2(45,50);
                actors[i].transform.LookAt(World(target));
                actors[i].GetComponent<Renderer>().sharedMaterial=isCt?ctMaterial:tMaterial;
            }
            Select(selected);
        }
        // Independent RNG stream: spawn draws never consume combat or tactic randomness.
        void ShuffleSpawnPlayers()
        {
            var random=new DeterministicRandom(unchecked(roundSeed ^ (int)0x6D2B79F5));
            for(int team=0;team<2;team++)
            {
                var slots=(Vector2[])(team==ctTeam?ctPositions:tPositions).Clone();
                for(int j=slots.Length-1;j>0;j--)
                {
                    int k=(int)(random.NextUInt()%(uint)(j+1));
                    Vector2 swap=slots[j]; slots[j]=slots[k]; slots[k]=swap;
                }
                int slot=0;
                for(int i=0;i<actors.Count;i++) if(teamIndex[i]==team)
                {
                    actors[i].transform.position=World(slots[slot++]);SourceSpawn(i);
                    actors[i].transform.LookAt(World(team==ctTeam?new Vector2(45,76):new Vector2(45,50)));
                }
            }
            Select(selected);
        }
        public void PrepareNextRound()
        {
            CompleteRoundOnce();
            roundSeed=unchecked(roundSeed+1);
            // MatchState, roster stats and prepared team configuration are not round state.
            PlaceTeams();
            ShuffleSpawnPlayers();
            PrepareIglOrders();
        }
        public void AdvanceFrame(float delta)
        {
            if(PauseMenuOpen)return;
            if(FastForwarding){PumpFastForward();return;}
            if(AwaitingMapStart||menuPage!=0||tournamentBoard)return;
            if(AutomaticMatch){AdvanceMatch(delta);RecordTournamentMap();return;}
            // Preparation does not reveal contacts or resume combat at the new spawn.
            if(!deploymentStarted||paused) return;
            SimulateMovement(delta);
            if(roundMode && director.Phase==RoundPhase.Ended) PrepareNextRound();
        }
        public void Select(int index)
        {
            if(index<0||index>=actors.Count) return;
            if(selected!=index){ClearViewKick();if(sourceArena!=null&&HasSourceFloors)SetSourceRadar(PlayerHeight(index)<SourceFloorSplit);}
            selected=index;

            for(int i=0;i<actors.Count;i++) actors[i].layer=i==selected?8:0;
            SyncCharacterVisuals();
            eyeCamera.transform.position=actors[index].transform.position+Vector3.up*(IsCrouched(index)?.10f:.65f);
            eyeCamera.transform.rotation=actors[index].transform.rotation;
#if UNITY_5_3_OR_NEWER
            eyeCamera.transform.rotation*=Quaternion.Euler(-combat.AimElevation(index),0,0);
#endif
            SyncFirstPersonEquipment();
        }
        public bool IsAlliedPlayer(int player) { return player >= 0 && player < Data.players.Length && Data.players[player].teamId == AlliedTeamId; }
        public void AssignZone(int player, int zone)
        {
            if(deploymentStarted) throw new InvalidOperationException("Deployment is locked after start.");
            if(player<0||player>=10||zone<0||zone>2) throw new ArgumentOutOfRangeException();
            if(!IsAlliedPlayer(player)) throw new InvalidOperationException("Opponent deployment is AI controlled.");
            assignments[player]=zone;
        }
        // Movement test: everyone walks to their assigned zone once and stops. Kept
        // separate from a real round so the deployment, vision and combat checks keep
        // exercising the same simple behaviour they were written against.
        public void BeginDeployment()
        {
            if(deploymentStarted) return;
            var reserved=new List<Vector2>();
            for(int i=0;i<actors.Count;i++)
            {
                homeAnchor[i]=(teamIndex[i]==ctTeam?ctZones:tZones)[assignments[i]];
                routes[i]=navigation.Route(MapPosition(i),homeAnchor[i],reserved);
                routeValid[i]=true; destinations[i]=homeAnchor[i];
            }
            Array.Clear(routeSteps,0,routeSteps.Length);
            deploymentStarted=true; roundMode=false; paused=false; DeploymentComplete=false; preparationError=null;
        }
        // A real round: the assigned zone becomes the opening position, and from there the
        // round director decides where everyone goes.
        // IGL opening orders use own roster and the round seed, never enemy information.
        // Charisma bonuses and persistent tactical styles are not implemented yet.
        public void PrepareIglOrders()
        {
            if(deploymentStarted) return;
            for(int team=0;team<Data.teams.Length;team++)
            {
                var members=new List<int>();
                for(int i=0;i<Data.players.Length;i++) if(teamIndex[i]==team) members.Add(i);
                int captain=members.Find(i=>Data.players[i].id==Data.teams[team].iglPlayerId);
                if(!members.Contains(captain)||Data.players[captain].id!=Data.teams[team].iglPlayerId)
                    throw new InvalidOperationException("Team has no valid IGL: "+Data.teams[team].name);
                var rng=new DeterministicRandom(unchecked(roundSeed ^ (team+1)*73856093));
                bool ct=team==ctTeam;
                int[] slots=(rng.NextUInt()%2==0)?new[]{0,0,1,1,2}:new[]{0,0,1,2,2};
                if(AutomaticMatch)slots=SideSlots(team);
                // Mirror the two sites, so one site is not always the heavily staffed one.
                if(!AutomaticMatch&&rng.NextUInt()%2==0) for(int j=0;j<slots.Length;j++) if(slots[j]!=1) slots[j]=2-slots[j];
                for(int s=0;s<slots.Length;s++)
                {
                    int best=-1; float score=float.MinValue;
                    foreach(int i in members)
                    {
                        var player=Data.players[i];
                        float candidate=rng.Next01()*10;
                        if(player.weaponPosition=="awper" && slots[s]==1 && !(AutomaticMatch&&ct&&DefenseFor(team)==DefenseTactic.Forward)) candidate+=30;
                        if(AutomaticMatch&&ct&&DefenseFor(team)==DefenseTactic.Forward&&slots[s]==1&&player.weaponPosition!="awper")candidate+=40;
                        if(player.riflerRole=="anchor_lurker" && ct && slots[s]!=1) candidate+=25;
                        if(player.riflerRole=="entry" && !ct && slots[s]!=1) candidate+=25;
                        if(candidate>score) { score=candidate; best=i; }
                    }
                    assignments[best]=slots[s]; members.Remove(best);
                }
            }
        }
        public int AssignedZone(int player) { return assignments[player]; }
        public void BeginRound()
        {
            if(deploymentStarted) return;
            RefreshConditionStats();PrepareIglOrders();
            for(int i=0;i<actors.Count;i++)
            {
                homeAnchor[i]=(teamIndex[i]==ctTeam?ctZones:tZones)[assignments[i]];
                destinations[i]=homeAnchor[i];
            }
            ClearRoutes();
            if(!AutomaticMatch)foreach(var state in matchState) state.RestockUtility();
            autonomy=new PlayerAutonomy(roundSeed,navigation); combat.ConfigureSpam(autonomy,navigation);
            autonomy.SetUtilityContext(teamIndex,visionPositions);
            for(int i=0;i<10;i++){movementAim[i]=new MovementAim();preAim[i]=new PreAimPlanner();awperMovement[i]=new AwperMovement(unchecked(roundSeed^(i+1)*19349663),navigation);awpCycle[i]=0;awpSidearm[i]=false;}
            for(int i=0;i<10;i++) peeking[i]=new PeekMovement(unchecked(roundSeed^(i+1)*73856093),navigation,EffectiveStats(i).movement,AutomaticMatch,EffectiveStats(i).composure,Data.players[i].riflerRole=="anchor_lurker");
            vision.LegacyFootsteps=false; vision.ExtraSight=autonomy.ClearSight; vision.Blinded=autonomy.Blinded;
            combat.UnifiedAim=AutomaticMatch;combat.AmmoEnabled=true;combat.ReloadAllowed=CanReloadSafely;ResetSafeReload();
            killFeed.Clear(); combat.Killed=RecordKill;
            for(int i=0;i<10;i++) { combat.Equip(i,WeaponCatalog.Equipped(matchState[i].equipment));combat.BindProtection(i,matchState[i]); }
            combat.ShotMissed=null;
            for(int i=0;i<10;i++) combat.SetMovementSkill(i,EffectiveStats(i).movement);
            combat.FollowupChosen=(i,style)=> { if(style==FollowupStyle.EvadeAndTap) peeking[i].RequestRetap(); };
            if(AutomaticMatch){ResetClutch();ResetThreats();ResetEquipmentSave();ResetFinishingCombat();Statistics.Begin(teamIndex,ctTeam);combat.DamageDealt=RecordCombatDamage;}
            combat.VisualShot=ShotPresentation;combat.PhysicalBullets=AutomaticMatch;combat.BulletClear=AutomaticMatch?new Func<Vector2,float,Vector2,float,bool>(elevation.Sight):null;combat.BulletTraced=PresentBullet;
            combat.ShotFired=i=> { autonomy.Sounds.Emit(i,MapPosition(i),SoundKind.Gunshot); if(AutomaticMatch&&combat.WeaponFor(i).id=="awp"){awpCycle[i]=combat.WeaponFor(i).fireInterval;awperMovement[i].Shot(MapPosition(i),visionFacing[i],combat.WeaponFor(i).fireInterval);} };
            combat.ReloadStarted=i=>autonomy.Sounds.Emit(i,MapPosition(i),SoundKind.Reload);
            ConfigureElevation();
            director.Begin(roundSeed,teamIndex,ctTeam);ConfigureBombEquipment();director.PreservingEquipment=IsSavingEquipment;KeepMidCarrierWithGroup();
            roundScored=false;
            director.StrategyTeam=AlliedTeamIndex;director.Strategy=AutomaticMatch?Strategy:TeamStrategy.Balanced;
            director.SoundIntel=autonomy.Sounds;
            director.ConfigureTactics(MatchPlayers(),teamIndex,ctTeam,navigation);
            director.ConfigureSidePlans(AutomaticMatch,DefenseFor(ctTeam),AttackFor(1-ctTeam),StackSite(),MatchPlayers());
            deploymentStarted=true; roundMode=true; paused=false; DeploymentComplete=false; preparationError=null;
            // Spawned players are stationary. Each round tick updates fire readiness
            // from actual translation, independently of their aim direction.
            for(int i=0;i<arrived.Length;i++) arrived[i]=true;
        }
        void Update() { CheckPauseInput();if(Data!=null&&error==null) AdvanceFrame(Time.deltaTime); }
        void LateUpdate() { if(eyeCamera!=null&&actors.Count>selected) Select(selected); SyncUtilityVisuals(); SyncGroundWeaponVisuals(); SyncBombEquipmentVisuals(); UpdateShotPresentation(); }
        public VisionSystem Vision { get { return vision; } }
        public DeploymentNavigation Navigation { get { return navigation; } }
        public int AlliedTeamIndex { get { return Data.teams[0].id==AlliedTeamId?0:1; } }
        public int TeamIndexOf(int player) { return teamIndex[player]; }
        public static readonly string[] CtZoneNames = { "B site", "Mid / Arch", "A site" };
        public static readonly string[] TZoneNames = { "Banana", "Mid", "Apartments" };
        public Vector2 DeploymentZone(bool counterTerrorist, int zone) { return (counterTerrorist?ctZones:tZones)[zone]; }
        public Vector2 MapPosition(int player)
        {
            Vector3 p=actors[player].transform.position; return new Vector2(p.x,100-p.z);
        }
        public Vector2 MapFacing(int player)
        {
            Vector3 f=actors[player].transform.forward; return new Vector2(f.x,-f.z);
        }
        public CombatSystem Combat { get { return combat; } }
        public bool IsAlive(int player) { return combat==null||combat.Alive(player); }
        public int RoundSeed { get { return roundSeed; } }
        public void SetRoundSeed(int seed) { roundSeed=seed; }
        public int LivingCount(int team) { return combat.LivingCount(teamIndex,team); }
        public RoundDirector Director { get { return director; } }
        public MapLayout Layout { get { return layout; } }
        public bool RoundMode { get { return roundMode; } }
        public int CounterTerroristTeam { get { return ctTeam; } }
        public Vector2 HomeAnchor(int player) { return homeAnchor[player]; }
        // Detection and engagement run whenever the clock runs, including while players
        // stand still, so team knowledge stays in step with the shown positions.
        // Order matters: move, then look, then shoot at what was just seen.
        void UpdateSenses(float delta)
        {
            if(vision==null||Data==null) return;
            for(int i=0;i<actors.Count;i++)
            {
                Vector3 p=actors[i].transform.position,f=actors[i].transform.forward;
                visionPositions[i]=new Vector2(p.x,100-p.z);
                visionFacing[i]=new Vector2(f.x,-f.z);
            }
            combat.FillAlive(alive);
            vision.Tick(delta,visionPositions,visionFacing,teamIndex,alive);
            combat.Tick(delta,visionPositions,visionFacing,teamIndex,arrived,aimStats,vision);
            combat.FillAlive(alive);
            for(int i=0;i<actors.Count;i++)
            {
                if(!alive[i]) { actors[i].GetComponent<Renderer>().sharedMaterial=deadMaterial; continue; }
                if(combat.UnifiedAim||!combat.Engaging(i)||moving[i]) continue;
                // Moving actors use the same independent aim for rendering and detection.
                // Combat may fire only after they have stopped.
                var f=visionFacing[i];
                actors[i].transform.rotation=Quaternion.LookRotation(new Vector3(f.x,0,-f.y));
            }
        }
        public void SimulateMovement(float delta)
        {
            float tick=Mathf.Min(delta,.1f);
            if(paused) return;
            if(roundMode) { SimulateRound(tick); return; }
            if(!deploymentStarted||DeploymentComplete) { UpdateSenses(tick); return; }
            bool complete=true;
            for(int i=0;i<actors.Count;i++)
            {
                // A dead player stops where they fell and counts as finished.
                if(!combat.Alive(i)) { routeSteps[i]=routes[i].Count; arrived[i]=true; continue; }
                if(routeSteps[i]>=routes[i].Count) { arrived[i]=true; continue; }
                StepRoute(i,tick);
                if(routeSteps[i]<routes[i].Count) complete=false; else arrived[i]=true;
            }
            elapsed+=tick; DeploymentComplete=complete;
            UpdateSenses(tick);
        }
        // Order per tick: act on the objectives decided last tick, then look, shoot, and
        // finally decide the next objectives from what was just observed.
        void SimulateRound(float tick)
        {
            if(director.Phase==RoundPhase.Ended) return;
            BeginPlayerMovement();
            SettleGroundWeapons(tick);TickBombEquipment(tick);
            autonomy.Tick(tick,visionPositions,visionFacing,alive);
            Array.Clear(aimApplied,0,aimApplied.Length);
            combat.UpdateEngagementFocus(tick,visionPositions,visionFacing,teamIndex,vision);if(AutomaticMatch){RememberThreats();ObserveCoach();DecideEquipmentSave();}
            for(int i=0;i<actors.Count;i++)
            {
                if(sourceArena!=null)sourceNavHeight=PlayerHeight(i);
                repathDelay[i]=Mathf.Max(0f,repathDelay[i]-tick);
                if(!combat.Alive(i)) { routeValid[i]=false; SettleDeadHeight(i,tick); continue; }
                moving[i]=false;
                if(AutomaticMatch&&StepSourceJump(i,tick))continue;
                tacticalCrouch[i]=false;
                if(AutomaticMatch&&StepUncontestedDefuse(i,tick))continue;
                if(AutomaticMatch&&StepEquipmentSave(i,tick))continue;
                if(AutomaticMatch&&StepCtOpening(i,tick))continue;
                if(AutomaticMatch)UpdateClutch(i);
                UpdateFinishingCombat(i,tick);
                var objective=autonomy.Decide(i,director.Objective(i),MapPosition(i),MatchPlayer(i),matchState[i],combat,vision,teamIndex[i],teamIndex[i]==ctTeam);
                UpdateAwperWeapon(i,tick);
                if(AutomaticMatch&&StepSafeReload(i,tick))continue;
                UpdateTravelWeapon(i,objective);
                if(AutomaticMatch&&StepThreatResponse(i,tick))continue;
                if(AutomaticMatch&&StepDefusing(i))continue;
                if(AutomaticMatch&&CollectClutchRifle(i,tick))continue;
                if(AutomaticMatch&&StepForwardAdvance(i,objective,tick))continue;
                if(AutomaticMatch&&StepStackPost(i,objective,tick))continue;
                if(StepAwper(i,objective,tick))continue;
                if(AutomaticMatch&&StepClutchSearch(i,objective,tick))continue;
                if(AlignBeforeEntry(i,objective,tick))continue;
                if(CollectNearbyWeapon(i,objective,tick))continue;
                if(StepElevation(i,objective,tick))continue;
                arrived[i]=true;
                Vector2 probe=MapPosition(i)+MapFacing(i)*12;
                bool contact=false;
                float closest=float.MaxValue;
                for(int enemy=0;!AutomaticMatch&&enemy<actors.Count;enemy++)
                {
                    if(teamIndex[enemy]==teamIndex[i]) continue;
                    var known=vision.Knowledge(teamIndex[i],enemy);
                    float distance=Vector2.Distance(MapPosition(i),known.lastKnownPosition);
                    if(!known.known||distance>=closest) continue;
                    closest=distance; probe=known.lastKnownPosition; contact=true;
                }
                // Unknown approach: inspect the next bend in our own route, not enemies.
                if(!contact&&routeSteps[i]+1<routes[i].Count)
                    probe=routes[i][routeSteps[i]+1];
                if(!contact&&AutomaticMatch)probe=ExpectedAngle(i,objective,0);
                if(AutomaticMatch)
                {
                    var direction=SharedAimDirection(i,probe);
                    contact=combat.FocusTarget(i)>=0||movementAim[i].HasContact;
                    if(contact)probe=MapPosition(i)+direction;
                }
                Vector2 peekTarget,peekWatch;
                if(combat.Reloading(i)||autonomy.Blinded[i]||combat.Health(i)<40||SuppressIdlePeek(i)||(clutchUrgent[i]&&combat.FocusTarget(i)<0))
                { var cancel=objective; cancel.valid=false; peeking[i].Step(tick,MapPosition(i),cancel,probe,contact,out peekTarget,out peekWatch); }
                else if(peeking[i].Step(tick,MapPosition(i),objective,probe,contact,out peekTarget,out peekWatch))
                {
                    var before=MapPosition(i);
                    if(AutomaticMatch&&clutchMode[i]&&Vector2.Dot(MapFacing(i),peekWatch.normalized)<.97f)peekTarget=before;
                    var next=Vector2.MoveTowards(before,peekTarget,tick*3.5f);
                    actors[i].transform.position=World(next);GroundActor(i);
                    moving[i]=Vector2.Distance(before,next)>.001f;
                    arrived[i]=!moving[i]&&peeking[i].ReadyToFire; // Stop and settle before shooting.
                    autonomy.Footstep(i,next,Vector2.Distance(before,next));
                    FaceWatch(i,peekWatch,tick);
                    routeValid[i]=false; repathDelay[i]=0; moveSpeed[i]=0;
                    continue;
                }
                // Prefer stopping to shoot; actual movement and stop recovery still widen the shot cone.
                // A player breaking off ignores that and keeps moving.
                bool seesEnemy=false;
                for(int enemy=0;enemy<actors.Count;enemy++) if(teamIndex[enemy]!=teamIndex[i]&&vision.Sees(i,enemy)) { seesEnemy=true; break; }
                bool intoCover=objective.hasCover&&Vector2.Distance(objective.destination,objective.cover)<.1f&&Vector2.Distance(MapPosition(i),objective.cover)>.2f;
                if(!objective.disengage&&!intoCover&&(seesEnemy||combat.Engaging(i))) { moveSpeed[i]=0; AimWhileMoving(i,objective,tick); continue; }
                if(objective.valid) MoveTo(i,objective.destination);
                if(routeValid[i]&&routeSteps[i]<routes[i].Count)
                {
                    Vector2 before=MapPosition(i);
                    StepRoute(i,tick); moving[i]=Vector2.Distance(before,MapPosition(i))>.001f;
                    arrived[i]=!moving[i];
                    AimWhileMoving(i,objective,tick);
                }
                else { moveSpeed[i]=0; AimWhileMoving(i,objective,tick); }
            }
            if(AutomaticMatch)ResolvePlayerMovement(tick);
            elapsed+=tick;
            for(int i=0;i<actors.Count;i++)
            {
                var order=director.Objective(i);
                combat.SpamAllowed[i]=!IsSavingEquipment(i)&&!moving[i]&&!order.disengage&&order.task!=PlayerTask.PlantBomb&&order.task!=PlayerTask.Defuse&&order.task!=PlayerTask.RecoverBomb&&order.task!=PlayerTask.Retake;
            }
            UpdateSenses(tick);
            autonomy.Sounds.Tick(tick,visionPositions,teamIndex,alive,navigation,vision);
            director.Tick(tick,visionPositions,teamIndex,ctTeam,homeAnchor,composureStats,vision,combat);
            autonomy.BombAudio(tick,director,visionPositions,teamIndex,ctTeam,combat);
        }
        public void ValidateMovementGeometry()
        {
            foreach(var actor in actors)
            {
                Vector3 p=actor.transform.position; var point=new Vector2(p.x,100-p.z);
                if(!navigation.Clear(point,point)) throw new InvalidOperationException("Player intersects geometry: "+actor.name);
            }
        }
        public void SwapPreviewSides() { if(!deploymentStarted) { ctTeam=1-ctTeam; PlaceTeams(); ShuffleSpawnPlayers(); PrepareIglOrders(); } }
        string RoundLine()
        {
            if(!deploymentStarted) return "PREPARATION / Round "+(CompletedRounds+1)+" / IGL orders ready"+(LastRoundOutcome!=RoundOutcome.None?" / Last: "+OutcomeText(LastRoundOutcome):"");
            if(!roundMode) return "MOVEMENT TEST / "+elapsed.ToString("F1")+"s";
            switch(director.Phase)
            {
                case RoundPhase.Setup: return "SETUP / "+director.Clock.ToString("F0")+"s";
                case RoundPhase.Execute:
                    string plant=director.PlantProgress>0f?"   PLANTING "+director.PlantProgress.ToString("F1")+"s":"";
                    return "LIVE / "+director.Clock.ToString("F0")+"s"+plant;
                case RoundPhase.PostPlant:
                    string defuse=director.DefuseProgress>0f?"   DEFUSING "+director.DefuseProgress.ToString("F1")+"s":"";
                    return "BOMB DOWN at "+director.SiteName(director.PlantedSite)+" / "+director.BombTimer.ToString("F1")+"s"+defuse;
                case RoundPhase.Ended: return "ROUND OVER / "+OutcomeText(director.Outcome);
            }
            return "ROUND";
        }
        static string OutcomeText(RoundOutcome outcome)
        {
            switch(outcome)
            {
                case RoundOutcome.BombExploded: return "bomb exploded, attackers win";
                case RoundOutcome.BombDefused: return "bomb defused, defenders win";
                case RoundOutcome.TerroristsEliminated: return "attackers eliminated, defenders win";
                case RoundOutcome.CounterTerroristsEliminated: return "defenders eliminated, attackers win";
                case RoundOutcome.TimeExpired: return "time expired, defenders win";
            }
            return "undecided";
        }
        void OnGUI()
        {
            if(error!=null) { GUI.Label(new Rect(20,20,1000,100),error); return; }
            if(Data==null)return;
            DrawBroadcastHud();
        }
        void OnDestroy()
        {
            DisposeShotPresentation();
            if(mapCamera!=null)mapCamera.targetTexture=null;
            if(eyeCamera!=null)eyeCamera.targetTexture=null;
#if UNITY_5_3_OR_NEWER
            if(equipmentCamera!=null)equipmentCamera.targetTexture=null;
#endif
            if(mapTexture!=null) { mapTexture.Release(); DestroyImmediate(mapTexture); }
            if(eyeTexture!=null) { eyeTexture.Release(); DestroyImmediate(eyeTexture); }
            foreach(var m in materials) if(m!=null) DestroyImmediate(m);
        }
    }
}
