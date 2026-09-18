using System.Collections.Generic;
using UnityEngine;
namespace FpsManager
{
 public sealed partial class DeploymentNavigation
 {
  public Vector2 VisibleAimPoint(Vector2 position,Vector2 intended)
  {
   if(SightClear(position,intended))return intended;
   if(AimOverride!=null)return AimOverride(position,intended);
   Vector2 best=position;float score=float.MinValue;var forward=(intended-position).normalized;
   foreach(var r in sightBlockers)
    foreach(var p in new[]{new Vector2(r.xMin-.12f,r.yMin-.12f),new Vector2(r.xMin-.12f,r.yMax+.12f),new Vector2(r.xMax+.12f,r.yMin-.12f),new Vector2(r.xMax+.12f,r.yMax+.12f)})
    {
     var delta=p-position;if(delta.magnitude<1||delta.magnitude>24)continue;
     float value=Vector2.Dot(delta.normalized,forward)*20-Vector2.Distance(p,intended)*.2f;
     if(value>score&&SightClear(position,p)){score=value;best=p;}
    }
   return best;
  }
  // Candidate angles come only from map geometry; never from enemy transforms.
  public Vector2 PreAimCorner(Vector2 position,Vector2 intended)
  {
   var forward=(intended-position).normalized;Vector2 best=intended;float score=float.MinValue;
   foreach(var r in sightBlockers)
    foreach(var p in new[]{new Vector2(r.xMin-.85f,r.yMin-.85f),new Vector2(r.xMin-.85f,r.yMax+.85f),new Vector2(r.xMax+.85f,r.yMin-.85f),new Vector2(r.xMax+.85f,r.yMax+.85f)})
    {
     float distance=Vector2.Distance(position,p);if(distance<4||distance>20||!Clear(p,p))continue;
     float alignment=Vector2.Dot((p-position).normalized,forward);if(alignment<.65f)continue;
     // Aim slightly past the lip into the corridor the opponent could emerge from.
     var probe=p+forward*1.2f;if(!Clear(probe,probe))probe=p;
     float value=alignment*12-Mathf.Abs(distance-10)*.35f-Vector2.Distance(probe,intended)*.15f;
     if(value<=score)continue;score=value;best=probe;
    }
   return best;
  }
 }
 public sealed class PreAimPlanner
 {
  Vector2 anchor,previousGoal,origin;float remaining;bool ready;
  public Vector2 Choose(float dt,Vector2 position,Vector2 facing,PlayerObjective order,List<Vector2> route,int step,DeploymentNavigation navigation)
  {
   remaining-=dt;
   if(ready&&remaining>0&&Vector2.Distance(origin,position)<4&&Vector2.Distance(previousGoal,order.destination)<4)return anchor;
   var direction=order.watch.sqrMagnitude>.001f?order.watch.normalized:facing;
   bool holding=(order.task==PlayerTask.DefendSite||order.task==PlayerTask.HoldSite)&&Vector2.Distance(position,order.destination)<8;
   var intended=position+direction*16;
   if(!holding&&route!=null&&step<route.Count)
   {
    float distance=0;var last=position;
    for(int n=step;n<route.Count;n++){distance+=Vector2.Distance(last,route[n]);last=route[n];intended=last;if(distance>=12)break;}
    if(Vector2.Distance(position,intended)<4)intended=position+direction*12;
   }
   else if(!holding&&order.valid&&Vector2.Distance(position,order.destination)>5)intended=order.destination;
   anchor=navigation.PreAimCorner(position,intended);origin=position;previousGoal=order.destination;remaining=1.2f;ready=true;return anchor;
  }
 }
 public sealed partial class CombatSystem
 {
  public void PreAimHeight(int i,float targetFeet,float distance,float dt,int aim)
  {
   if(FeetHeight==null||Engaging(i))return;
   // Pre-aim at a standing head (1.70m) from the 1.65m eye, not a feet/body reference.
   float desired=Mathf.Atan2(targetFeet-FeetHeight(i)+.05f,Mathf.Max(4,distance))/Mathf.Deg2Rad;
   float change=(65+Mathf.Clamp01(aim/100f)*95)*dt;aimElevation[i]+=Mathf.Clamp(desired-aimElevation[i],-change,change);
  }
 }
 public partial class Prototype
 {
  readonly PreAimPlanner[] preAim=new PreAimPlanner[10];
  Vector2 ExpectedAngle(int i,PlayerObjective order,float dt)
  {
   if(preAim[i]==null)preAim[i]=new PreAimPlanner();
   return preAim[i].Choose(dt,MapPosition(i),MapFacing(i),order,routes[i],routeSteps[i],navigation);
  }
  bool AlignBeforeEntry(int i,PlayerObjective order,float dt)
  {
   if(!AutomaticMatch||!order.valid||order.disengage||combat.Engaging(i)||(director.Clock<15&&SafeForTravel(i))||combat.KnifeOut(i))return false;
   var desired=SharedAimDirection(i,navigation.VisibleAimPoint(MapPosition(i),ExpectedAngle(i,order,0)));
   if(desired.sqrMagnitude<1||Vector2.Dot(MapFacing(i).normalized,desired.normalized)>.7f)return false;
   FaceWatch(i,desired,dt);moveSpeed[i]=0;arrived[i]=true;return true;
  }
 }
}
