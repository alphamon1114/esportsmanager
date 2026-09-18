using System;
using System.Collections.Generic;
using UnityEngine;
namespace FpsManager
{
 // Deliberate angle checks between fights, not target switching during a fight.
 public sealed class ClutchSearch
 {
  int phase,index;float cooldown,held,elapsed;Vector2 home,edge;
  public Vector2 Point {get;private set;}
  public bool Active {get{return phase!=0;}}
  public int Checks {get;private set;}
  public int RearChecks {get;private set;}
  public void Cancel(){phase=0;cooldown=.7f;}
  public bool Step(float dt,Vector2 position,Vector2 facing,Vector2 forward,DeploymentNavigation nav,out Vector2 target,Vector2? cue=null)
  {
   target=position;cooldown-=dt;
   if(phase==0)
   {
    if(cooldown>0)return false;
    if(forward.sqrMagnitude<.1f)forward=facing;
    float angle=(index==0?-35:index==1?35:index==2?180:0)*Mathf.Deg2Rad;
    var direction=new Vector2(forward.x*Mathf.Cos(angle)-forward.y*Mathf.Sin(angle),forward.x*Mathf.Sin(angle)+forward.y*Mathf.Cos(angle)).normalized;
    var intended=cue.HasValue?cue.Value:nav.PreAimCorner(position,position+direction*14);
    home=edge=position;Point=intended;
    if(!nav.SightClear(position,intended))
    {
     bool exposed=false;var lateral=new Vector2(-direction.y,direction.x);
     for(float reach=.8f;reach<=2.41f&&!exposed;reach+=.8f)
      for(int side=-1;side<=1&&!exposed;side+=2)
      {var candidate=position+lateral*(reach*side);if(nav.Clear(position,candidate)&&nav.SightClear(candidate,intended)){edge=candidate;exposed=true;}}
     if(!exposed)Point=nav.VisibleAimPoint(position,intended);
    }
    if(Vector2.Dot((Point-position).normalized,forward.normalized)<0)edge=home;
    if(Vector2.Distance(Point,position)<1){index=(index+1)%4;cooldown=.5f;return false;}
    phase=1;held=elapsed=0;
   }
   elapsed+=dt;if(elapsed>3.5f){Cancel();index=(index+1)%4;return false;}
   target=phase==1?edge:home;
   // Never leave cover while looking away from the angle being opened.
   if(phase==1&&Vector2.Dot(facing.normalized,(Point-position).normalized)<.97f)target=position;
   if(!nav.Clear(position,target)){Cancel();return false;}
   if(Vector2.Distance(position,target)>.12f)return true;
   if(phase==1)
   {
    if(Vector2.Dot(facing.normalized,(Point-position).normalized)>.985f)held+=dt;
    if(held>=.4f)phase=2;
   }
   else {Checks++;if(index==2)RearChecks++;index=(index+1)%4;Cancel();return false;}
   return true;
  }
 }
 public partial class Prototype
 {
  readonly ClutchSearch[] clutchSearch=new ClutchSearch[10];
  readonly bool[] clutchMode=new bool[10],clutchUrgent=new bool[10];
  void ResetClutch(){for(int i=0;i<10;i++){clutchSearch[i]=new ClutchSearch();clutchMode[i]=clutchUrgent[i]=false;}}
  float ClutchTravel(int i,Vector2 goal)
  {
   var pos=MapPosition(i);float distance=0;
   if(routes[i]!=null&&routeSteps[i]<routes[i].Count&&Vector2.Distance(routes[i][routes[i].Count-1],goal)<3)
   {for(int n=routeSteps[i];n<routes[i].Count;n++){distance+=Vector2.Distance(pos,routes[i][n]);pos=routes[i][n];}return distance;}
   return Vector2.Distance(pos,goal)*1.3f;
  }
  float ClutchSpareTime(int i)
  {
   if(director.PlantedSite>=0)
   {
    if(teamIndex[i]!=ctTeam)return float.PositiveInfinity;
    float defuse=director.Settings.defuseSeconds*(matchState[i].defuseKit?.5f:1);
    if(director.Defuser==i)defuse=Mathf.Max(0,defuse-director.DefuseProgress);
    return director.BombTimer-ClutchTravel(i,director.BombPosition)/2.4f-defuse;
   }
   if(teamIndex[i]==ctTeam)return float.PositiveInfinity;
   var site=layout.Sites[director.TargetSite];float travel=ClutchTravel(i,site);
   if(director.BombDropped)travel=ClutchTravel(i,director.BombPosition)+Vector2.Distance(director.BombPosition,site)*1.3f;
   return director.Settings.roundSeconds-director.Clock-travel/2.4f-Mathf.Max(0,director.Settings.plantSeconds-director.PlantProgress);
  }
  void UpdateClutch(int i)
  {
   clutchMode[i]=AutomaticMatch&&combat.Alive(i)&&combat.LivingCount(teamIndex,teamIndex[i])==1&&combat.LivingCount(teamIndex,1-teamIndex[i])>0;
   clutchUrgent[i]=clutchMode[i]&&ClutchSpareTime(i)<=5;
   if(!clutchMode[i]||clutchUrgent[i]||combat.FocusTarget(i)>=0||combat.Spamming(i))clutchSearch[i].Cancel();
  }
  bool StepClutchSearch(int i,PlayerObjective order,float dt)
  {
   if(!clutchMode[i]||clutchUrgent[i]||!order.valid||order.disengage||combat.FocusTarget(i)>=0||combat.Spamming(i)||autonomy.Blinded[i]||combat.Reloading(i)){clutchSearch[i].Cancel();return false;}
   if((director.Carrier==i&&director.PlantProgress>0)||(director.Defuser==i&&director.DefuseProgress>0)){clutchSearch[i].Cancel();return false;}
   if(Mathf.Abs(PlayerHeight(i)-MapFloor(MapPosition(i),PlayerHeight(i)))>.4f)return false;
   Vector2 forward=order.destination-MapPosition(i);
   if(forward.magnitude<3)forward=order.watch.sqrMagnitude>.1f?order.watch:MapFacing(i);
   var heard=autonomy.Sounds.Heard(i);Vector2? cue=heard.known&&heard.age<.5f&&heard.kind!=SoundKind.Beep?(Vector2?)heard.position:null;
   Vector2 informed;if(ClutchKnownCue(i,out informed)){cue=informed;if(clutchSearch[i].Active&&Vector2.Distance(clutchSearch[i].Point,informed)>3)clutchSearch[i].Cancel();}
   Vector2 target;if(!clutchSearch[i].Step(dt,MapPosition(i),MapFacing(i),forward.normalized,navigation,out target,cue))return false;
   peeking[i].Cancel();combat.SetKnife(i,false);
   var before=MapPosition(i);var next=Vector2.MoveTowards(before,target,dt*2.4f);
   actors[i].transform.position=World(next);GroundActor(i);moving[i]=Vector2.Distance(before,next)>.001f;arrived[i]=!moving[i];
   FaceWatch(i,clutchSearch[i].Point-next,dt);combat.PreAimHeight(i,MapFloor(clutchSearch[i].Point,PlayerHeight(i)),Vector2.Distance(next,clutchSearch[i].Point),dt,aimStats[i]);autonomy.Walking[i]=true;autonomy.Footstep(i,next,Vector2.Distance(before,next));
   routeValid[i]=false;repathDelay[i]=0;moveSpeed[i]=0;return true;
  }
  bool ClutchRifleSafe(int i,GroundWeapon drop)
  {
   int weapon=Array.IndexOf(WeaponCatalog.Ids,drop.weapon);
   if(!clutchMode[i]||clutchUrgent[i]||OwnedPrimary(i)!="awp"||combat.LivingCount(teamIndex,1-teamIndex[i])<2||weapon<11||weapon>16)return false;
   if(combat.FocusTarget(i)>=0||combat.Engaging(i)||combat.Reloading(i)||autonomy.Blinded[i])return false;
   for(int enemy=0;enemy<10;enemy++)if(teamIndex[enemy]!=teamIndex[i]&&vision.Sees(i,enemy))return false;
   var sound=autonomy.Sounds.Heard(i);if(sound.known&&sound.age<1.5f&&Vector2.Distance(MapPosition(i),sound.position)<18)return false;
   float distance=(actors[i].transform.position-Vector3.up-drop.position).magnitude;
   if(distance>4||ClutchSpareTime(i)<=distance/2.4f+5.35f)return false;
   return director.PlantProgress<=0&&director.DefuseProgress<=0;
  }
  bool CollectClutchRifle(int i,float dt)
  {
   GroundWeapon best=null;float score=float.MinValue;
   foreach(var item in groundWeapons)
   {
    if(item.receiver>=0||item.delivery>0||!ClutchRifleSafe(i,item))continue;
    var point=new Vector2(item.position.x,100-item.position.z);
    if(Mathf.Abs(PlayerHeight(i)-item.position.y)>.4f||!navigation.Clear(MapPosition(i),point)||!vision.LineOfSight(MapPosition(i),MapFacing(i),point))continue;
    if(!elevation.Sight(MapPosition(i),PlayerHeight(i)+1.65f,point,item.position.y+.15f)||!autonomy.ClearSight3D(MapPosition(i),PlayerHeight(i)+1.65f,point,item.position.y+.15f))continue;
    float value=MatchEconomy.Skill(Data.players[i],item.weapon)*10-Vector2.Distance(MapPosition(i),point);
    if(value>score){score=value;best=item;}
   }
   if(best==null)return false;
   clutchSearch[i].Cancel();peeking[i].Cancel();
   var dest=new Vector2(best.position.x,100-best.position.z);
   if(Vector2.Distance(MapPosition(i),dest)<=1.5f)return TryPickUpWeapon(i,best);
   var before=MapPosition(i);var next=Vector2.MoveTowards(before,dest,dt*2.4f);
   actors[i].transform.position=World(next);GroundActor(i);moving[i]=true;arrived[i]=false;
   FaceWatch(i,dest-before,dt);autonomy.Footstep(i,next,Vector2.Distance(before,next));routeValid[i]=false;return true;
  }
  bool ClutchKeepsRifle(int i,string pickup)
  {int held=Array.IndexOf(WeaponCatalog.Ids,OwnedPrimary(i));return clutchMode[i]&&combat.LivingCount(teamIndex,1-teamIndex[i])>=2&&held>=11&&held<=16&&pickup=="awp";}
 }
}


