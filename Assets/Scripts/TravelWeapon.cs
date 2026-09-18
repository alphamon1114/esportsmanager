using UnityEngine;
namespace FpsManager
{
 public sealed partial class CombatSystem
 {
  readonly bool[] knifeOut=new bool[10];
  public bool KnifeOut(int i){return knifeOut[i];}
  public void SetKnife(int i,bool value)
  {
   if(knifeOut[i]==value)return;knifeOut[i]=value;states[i].target=-1;
   if(!value)fireHold[i]=Mathf.Max(fireHold[i],.35f);
  }
 }
 public partial class Prototype
 {
  readonly bool[] ctOpeningDone=new bool[10];
  readonly Vector3[] midEntry=new Vector3[2];
  readonly Vector2[] midEntryDirection=new Vector2[2];
  void BuildMidEntries(){
   foreach(int side in new[]{0,1}){
    var path=sourceArena.Route(side==0?sourceArena.CT:sourceArena.T,sourceArena.Mid);
    float length=0;for(int n=1;n<path.Count;n++)length+=(path[n]-path[n-1]).magnitude;
    midEntry[side]=PointAlong(path,Mathf.Max(0,length-12));
    midEntryDirection[side]=(SourceArena.Flat(PointAlong(path,Mathf.Max(0,length-9)))-SourceArena.Flat(midEntry[side])).normalized;
   }
  }
  bool RequiresMidGun(int i){
   var p=MapPosition(i);
   if(sourceArena==null)return Vector2.Distance(p,layout.Mid)<12;
   int side=teamIndex[i]==ctTeam?0:1;
   // The last 12 NAV metres mark the entrance. Keep the gun through the mid fight,
   // including the exit beyond its centre; unrelated floors and distant travel are excluded.
   return Vector2.Distance(p,layout.Mid)<24&&Mathf.Abs(PlayerHeight(i)-sourceArena.Mid.y)<3
    &&Vector2.Dot(p-SourceArena.Flat(midEntry[side]),midEntryDirection[side])>=0;
  }
  bool StepCtOpening(int i,float dt){
   if(teamIndex[i]!=ctTeam||ctOpeningDone[i]||director.PlantedSite>=0)return false;
   Vector2 cue;
   if(!SafeForTravel(i)||ImmediateCue(i,out cue)||HasDirectFight(i)){
    ctOpeningDone[i]=true;combat.SetKnife(i,false);return false;
   }
   int site=assignments[i]==0?1:assignments[i]==2?0:-1;
   var goal=site<0?layout.Mid:DefenseFor(ctTeam)==DefenseTactic.Stack&&layout.StackPost!=null?layout.StackPost[site][i%5]:layout.HoldRing[site][i%5];
   if(Vector2.Distance(MapPosition(i),goal)<1.5f&&Mathf.Abs(PlayerHeight(i)-GoalFloor(goal,PlayerHeight(i)))<1){ctOpeningDone[i]=true;combat.SetKnife(i,false);return false;}
   peeking[i].Cancel();clutchSearch[i].Cancel();autonomy.Walking[i]=false;combat.SetKnife(i,!RequiresMidGun(i));
   var order=new PlayerObjective{valid=true,task=PlayerTask.MoveToLane,destination=goal,watch=goal-MapPosition(i)};
   MoveTo(i,goal);var before=MapPosition(i);if(routeValid[i]&&routeSteps[i]<routes[i].Count)StepRoute(i,dt);
   moving[i]=Vector2.Distance(before,MapPosition(i))>.001f;arrived[i]=!moving[i];AimWhileMoving(i,order,dt);return true;
  }
  public string HeldWeapon(int i){return (!AutomaticMatch&&!roundMode&&!deploymentStarted)||combat.KnifeOut(i)?"knife":combat.WeaponFor(i).id;}
  bool SafeForTravel(int i)
  {
   if(combat.Engaging(i)||combat.Reloading(i)||autonomy==null||autonomy.Blinded[i])return false;
   var sound=autonomy.Sounds.Heard(i);if(sound.known&&sound.age<3&&Vector2.Distance(MapPosition(i),sound.position)<28)return false;
   for(int enemy=0;enemy<10;enemy++)if(teamIndex[enemy]!=teamIndex[i])
   {var known=vision.Knowledge(teamIndex[i],enemy);if(vision.Sees(i,enemy)||(known.known&&Vector2.Distance(MapPosition(i),known.lastKnownPosition)<28))return false;}
   return true;
  }
  void UpdateTravelWeapon(int i,PlayerObjective order)
  {
   if(!AutomaticMatch)return;
   bool travel=order.valid&&!order.cautious&&!order.disengage&&order.task!=PlayerTask.PlantBomb&&order.task!=PlayerTask.Defuse&&order.task!=PlayerTask.RecoverBomb&&order.task!=PlayerTask.Lurk&&order.task!=PlayerTask.Retake;
   float distance=Vector2.Distance(MapPosition(i),order.destination);
   bool safe=SafeForTravel(i);
   bool knife=travel&&safe&&!RequiresMidGun(i)&&distance>(combat.KnifeOut(i)?10:18)&&!peeking[i].Active;
   // After the opening run, only draw the knife on a clear known corridor.
   if(director.Clock>=15&&!navigation.SightClear(MapPosition(i),order.destination))knife=false;
   if(clutchMode[i]&&!clutchUrgent[i])knife=false;
   combat.SetKnife(i,knife);peeking[i].InformationAllowed=!clutchUrgent[i]&&(director.Clock>=15||!safe);
  }
 }
}
