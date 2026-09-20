using System;using System.Collections.Generic;using UnityEngine;
namespace FpsManager {
 public partial class Prototype {
  SourceArena sourceArena;float sourceNavHeight;string loadedMapId="legacy_inferno";bool quickSeriesSetup;
#if UNITY_5_3_OR_NEWER
  GameObject[] sourceRadar;
#endif
  bool sourceLowerFloor;
  bool HasSourceFloors {get{return sourceArena!=null&&(sourceArena.Id=="de_nuke"||sourceArena.Id=="de_vertigo");}}
  float SourceFloorSplit {get{return sourceArena==null?0:(sourceArena.Id=="de_nuke"?(sourceArena.Sites[0].y+sourceArena.Sites[1].y)/2:(sourceArena.T.y+sourceArena.CT.y)/2);}}

  readonly List<GameObject> arenaObjects=new List<GameObject>();
  readonly float[] sourceFeet=new float[10];readonly List<Vector3>[] sourceRoutes=new List<Vector3>[10];
  readonly Dictionary<Vector2,float> sourceAnchors=new Dictionary<Vector2,float>();
  public bool SourceMapActive {get{return sourceArena!=null;}}
  float MapFloor(Vector2 p,float hint=0){return sourceArena==null?ElevationMap.Ground(p):sourceArena.Floor(p,hint);}
  float SiteFloor(int site){return sourceArena==null?0:sourceArena.Sites[Math.Max(0,Math.Min(1,site))].y;}
  float BombFloor(){return SiteFloor(director.PlantedSite>=0?director.PlantedSite:director.TargetSite);}
  Vector2 Anchor(Vector3 p){p=sourceArena.Snap(p);var flat=SourceArena.Flat(p);sourceAnchors[flat]=p.y;return flat;}
  float GoalFloor(Vector2 p,float hint){float floor;if(sourceAnchors.TryGetValue(p,out floor))return floor;return MapFloor(p,hint);}
  void ActivateMatchMap(string id){
   if(!MatchMaps.Known(id))throw new ArgumentException("Unknown match map");
   ActiveMapId=id;if(FastForwarding||quickSeriesSetup)return;if(loadedMapId==id){SetSourceRadar(sourceLowerFloor);return;}loadedMapId=id;sourceLowerFloor=false;
   sourceArena=SourceArena.Load(id);ActiveMapId=id;
   foreach(var go in arenaObjects){
#if UNITY_5_3_OR_NEWER
    go.SetActive(false);if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);
#endif
   }arenaObjects.Clear();obstacles.Clear();elevation.Solids.Clear();elevation.GroundObstacles.Clear();sourceAnchors.Clear();
   if(sourceArena==null){
#if UNITY_5_3_OR_NEWER
    sourceRadar=null;mapCamera.cullingMask=~(1<<9);eyeCamera.cullingMask|=1<<10;
#endif
    var c=new[]{new Vector2(50,9),new Vector2(53,9),new Vector2(56,9),new Vector2(51.5f,12),new Vector2(54.5f,12)};var t=new[]{new Vector2(26,88),new Vector2(30,88),new Vector2(34,88),new Vector2(28,91),new Vector2(32,91)};
    Array.Copy(c,ctPositions,5);Array.Copy(t,tPositions,5);ctZones[0]=new Vector2(18,22);ctZones[1]=new Vector2(65,38);ctZones[2]=new Vector2(83,33);tZones[0]=new Vector2(27,55);tZones[1]=new Vector2(45,61);tZones[2]=new Vector2(89,63);
    BuildMap();BuildElevation();navigation=new DeploymentNavigation(obstacles);layout=BuildLayout();
   }else{
    navigation=new DeploymentNavigation(new List<Rect>());
    navigation.ClearOverride=(a,b)=>sourceArena.WalkClear(SourceArena.At(a,MapFloor(a,sourceNavHeight)),SourceArena.At(b,MapFloor(b,sourceNavHeight)));
    navigation.SightOverride=(a,b)=>sourceArena.Sight(SourceArena.At(a,MapFloor(a,sourceNavHeight)+1.65f),SourceArena.At(b,MapFloor(b,sourceNavHeight)+1.65f));
    navigation.RouteOverride=(a,b)=>{var route=sourceArena.Route(SourceArena.At(a,MapFloor(a,sourceNavHeight)),SourceArena.At(b,GoalFloor(b,sourceNavHeight)));var flat=new List<Vector2>();foreach(var p in route)flat.Add(SourceArena.Flat(p));return flat;};
    navigation.AimOverride=SourceAimPoint;
    BuildSourceLayout();BuildSourceVisuals();
   }
   vision=new VisionSystem(navigation,visionSettings,10);director=new RoundDirector(roundSettings,layout,10);
   float span=sourceArena==null?106:sourceArena.Span+8;mapCamera.transform.position=new Vector3(50,120,50);mapCamera.orthographicSize=span/2;
#if UNITY_5_3_OR_NEWER
   mapCamera.farClipPlane=3000;eyeCamera.farClipPlane=1000;
#endif
  }
  void ResumeWatchGeometry(){
   if(loadedMapId==ActiveMapId||Stage==MatchStage.Finished)return;
   var stage=Stage;ActivateMatchMap(ActiveMapId);PlaceTeams();ShuffleSpawnPlayers();PrepareIglOrders();
   if(stage==MatchStage.Live)BeginRound();
   else if(stage==MatchStage.Result)director.FinishStatRound(LastRoundOutcome);
   Stage=stage;
  }
  void BuildSourceLayout(){
   for(int i=0;i<5;i++){var offset=new Vector3((i%3-1)*1.2f,0,(i/3)*1.4f);ctPositions[i]=Anchor(sourceArena.CT+offset);tPositions[i]=Anchor(sourceArena.T+offset);}
   SpaceSpawnSlots(ctPositions,sourceArena.CT);SpaceSpawnSlots(tPositions,sourceArena.T);
   layout=new MapLayout{Sites=new[]{Anchor(sourceArena.Sites[0]),Anchor(sourceArena.Sites[1])},SiteNames=new[]{"A","B"},AttackerSpawn=Anchor(sourceArena.T),Mid=Anchor(sourceArena.Mid)};
   layout.Staging=new Vector2[2];layout.HoldRing=new Vector2[2][];layout.Approaches=new Vector2[2][];layout.PeekPost=new Vector2[2][];layout.CoverPost=new Vector2[2][];
   layout.ForwardWatch=new Vector2[2][];
   for(int site=0;site<2;site++){
    var at=sourceArena.Sites[site];var ctPath=sourceArena.Route(at,sourceArena.CT);var tPath=sourceArena.Route(at,sourceArena.T);
    layout.Staging[site]=Anchor(PointAlong(ctPath,10));layout.Approaches[site]=new[]{Anchor(PointAlong(tPath,9)),Anchor(PointAlong(sourceArena.Route(at,sourceArena.Mid),9))};
    layout.ForwardWatch[site]=new[]{Anchor(PointAlong(tPath,21)),Anchor(PointAlong(sourceArena.Route(at,sourceArena.Mid),21))};
    for(int n=0;n<2;n++)if(Vector2.Distance(layout.ForwardWatch[site][n],layout.Approaches[site][n])<4){var mouth=SourceArena.At(layout.Approaches[site][n],GoalFloor(layout.Approaches[site][n],at.y));layout.ForwardWatch[site][n]=Anchor(PointAlong(sourceArena.Route(mouth,sourceArena.T),12));}
    layout.HoldRing[site]=new Vector2[5];for(int i=0;i<5;i++){float angle=i*6.28318f/5;layout.HoldRing[site][i]=Anchor(at+new Vector3(Mathf.Cos(angle)*3,0,Mathf.Sin(angle)*3));}
    layout.PeekPost[site]=new[]{layout.HoldRing[site][0],layout.HoldRing[site][2]};layout.CoverPost[site]=new[]{layout.Staging[site],layout.HoldRing[site][4]};
   }
   layout.MidForwardWatch=Anchor(PointAlong(sourceArena.Route(sourceArena.Mid,sourceArena.T),12));
   BuildMidEntries();BuildStackCover();
   ctZones[0]=layout.Sites[1];ctZones[1]=layout.Mid;ctZones[2]=layout.Sites[0];tZones[0]=layout.Approaches[1][0];tZones[1]=layout.Mid;tZones[2]=layout.Approaches[0][0];
   layout.FlankRoutes=new[]{new[]{new[]{layout.Approaches[1][0],layout.Mid,layout.Staging[0]},new[]{layout.Mid,layout.Approaches[0][1]}},new[]{new[]{layout.Approaches[0][0],layout.Mid,layout.Staging[1]},new[]{layout.Mid,layout.Approaches[1][1]}}};
  }
  void SpaceSpawnSlots(Vector2[] slots,Vector3 center){
   for(int i=0;i<slots.Length;i++){
    bool clear=true;for(int j=0;j<i;j++)if(Vector2.Distance(slots[i],slots[j])<1.1f)clear=false;if(clear)continue;
    bool found=false;for(int ring=1;ring<=10&&!found;ring++)for(int n=0;n<24&&!found;n++){
     float angle=n*Mathf.PI/12;var p=sourceArena.Snap(center+new Vector3(Mathf.Cos(angle)*ring*.65f,0,Mathf.Sin(angle)*ring*.65f));var flat=SourceArena.Flat(p);
     if(Mathf.Abs(p.y-center.y)>.6f||!sourceArena.Sight(center+Vector3.up,p+Vector3.up))continue;clear=true;
     for(int j=0;j<i;j++)if(Vector2.Distance(flat,slots[j])<1.1f)clear=false;
     if(clear){slots[i]=Anchor(p);found=true;}
    }
    if(!found)throw new InvalidOperationException("No separated spawn slots: "+ActiveMapId);
   }
  }
  static Vector3 PointAlong(List<Vector3> route,float distance){var last=route[0];foreach(var p in route){float d=(p-last).magnitude;if(d>=distance)return Vector3.MoveTowards(last,p,distance);distance-=d;last=p;}return last;}
  void BuildSourceVisuals(){
#if UNITY_5_3_OR_NEWER
   var go=new GameObject(ActiveMapName+" / public geometry");go.transform.SetParent(transform,false);arenaObjects.Add(go);
   var mesh=new Mesh{name=ActiveMapId,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=sourceArena.Mesh.vertices;mesh.triangles=sourceArena.Mesh.triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
   go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=new Material(Shader.Find("FpsManager/SourceMapPreview")){color=new Color(.56f,.61f,.66f)};
   go.AddComponent<MeshCollider>().sharedMesh=mesh;
   sourceRadar=new GameObject[2];
   for(int level=0;level<2;level++){
    var vertices=new List<Vector3>();var indices=new List<int>();
    foreach(var a in sourceArena.Areas.Values){if(a.hull!=0||(HasSourceFloors&&(a.Center.y<SourceFloorSplit)!=(level==1))||(!HasSourceFloors&&level==1))continue;
     int start=vertices.Count;foreach(var p in a.corners)vertices.Add(new Vector3(p.x,0,p.z));for(int n=1;n<a.corners.Length-1;n++){indices.Add(start);indices.Add(start+n);indices.Add(start+n+1);}}
    var navMesh=new Mesh{name="Radar floor",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};navMesh.vertices=vertices.ToArray();navMesh.triangles=indices.ToArray();navMesh.RecalculateNormals();navMesh.RecalculateBounds();
    var radar=new GameObject("Radar "+level);radar.layer=14;radar.transform.SetParent(transform,false);radar.AddComponent<MeshFilter>().sharedMesh=navMesh;radar.AddComponent<MeshRenderer>().sharedMaterial=new Material(Shader.Find("FpsManager/SourceRadar")){color=new Color(.4f,.5f,.58f)};sourceRadar[level]=radar;arenaObjects.Add(radar);
   }
   mapCamera.cullingMask=1<<14;eyeCamera.cullingMask&=~(1<<14);SetSourceRadar(false);
#endif
  }
  void SetSourceRadar(bool lower){sourceLowerFloor=HasSourceFloors&&lower;
#if UNITY_5_3_OR_NEWER
   if(sourceRadar!=null)for(int i=0;i<sourceRadar.Length;i++){
    var radar=sourceRadar[i];if(radar==null)continue;
    radar.SetActive(HasSourceFloors||i==0);
    bool selectedFloor=i==(sourceLowerFloor?1:0);var material=radar.GetComponent<MeshRenderer>().sharedMaterial;
    material.color=selectedFloor?new Color(.4f,.54f,.64f,1):new Color(.48f,.61f,.69f,.24f);
    // Draw the ghost floor first. The selected lower floor must not be hidden by the upper one.
    material.renderQueue=selectedFloor?3001:3000;
   }
#endif
  }
  float RadarOpacity(float height){return !HasSourceFloors||(height<SourceFloorSplit)==sourceLowerFloor?1:.32f;}
  void DrawSourceFloorControl(Rect map){
   if(!HasSourceFloors)return;
   float y=map.y+map.height-31;
   if(HudButton(new Rect(map.x+6,y,70,25),"1F",true,sourceLowerFloor))SetSourceRadar(true);
   if(HudButton(new Rect(map.x+80,y,70,25),"2F",true,!sourceLowerFloor))SetSourceRadar(false);
  }
  Vector2 SourceAimPoint(Vector2 position,Vector2 intended){
   var start=SourceArena.At(position,MapFloor(position,sourceNavHeight));Vector2 best=position;float score=float.MinValue;
   var forward=(intended-position).normalized;
   try{var route=sourceArena.Route(start,SourceArena.At(intended,GoalFloor(intended,sourceNavHeight)));
    // NAV starts at the area's centre, which can be behind the player or
    // occluded. It is a movement guide, not a reason to stop looking ahead.
    foreach(var point in route){var flat=SourceArena.Flat(point);var delta=flat-position;float distance=delta.magnitude;
     if(distance<2||distance>20)continue;float alignment=Vector2.Dot(delta.normalized,forward);
     float value=alignment*20+Mathf.Min(distance,12)*.2f;
     if(alignment<=0||value<=score||!navigation.SightClear(position,flat))continue;best=flat;score=value;
    }
   }catch(InvalidOperationException){}
   if(best!=position)return best;
   // At a tight bend, inspect a clear ray near the intended corridor instead
   // of returning our own feet (which preserves an arbitrary old facing).
   foreach(float angle in new[]{0f,30f,-30f,60f,-60f,80f,-80f}){
    float radians=angle*Mathf.Deg2Rad;var direction=new Vector2(forward.x*Mathf.Cos(radians)-forward.y*Mathf.Sin(radians),forward.x*Mathf.Sin(radians)+forward.y*Mathf.Cos(radians));
    for(float distance=12;distance>=2;distance-=1){var candidate=position+direction*distance;
     float value=Vector2.Dot(direction,forward)*20+distance*.2f;
     if(value<=score||!navigation.SightClear(position,candidate))continue;score=value;best=candidate;
    }
   }
   return best;
  }
  void SourceSpawn(int i){if(sourceArena==null)return;sourceFeet[i]=GoalFloor(MapPosition(i),teamIndex[i]==ctTeam?sourceArena.CT.y:sourceArena.T.y);var p=actors[i].transform.position;p.y=sourceFeet[i]+1;actors[i].transform.position=p;}
  void SourceMoveTo(int i,Vector2 destination){
   if(routeValid[i]&&(destination-destinations[i]).sqrMagnitude<1){
    bool finished=sourceRoutes[i]!=null&&routeSteps[i]>=sourceRoutes[i].Count;
    if(!finished||Vector2.Distance(MapPosition(i),destination)<.5f)return;
    routeValid[i]=false; // A final movement rejected by body collision did not reach its goal.
   }
   if(repathDelay[i]>0)return;repathDelay[i]=.5f;destinations[i]=destination;
   try{sourceRoutes[i]=sourceArena.Route(actors[i].transform.position-Vector3.up,SourceArena.At(destination,GoalFloor(destination,sourceFeet[i])));routes[i]=new List<Vector2>();foreach(var p in sourceRoutes[i])routes[i].Add(SourceArena.Flat(p));routeSteps[i]=0;routeValid[i]=true;}
   catch(InvalidOperationException){routeValid[i]=false;}
  }
  void SourceStepRoute(int i,float dt){
   if(sourceRoutes[i]==null)return;
   // A crowded narrow portal must not require touching its exact centre.
   // Only skip a nearby guide when the next NAV segment is visible through the opening.
   if(AutomaticMatch)while(routeSteps[i]+1<sourceRoutes[i].Count){
    var feet=actors[i].transform.position-Vector3.up;var guide=sourceRoutes[i][routeSteps[i]];
    if((feet-guide).magnitude>PlayerCollision.Diameter*.55f||!sourceArena.Sight(feet+Vector3.up*.8f,sourceRoutes[i][routeSteps[i]+1]+Vector3.up*.8f))break;
    routeSteps[i]++;
   }
   if(AutomaticMatch&&PrepareSourceTraversal(i,dt))return;
   // NAV area centers are guides, not mandatory one-person stopping points.
   // At render-frame timesteps a queue cannot reach a center occupied by a teammate.
   if(AutomaticMatch)for(int look=0;look<4&&routeSteps[i]+1<sourceRoutes[i].Count;look++){
    var p=actors[i].transform.position-Vector3.up;var waypoint=sourceRoutes[i][routeSteps[i]];bool occupied=false;
    for(int j=0;j<actors.Count;j++)if(j!=i&&combat.Alive(j)&&Mathf.Abs(PlayerHeight(j)-waypoint.y)<1.8f&&Vector2.Distance(MapPosition(j),SourceArena.Flat(waypoint))<PlayerCollision.Diameter+.15f){occupied=true;break;}
    if(!occupied||!sourceArena.WalkClear(p,sourceRoutes[i][routeSteps[i]+1]))break;routeSteps[i]++;
   }
   var before=MapPosition(i);float desired=IsCrouched(i)?1.7f:autonomy!=null&&autonomy.Walking[i]?2.4f:combat.KnifeOut(i)?5.75f:5;moveSpeed[i]+=Mathf.Clamp(desired-moveSpeed[i],-dt*24,dt*24);float remaining=moveSpeed[i]*dt;
   while(remaining>0&&routeSteps[i]<sourceRoutes[i].Count){var target=sourceRoutes[i][routeSteps[i]]+Vector3.up;var p=actors[i].transform.position;float distance=(target-p).magnitude;float step=Math.Min(remaining,distance);actors[i].transform.position=Vector3.MoveTowards(p,target,step);sourceFeet[i]=PlayerHeight(i);remaining-=step;if(distance<=step+.001f)routeSteps[i]++;else break;}
   if(autonomy!=null)autonomy.Footstep(i,MapPosition(i),Vector2.Distance(before,MapPosition(i)));
  }
  void SourceGroundActor(int i){var p=sourceArena.Snap(SourceArena.At(MapPosition(i),sourceFeet[i]));sourceFeet[i]=p.y;actors[i].transform.position=p+Vector3.up;}
  void ConfigureSourceElevation(){
   vision.PairGeometry=(i,j,a,b)=>(sourceArena.Sight(SourceArena.At(a,StanceHeight(i)+1.65f),SourceArena.At(b,StanceHeight(j)+1.7f))||sourceArena.Sight(SourceArena.At(a,StanceHeight(i)+1.65f),SourceArena.At(b,StanceHeight(j)+1)))&&autonomy.ClearSight3D(a,StanceHeight(i)+1.65f,b,StanceHeight(j)+1);
   combat.FeetHeight=StanceHeight;combat.HeightShotClear=(i,j,h)=>sourceArena.Sight(SourceArena.At(MapPosition(i),StanceHeight(i)+1.65f),SourceArena.At(MapPosition(j),StanceHeight(j)+1+h));
   director.InteractionAllowed=i=>Math.Abs(PlayerHeight(i)-BombFloor())<1;
   autonomy.Sounds.SourceHeight=i=>i<0?BombFloor():PlayerHeight(i);
   autonomy.ThrowerHeight=PlayerHeight;autonomy.LandingHeight=(i,p)=>MapFloor(p,PlayerHeight(i));autonomy.HeightRay=(a,ay,b,by)=>sourceArena.Sight(SourceArena.At(a,ay),SourceArena.At(b,by));vision.ExtraSight=null;
   elevation.SightOverride=(a,ay,b,by)=>sourceArena.Sight(SourceArena.At(a,ay),SourceArena.At(b,by));
  }
 }
}
