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
            ctMaterial=Material(new Color(.18f,.65f,1)); tMaterial=Material(new Color(1,.62f,.18f));
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
        public void PlaceTeams()
        {
            deploymentStarted=false; paused=false; elapsed=0; DeploymentComplete=false;
            routes.Clear(); Array.Clear(routeSteps,0,routeSteps.Length); preparationError=null;
            if(vision!=null) vision.Reset();
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
        public void BeginDeployment()
        {
            if(deploymentStarted) return;
            routes.Clear(); var reserved=new List<Vector2>();
            for(int i=0;i<actors.Count;i++)
            {
                bool isCt=Data.players[i].teamId==Data.teams[ctTeam].id;
                Vector3 p=actors[i].transform.position;
                routes.Add(navigation.Route(new Vector2(p.x,100-p.z),(isCt?ctZones:tZones)[assignments[i]],reserved));
            }
            Array.Clear(routeSteps,0,routeSteps.Length);
            deploymentStarted=true; paused=false; DeploymentComplete=false; preparationError=null;
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
        // Detection runs whenever the clock runs, including while players stand still
        // in preparation, so team knowledge is always in step with the shown positions.
        void UpdateVision(float delta)
        {
            if(vision==null||Data==null) return;
            for(int i=0;i<actors.Count;i++)
            {
                Vector3 p=actors[i].transform.position,f=actors[i].transform.forward;
                visionPositions[i]=new Vector2(p.x,100-p.z);
                visionFacing[i]=new Vector2(f.x,-f.z);
            }
            vision.Tick(delta,visionPositions,visionFacing,teamIndex);
        }
        public void SimulateMovement(float delta)
        {
            float tick=Mathf.Min(delta,.1f);
            if(paused) return;
            if(!deploymentStarted||DeploymentComplete) { UpdateVision(tick); return; }
            bool complete=true;
            for(int i=0;i<actors.Count;i++)
            {
                if(routeSteps[i]>=routes[i].Count) continue;
                float remaining=Mathf.Min(delta,.1f)*5f;
                while(remaining>0&&routeSteps[i]<routes[i].Count)
                {
                    Vector3 target=World(routes[i][routeSteps[i]]),position=actors[i].transform.position;
                    Vector3 direction=target-position;
                    float distance=direction.magnitude;
                    if(distance>.001f) actors[i].transform.rotation=Quaternion.RotateTowards(actors[i].transform.rotation,Quaternion.LookRotation(direction),360*Mathf.Min(delta,.1f));
                    float step=Mathf.Min(remaining,distance);
                    actors[i].transform.position=Vector3.MoveTowards(position,target,step);
                    remaining-=step;
                    if(distance<=step+.001f) routeSteps[i]++; else break;
                }
                if(routeSteps[i]<routes[i].Count) complete=false;
            }
            elapsed+=tick; DeploymentComplete=complete;
            UpdateVision(tick);
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
        void OnGUI()
        {
            if(error!=null) { GUI.Label(new Rect(20,20,1000,100),error); return; }
            if(Data==null) return;
            GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1280f,Screen.height/800f,1));
            GUI.Label(new Rect(20,12,1000,25),"FPS MANAGER / YOUR TEAM: SPIRIT / OPPONENT: FALCONS");
            GUI.Label(new Rect(20,39,1200,25),(deploymentStarted?"MOVEMENT TEST / "+elapsed.ToString("F1")+"s":"PREPARATION / Assign zones before starting")+" | Detection: line of sight only | Combat / economy: not connected");
            Rect map=new Rect(20,80,650,650); GUI.DrawTexture(map,mapTexture,ScaleMode.StretchToFill);
            int viewerTeam=AlliedTeamIndex;
            for(int i=0;i<actors.Count;i++)
            {
                bool own=teamIndex[i]==viewerTeam;
                var contact=vision.Knowledge(viewerTeam,i);
                Vector2 shown;
                if(own||!fogOfWar) shown=MapPosition(i);
                else if(contact.known) shown=contact.lastKnownPosition;
                else continue;   // no contact: the marker is not drawn at all
                var p=mapCamera.WorldToViewportPoint(World(shown));
                string label=own||!fogOfWar||contact.visible?Data.players[i].handle:Data.players[i].handle+"?";
                if(GUI.Button(new Rect(map.x+p.x*map.width-31,map.y+(1-p.y)*map.height-11,62,22),label)) Select(i);
            }
            GUI.Label(new Rect(683,76,570,24),"SELECTED PLAYER / FIRST PERSON");
            GUI.DrawTexture(new Rect(685,104,570,321),eyeTexture,ScaleMode.StretchToFill);
            GUI.Label(new Rect(964,252,20,25),"+");
            var player=Data.players[selected]; var s=player.stats;
            GUI.Label(new Rect(685,433,570,25),player.handle+" / "+player.weaponPosition+" "+player.riflerRole);
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
                    int index=i; string name=Data.players[i].handle+(Data.teams[team].iglPlayerId==Data.players[i].id?" [IGL]":"");
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
            if(!deploymentStarted)
            {
                if(GUI.Button(new Rect(20,741,180,32),"Start movement test"))
                    try { BeginDeployment(); } catch(Exception ex) { preparationError=ex.Message; Debug.LogException(ex); }
                if(GUI.Button(new Rect(210,741,180,32),"Swap starting sides")) SwapPreviewSides();
            }
            else
            {
                if(GUI.Button(new Rect(20,741,180,32),paused?"Resume":"Pause")) paused=!paused;
                if(GUI.Button(new Rect(210,741,180,32),"Reset to preparation")) PlaceTeams();
            }
            if(GUI.Button(new Rect(400,741,180,32),fogOfWar?"Fog of war: ON":"Fog of war: OFF")) fogOfWar=!fogOfWar;
            if(preparationError!=null) GUI.Label(new Rect(590,741,670,28),preparationError);
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
