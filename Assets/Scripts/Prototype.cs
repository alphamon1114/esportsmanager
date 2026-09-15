using System;
using System.Collections.Generic;
using UnityEngine;

namespace FpsManager
{
    public class Prototype : MonoBehaviour
    {
        public Database Data { get; private set; }
        readonly List<GameObject> actors = new List<GameObject>();
        readonly List<Material> materials = new List<Material>();
        Camera mapCamera, eyeCamera;
        RenderTexture mapTexture, eyeTexture;
        const string AlliedTeamId = "spirit";
        int selected, ctTeam;
        readonly List<Rect> obstacles = new List<Rect>();
        DeploymentNavigation navigation;
        VisionSystem vision;
        readonly VisionSettings visionSettings = new VisionSettings();
        readonly int[] teamIndex = new int[10];
        readonly Vector2[] visionPositions = new Vector2[10];
        readonly Vector2[] visionFacing = new Vector2[10];
        bool fogOfWar = true;
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
        readonly RoundSettings roundSettings = new RoundSettings();
        MapLayout layout;
        bool roundMode;
        readonly Vector2[] homeAnchor = new Vector2[10];
        readonly Vector2[] destinations = new Vector2[10];
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
            Data = JsonUtility.FromJson<Database>(asset.text);
            if (Data.teams.Length != 2 || Data.players.Length != 10) throw new InvalidOperationException("Expected two teams and ten players.");
            BuildMap();
            navigation = new DeploymentNavigation(obstacles);
            for (int i = 0; i < Data.players.Length; i++) teamIndex[i] = Data.players[i].teamId == Data.teams[0].id ? 0 : 1;
            vision = new VisionSystem(navigation, visionSettings, Data.players.Length);
            combat = new CombatSystem(combatSettings, weapon, Data.players.Length);
            for (int i = 0; i < Data.players.Length; i++)
            {
                aimStats[i] = Data.players[i].stats.aim;
                composureStats[i] = Data.players[i].stats.composure;
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
            mapCamera.orthographicSize = 53;
            eyeCamera = NewCamera("Selected player camera", eyeTexture);
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
        void Start() { try { Initialize(); } catch(Exception ex) { error=ex.Message; Debug.LogException(ex); } }
        Material Material(Color color)
        {
            var m = new Material(Shader.Find("Unlit/Color")); m.color=color; materials.Add(m); return m;
        }
        GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            if(name=="Wall"||name=="Cover") obstacles.Add(new Rect(position.x-scale.x/2,100-position.z-scale.z/2,scale.x,scale.z));
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name;
            obj.transform.SetParent(transform); obj.transform.position=position; obj.transform.localScale=scale;
            obj.GetComponent<Renderer>().sharedMaterial=material; return obj;
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
            result.Staging = new Vector2[sites.Length];
            result.HoldRing = new Vector2[sites.Length][];
            var spawn = new Vector2(0,0);
            foreach(var p in ctPositions) spawn += p;
            spawn /= ctPositions.Length;
            for(int i=0;i<sites.Length;i++)
            {
                result.Staging[i] = BackAlongApproach(spawn, sites[i], 16f);
                result.HoldRing[i] = Ring(sites[i], roundSettings.holdRadius, 5);
            }
            return result;
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
            if(routeValid[player]&&(destination-destinations[player]).sqrMagnitude<1f) return;
            if(repathDelay[player]>0f) return;
            destinations[player]=destination; repathDelay[player]=.5f;
            try
            {
                routes[player]=navigation.Route(MapPosition(player),destination,new List<Vector2>());
                routeSteps[player]=0; routeValid[player]=true;
            }
            catch(InvalidOperationException) { routeValid[player]=false; }
        }
        void StepRoute(int player, float tick)
        {
            float remaining=tick*5f;
            while(remaining>0&&routeSteps[player]<routes[player].Count)
            {
                Vector3 target=World(routes[player][routeSteps[player]]),position=actors[player].transform.position;
                Vector3 direction=target-position;
                float distance=direction.magnitude;
                if(distance>.001f) actors[player].transform.rotation=Quaternion.RotateTowards(actors[player].transform.rotation,Quaternion.LookRotation(direction),360*tick);
                float step=Mathf.Min(remaining,distance);
                actors[player].transform.position=Vector3.MoveTowards(position,target,step);
                remaining-=step;
                if(distance<=step+.001f) routeSteps[player]++; else break;
            }
        }
        void FaceWatch(int player, Vector2 watch, float tick)
        {
            if(watch.sqrMagnitude<.0001f) return;
            var target=Quaternion.LookRotation(new Vector3(watch.x,0,-watch.y));
            actors[player].transform.rotation=Quaternion.RotateTowards(actors[player].transform.rotation,target,180*tick);
        }
        public void PlaceTeams()
        {
            deploymentStarted=false; paused=false; elapsed=0; DeploymentComplete=false; roundMode=false;
            ClearRoutes(); preparationError=null;
            if(vision!=null) vision.Reset();
            if(combat!=null) combat.Reset(roundSeed);
            if(director!=null) director.Reset();
            for(int i=0;i<alive.Length;i++) { alive[i]=true; arrived[i]=false; }
            int ct=0,t=0;
            for(int i=0;i<actors.Count;i++)
            {
                bool isCt=Data.players[i].teamId==Data.teams[ctTeam].id;
                var p=isCt?ctPositions[ct++]:tPositions[t++];
                actors[i].transform.position=World(p);
                var target=isCt?new Vector2(45,76):new Vector2(45,50);
                actors[i].transform.LookAt(World(target));
                actors[i].GetComponent<Renderer>().sharedMaterial=isCt?ctMaterial:tMaterial;
            }
            Select(selected);
        }
        public void Select(int index)
        {
            if(index<0||index>=actors.Count) return;
            selected=index;
            for(int i=0;i<actors.Count;i++) actors[i].layer=i==selected?8:0;
            eyeCamera.transform.position=actors[index].transform.position+Vector3.up*.65f;
            eyeCamera.transform.rotation=actors[index].transform.rotation;
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
        public void BeginRound()
        {
            if(deploymentStarted) return;
            for(int i=0;i<actors.Count;i++)
            {
                homeAnchor[i]=(teamIndex[i]==ctTeam?ctZones:tZones)[assignments[i]];
                destinations[i]=homeAnchor[i];
            }
            ClearRoutes();
            director.Begin(roundSeed,teamIndex,ctTeam);
            deploymentStarted=true; roundMode=true; paused=false; DeploymentComplete=false; preparationError=null;
            // Everyone may shoot from the first tick; the stand-still-to-engage rule is a
            // movement-test simplification, not a round rule.
            for(int i=0;i<arrived.Length;i++) arrived[i]=true;
        }
        void Update() { if(Data!=null&&error==null) SimulateMovement(Time.deltaTime); }
        void LateUpdate() { if(eyeCamera!=null&&actors.Count>selected) Select(selected); }
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
                if(!combat.Engaging(i)||moving[i]) continue;
                // Combat steers a shooter who is standing still. A player on the move keeps
                // the facing their movement gave them, so they can fire at what is ahead of
                // them but not at something behind: no turning to shoot while running.
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
            if(director.Phase==RoundPhase.Ended) { UpdateSenses(tick); return; }
            for(int i=0;i<actors.Count;i++)
            {
                repathDelay[i]=Mathf.Max(0f,repathDelay[i]-tick);
                if(!combat.Alive(i)) { routeValid[i]=false; continue; }
                moving[i]=false;
                var objective=director.Objective(i);
                // A player with something to shoot at stops and fights. Moving accuracy is
                // not modelled yet, so standing still is the honest simplification.
                // A player breaking off ignores that and keeps moving.
                if(!objective.disengage&&combat.Engaging(i)) continue;
                if(objective.valid) MoveTo(i,objective.destination);
                if(routeValid[i]&&routeSteps[i]<routes[i].Count) { StepRoute(i,tick); moving[i]=true; }
                else if(objective.valid) FaceWatch(i,objective.watch,tick);
            }
            elapsed+=tick;
            UpdateSenses(tick);
            director.Tick(tick,visionPositions,teamIndex,ctTeam,homeAnchor,composureStats,vision,combat);
        }
        public void ValidateMovementGeometry()
        {
            foreach(var actor in actors)
            {
                Vector3 p=actor.transform.position; var point=new Vector2(p.x,100-p.z);
                if(!navigation.Clear(point,point)) throw new InvalidOperationException("Player intersects geometry: "+actor.name);
            }
        }
        public void SwapPreviewSides() { if(!deploymentStarted) { ctTeam=1-ctTeam; PlaceTeams(); } }
        string RoundLine()
        {
            if(!deploymentStarted) return "PREPARATION / Assign zones before starting";
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
            if(Data==null) return;
            GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1280f,Screen.height/800f,1));
            GUI.Label(new Rect(20,12,1000,25),"FPS MANAGER / YOUR TEAM: SPIRIT / OPPONENT: FALCONS");
            GUI.Label(new Rect(20,39,1240,25),RoundLine()
                +"   ALIVE "+Data.teams[AlliedTeamIndex].name+" "+LivingCount(AlliedTeamIndex)+" : "+LivingCount(1-AlliedTeamIndex)+" "+Data.teams[1-AlliedTeamIndex].name
                +"   SEED "+roundSeed+" | Rifle only, no economy or scoring across rounds");
            Rect map=new Rect(20,80,650,650); GUI.DrawTexture(map,mapTexture,ScaleMode.StretchToFill);
            int viewerTeam=AlliedTeamIndex;
            for(int i=0;i<actors.Count;i++)
            {
                bool own=teamIndex[i]==viewerTeam,living=combat.Alive(i);
                var contact=vision.Knowledge(viewerTeam,i);
                Vector2 shown;
                if(own||!fogOfWar) shown=MapPosition(i);
                else if(!living) continue;              // a dead opponent is not tracked on the map
                else if(contact.known) shown=contact.lastKnownPosition;
                else continue;                          // no contact: the marker is not drawn at all
                var p=mapCamera.WorldToViewportPoint(World(shown));
                string label=!living?"x "+Data.players[i].handle
                    :own||!fogOfWar||contact.visible?Data.players[i].handle:Data.players[i].handle+"?";
                if(GUI.Button(new Rect(map.x+p.x*map.width-31,map.y+(1-p.y)*map.height-11,62,22),label)) Select(i);
            }
            GUI.Label(new Rect(683,76,570,24),"SELECTED PLAYER / FIRST PERSON");
            GUI.DrawTexture(new Rect(685,104,570,321),eyeTexture,ScaleMode.StretchToFill);
            GUI.Label(new Rect(964,252,20,25),"+");
            var player=Data.players[selected]; var s=player.stats;
            GUI.Label(new Rect(685,433,570,25),player.handle+" / "+player.weaponPosition+" "+player.riflerRole
                +"   HP "+(combat.Alive(selected)?Mathf.RoundToInt(combat.Health(selected)).ToString():"0 (down)"));
            GUI.Label(new Rect(685,459,570,25),$"AIM {s.aim}   UTIL {s.utility}   MOVE {s.movement}   CHA {s.charisma}   COMP {s.composure}");
            scroll=GUI.BeginScrollView(new Rect(685,490,570,42),scroll,new Rect(0,0,530,player.weapons.Length*21));
            for(int i=0;i<player.weapons.Length;i++) GUI.Label(new Rect(0,i*21,520,21),player.weapons[i].weapon+"   "+new string('*',player.weapons[i].stars));
            GUI.EndScrollView();
            bool selectedCt=player.teamId==Data.teams[ctTeam].id;
            string[] zoneNames=selectedCt?CtZoneNames:TZoneNames;
            if(!deploymentStarted && IsAlliedPlayer(selected))
            {
                GUI.Label(new Rect(685,537,570,22),"Initial zone / "+player.handle);
                for(int z=0;z<3;z++) if(GUI.Button(new Rect(685+z*190,560,185,25),(assignments[selected]==z?"[X] ":"")+zoneNames[z])) AssignZone(selected,z);
            }
            for(int team=0;team<2;team++)
            {
                                int[] counts=new int[3]; for(int j=0;j<10;j++) if(Data.players[j].teamId==Data.teams[team].id) counts[assignments[j]]++;
                int n=0; GUI.Label(new Rect(685,590+team*62,570,23),Data.teams[team].name+(team==ctTeam?" / CT":" / T")+(!deploymentStarted && Data.teams[team].id==AlliedTeamId?"   B / Mid / A: "+counts[0]+" / "+counts[1]+" / "+counts[2]:""));
                for(int i=0;i<Data.players.Length;i++) if(Data.players[i].teamId==Data.teams[team].id)
                {
                    int index=i; string name=(combat.Alive(i)?"":"x ")+Data.players[i].handle+(Data.teams[team].iglPlayerId==Data.players[i].id?" [IGL]":"");
                    if(GUI.Button(new Rect(685+n++*114,614+team*62,110,27),name)) Select(index);
                }
            }
            string contacts="";
            for(int i=0;i<Data.players.Length;i++)
            {
                if(teamIndex[i]==viewerTeam) continue;
                var contact=vision.Knowledge(viewerTeam,i);
                if(!contact.known) continue;
                contacts+=(contacts.Length>0?"  ":"")+Data.players[i].handle+(contact.visible?"*":" "+contact.age.ToString("F1")+"s");
            }
            GUI.Label(new Rect(685,708,570,22),"SPIRIT CONTACTS / "+(contacts.Length>0?contacts:"none")+"   (* = seen now)");
            if(roundMode && showDebugInfo)
            {
                var objective=director.Objective(selected);
                string bomb=director.PlantedSite>=0?"planted "+director.SiteName(director.PlantedSite)
                    :director.Carrier>=0?"carried by "+Data.players[director.Carrier].handle
                    :director.BombDropped?"dropped":"-";
                GUI.Label(new Rect(685,730,570,22),"BOMB "+bomb+"   TARGET "+director.SiteName(director.TargetSite)
                    +"   "+Data.players[selected].handle+": "+(combat.Alive(selected)?objective.task.ToString():"down"));
            }
            if(!deploymentStarted)
            {
                if(GUI.Button(new Rect(20,741,180,32),"Start round"))
                    try { BeginRound(); } catch(Exception ex) { preparationError=ex.Message; Debug.LogException(ex); }
                if(GUI.Button(new Rect(210,741,180,32),"Swap starting sides")) SwapPreviewSides();
            }
            else
            {
                if(GUI.Button(new Rect(20,741,180,32),paused?"Resume":"Pause")) paused=!paused;
                // Reset advances the seed so repeated runs explore different shots.
                if(GUI.Button(new Rect(210,741,180,32),"Reset / next seed")) { roundSeed++; PlaceTeams(); }
            }
            if(GUI.Button(new Rect(400,741,180,32),fogOfWar?"Fog of war: ON":"Fog of war: OFF")) fogOfWar=!fogOfWar;
            if(GUI.Button(new Rect(590,741,90,32),showDebugInfo?"Debug: ON":"Debug: OFF")) showDebugInfo=!showDebugInfo;
            if(preparationError!=null) GUI.Label(new Rect(685,751,570,25),preparationError);
            GUI.Label(new Rect(20,776,1240,24),"Blue: CT | Orange: T | Click a name to change POV | Map hides Falcons until Spirit spots them; '?' marks a remembered position");
        }
        void OnDestroy()
        {
            if(mapTexture!=null) { mapTexture.Release(); DestroyImmediate(mapTexture); }
            if(eyeTexture!=null) { eyeTexture.Release(); DestroyImmediate(eyeTexture); }
            foreach(var m in materials) if(m!=null) DestroyImmediate(m);
        }
    }
}
