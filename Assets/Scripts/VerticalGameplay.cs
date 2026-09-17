using System;
using UnityEngine;
namespace FpsManager
{
 public partial class Prototype
 {
  readonly ElevationMap elevation=new ElevationMap();
  sealed class ClimbState { public int link=-1,step;public bool returning;public float hold,cooldown,jumpTime;public Vector3 jumpStart; }
  readonly ClimbState[] climbers=new ClimbState[10];
  public float PlayerHeight(int i){return actors[i].transform.position.y-1;}
  public bool ElevatedMatch {get{return AutomaticMatch&&roundMode;}}
  void ElevatedBox(string name,Vector2 p,float bottom,Vector3 size,Material material,bool ground=false)
  {
   Box(name,World(p,bottom+size.y/2),size,material);
   elevation.Add(new Rect(p.x-size.x/2,p.y-size.z/2,size.x,size.z),bottom,bottom+size.y,ground);
  }
  void BuildElevation()
  {
   var stone=Material(new Color(.38f,.40f,.43f));var trim=Material(new Color(.51f,.46f,.35f));var wood=Material(new Color(.50f,.32f,.15f));
#if UNITY_5_3_OR_NEWER
   foreach(var material in new[]{stone,trim,wood}){material.shader=Shader.Find("Standard");material.SetFloat("_Glossiness",.12f);}
   var lightObject=new GameObject("Elevation shape light");lightObject.transform.SetParent(transform,false);lightObject.transform.rotation=Quaternion.Euler(55,-35,0);
   var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.cullingMask=1;light.shadows=LightShadows.None;
#endif
   ElevatedBox("A upper floor",new Vector2(89,30),2.75f,new Vector3(8,.25f,8),stone);
   ElevatedBox("A building back",new Vector2(89,25.85f),3,new Vector3(8,3,.3f),stone);
   ElevatedBox("A building side",new Vector2(93,30),3,new Vector3(.3f,3,8),stone);
   ElevatedBox("A upper roof",new Vector2(89,28),5.8f,new Vector3(8,.25f,4),stone);
   ElevatedBox("A balcony cover",new Vector2(89,34),3,new Vector3(8,.75f,.3f),trim);
   foreach(var p in new[]{new Vector2(85.3f,26.3f),new Vector2(92.7f,26.3f),new Vector2(92.7f,33.7f)})ElevatedBox("Lower floor pillar",p,0,new Vector3(.5f,2.75f,.5f),stone,true);
   // Stair surface is an explicit link, never a shortcut through the side of the stairs.
   elevation.GroundObstacles.Add(new Rect(82.8f,25.8f,2.4f,8.6f));
   for(int n=0;n<12;n++){float h=(n+1)*.25f;ElevatedBox("A stair "+n,new Vector2(84,34-(n+.5f)*7/12),0,new Vector3(2,h,7/12f),trim);}
   foreach(int link in new[]{1,2}){var p=elevation.Links[link].points[0];ElevatedBox(elevation.Links[link].name,new Vector2(p.x,p.z),0,new Vector3(2.4f,1.2f,2.4f),wood,true);}
   for(int x=39;x<51;x++)for(int y=48;y<70;y++)
   {
    var p=new Vector2(x+.5f,y+.5f);float h=ElevationMap.Ground(p);if(h>.01f)ElevatedBox("Mid raised paving",p,0,new Vector3(1,h,1),stone);
   }
  }
  void ConfigureElevation()
  {
   navigation.MovementFilter=AutomaticMatch?new Func<Vector2,Vector2,bool>(elevation.GroundClear):null;
   vision.PairGeometry=AutomaticMatch?new Func<int,int,Vector2,Vector2,bool>(HeightVisibility):null;
   combat.FeetHeight=AutomaticMatch?new Func<int,float>(PlayerHeight):null;
   combat.HeightShotClear=AutomaticMatch?new Func<int,int,float,bool>(HeightBullet):null;
   director.InteractionAllowed=AutomaticMatch?new Func<int,bool>(i=>PlayerHeight(i)<.4f):null;
   if(autonomy!=null&&AutomaticMatch)
   {
    autonomy.ThrowerHeight=PlayerHeight;
    autonomy.LandingHeight=(i,p)=>PlayerHeight(i)>2&&ElevationMap.Inside(new Rect(85,26,8,8),p)?3:ElevationMap.Ground(p);
    autonomy.HeightRay=elevation.Sight;vision.ExtraSight=null;
   }
  }
  void ResetElevation()
  {
   for(int i=0;i<10;i++)climbers[i]=new ClimbState();
   if(navigation!=null)navigation.MovementFilter=null;
   if(vision!=null)vision.PairGeometry=null;
   if(combat!=null){combat.FeetHeight=null;combat.HeightShotClear=null;}
   if(director!=null)director.InteractionAllowed=null;
  }
  bool HeightVisibility(int i,int j,Vector2 a,Vector2 b)
  {
   float eye=PlayerHeight(i)+1.65f,body=PlayerHeight(j)+1;
   if(Mathf.Abs(Mathf.Atan2(body-eye,Mathf.Max(.01f,Vector2.Distance(a,b)))/Mathf.Deg2Rad)>65)return false;
   return (elevation.Sight(a,eye,b,body)&&autonomy.ClearSight3D(a,eye,b,body))||(elevation.Sight(a,eye,b,body+.8f)&&autonomy.ClearSight3D(a,eye,b,body+.8f));
  }
  bool HeightBullet(int shooter,int target,float relativeHeight)
  {return elevation.Sight(MapPosition(shooter),PlayerHeight(shooter)+1.65f,MapPosition(target),PlayerHeight(target)+1+relativeHeight);}
  void GroundActor(int i)
  {
   if(!ElevatedMatch)return;var p=actors[i].transform.position;p.y=1+ElevationMap.Ground(MapPosition(i));actors[i].transform.position=p;
  }
  void SettleDeadHeight(int i,float dt)
  {
   if(!ElevatedMatch)return;
   float feet=PlayerHeight(i),floor=ElevationMap.Ground(MapPosition(i));
   foreach(var solid in elevation.Solids)if(solid.top<=feet+.1f&&solid.top>floor&&ElevationMap.Inside(solid.area,MapPosition(i)))floor=solid.top;
   var p=actors[i].transform.position;p.y=1+Mathf.Max(floor,feet-8*dt);actors[i].transform.position=p;
  }
  bool StepElevation(int i,PlayerObjective order,float dt)
  {
   if(!ElevatedMatch)return false;var s=climbers[i];s.cooldown=Mathf.Max(0,s.cooldown-dt);
   bool urgent=order.task==PlayerTask.PlantBomb||order.task==PlayerTask.Defuse||order.task==PlayerTask.RecoverBomb||order.task==PlayerTask.Retake||order.task==PlayerTask.FallBack||director.Carrier==i;
   if(s.link<0)
   {
    GroundActor(i);
    if(s.cooldown>0||urgent||combat.Engaging(i)||!order.valid)return false;
    bool role=Data.players[i].weaponPosition=="awper"||Data.players[i].riflerRole=="anchor_lurker"||i%3==0;
    if(!role)return false;
    float best=12;
    for(int link=0;link<elevation.Links.Length;link++)
    {
     var lane=elevation.Links[link];float distance=Vector2.Distance(MapPosition(i),lane.entry);
     bool assignedUpper=link==0&&teamIndex[i]==ctTeam&&Vector2.Distance(homeAnchor[i],new Vector2(83,33))<18;
     if(assignedUpper)distance*=.2f;
     if(distance>=best||Vector2.Distance(order.destination,lane.entry)>20)continue;
     bool reserved=false;for(int j=0;j<10;j++)if(j!=i&&teamIndex[j]==teamIndex[i]&&combat.Alive(j)&&climbers[j].link==link)reserved=true;
     if(reserved)continue;best=distance;s.link=link;
    }
    if(s.link<0)return false;s.step=-1;s.returning=false;s.hold=0;s.jumpTime=0;
   }
   var path=elevation.Links[s.link];
   if(s.step<0&&!s.returning)
   {
    if(urgent){s.link=-1;s.cooldown=6;return false;}
    MoveTo(i,path.entry);if(routeValid[i]&&routeSteps[i]<routes[i].Count)StepRoute(i,dt);
    if(Vector2.Distance(MapPosition(i),path.entry)<1.1f&&navigation.Clear(MapPosition(i),path.entry))
    {var point=Vector2.MoveTowards(MapPosition(i),path.entry,3.5f*dt);actors[i].transform.position=World(point); }
    GroundActor(i);moving[i]=true;arrived[i]=false;AimWhileMoving(i,order,dt);
    if(Vector2.Distance(MapPosition(i),path.entry)<.15f){s.step=0;s.jumpStart=actors[i].transform.position;s.jumpTime=0;}
    else if(!routeValid[i]){s.link=-1;s.cooldown=6;}
    return true;
   }
   if(s.step>=path.points.Length&&!s.returning)
   {
    moving[i]=false;arrived[i]=true;s.hold+=dt;AimWhileMoving(i,order,dt);
    if(urgent||s.hold>9||Vector2.Distance(order.destination,MapPosition(i))>23){s.returning=true;s.step=path.points.Length-2;s.jumpStart=actors[i].transform.position;s.jumpTime=0;}
    return true;
   }
   Vector3 node=s.returning&&s.step<0?new Vector3(path.entry.x,0,path.entry.y):path.points[s.step];
   Vector3 goal=World(new Vector2(node.x,node.z),node.y+1),before=actors[i].transform.position;
   if(path.jump)
   {
    s.jumpTime+=dt;float t=Mathf.Clamp01(s.jumpTime/.65f);
    // Ballistic-looking shared jump, independent of movement skill.
    var p=s.jumpStart+(goal-s.jumpStart)*t;p.y+=4*.75f*t*(1-t);actors[i].transform.position=p;
   }
   else actors[i].transform.position=Vector3.MoveTowards(before,goal,3.5f*dt);
   moving[i]=true;arrived[i]=false;AimWhileMoving(i,order,dt);
   autonomy.Footstep(i,MapPosition(i),(before-actors[i].transform.position).magnitude);
   if((actors[i].transform.position-goal).magnitude<.02f)
   {
    if(s.returning&&s.step<0){s.link=-1;s.cooldown=14;routeValid[i]=false;repathDelay[i]=0;GroundActor(i);}
    else s.step+=s.returning?-1:1;
   }
   return true;
  }
 }
}
