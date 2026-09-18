using System;using UnityEngine;
namespace FpsManager {
 public partial class Prototype {
  readonly bool[] tacticalCrouch=new bool[10],sourceJumping=new bool[10];
  readonly float[] jumpWait=new float[10];
  readonly float[] crouchFor=new float[10],sourceJumpTime=new float[10],jumpCooldown=new float[10];
  readonly Vector3[] jumpFrom=new Vector3[10],jumpTo=new Vector3[10];
  readonly int[] jumpNode=new int[10];
  public bool IsCrouched(int i){return IsSaveCrouched(i)||tacticalCrouch[i]||crouchFor[i]>0;}
  public bool IsJumping(int i){return sourceJumping[i];}
  void ResetTraversal(){Array.Clear(jumpWait,0,10);Array.Clear(ctOpeningDone,0,10);Array.Clear(tacticalCrouch,0,10);Array.Clear(crouchFor,0,10);Array.Clear(sourceJumping,0,10);Array.Clear(jumpCooldown,0,10);ResetTraffic();}
  public static Vector3 JumpArc(Vector3 start,Vector3 end,float t){var p=start+(end-start)*t;p.y+=4*.55f*t*(1-t);return p;}
  bool SourceHullClear(Vector3 a,Vector3 b,float height){
   // Sample both sides of the hull: a center ray alone permits clipping door frames.
   foreach(var offset in new[]{Vector3.zero,new Vector3(.22f,0,0),new Vector3(-.22f,0,0),new Vector3(0,0,.22f),new Vector3(0,0,-.22f)}){
    if(!sourceArena.Sight(a+offset+Vector3.up*.2f,b+offset+Vector3.up*.2f)||!sourceArena.Sight(a+offset+Vector3.up*(height-.1f),b+offset+Vector3.up*(height-.1f)))return false;
    if(!sourceArena.Sight(b+offset+Vector3.up*.2f,b+offset+Vector3.up*height))return false;
   }return true;
  }
  bool PrepareSourceTraversal(int i,float dt){
   if(sourceJumping[i])return StepSourceJump(i,dt);
   var path=sourceRoutes[i];int n=routeSteps[i];var start=actors[i].transform.position-Vector3.up;
   while(n<path.Count&&(path[n]-start).magnitude<.08f)n++;
   if(n>=path.Count)return false;
   var end=path[n];var near=Vector3.MoveTowards(start,end,.5f);
   if(!SourceHullClear(start,near,1.8f)&&SourceHullClear(start,near,1.2f))crouchFor[i]=.5f;
   float rise=end.y-start.y,flat=Vector2.Distance(SourceArena.Flat(start),SourceArena.Flat(end));
   if(jumpCooldown[i]>0||rise<.38f||rise>1.15f||flat>2.4f)return false;
   // A gently rising NAV connection is a stair/ramp, not a box jump.
   if(flat>rise*1.4f&&sourceArena.WalkClear(start,end))return false;
   // Only use the next NAV link, never invent a jump through a wall or across floors.
   var last=start;for(int k=1;k<=12;k++){var p=JumpArc(start,end,k/12f);if(!SourceHullClear(last,p,1.2f))return false;last=p;}
   // Check the entire jump corridor before committing. Competing jumpers queue on the ground.
   last=start;for(int k=1;k<=12;k++){
    var p=JumpArc(start,end,k/12f);
    if(!BodyMoveClear(i,last+Vector3.up,p+Vector3.up)){
     // A stable queue priority prevents two grounded jumpers waiting on one another.
     jumpWait[i]+=dt;if(jumpWait[i]>.45f)RequestTrafficYield(i,last+Vector3.up,p+Vector3.up,true);
     moving[i]=false;arrived[i]=false;return true;
    }
    last=p;
   }
   jumpWait[i]=0;
   jumpFrom[i]=start;jumpTo[i]=end;jumpNode[i]=n;sourceJumpTime[i]=0;sourceJumping[i]=true;crouchFor[i]=.8f;
   return StepSourceJump(i,dt);
  }
  bool StepSourceJump(int i,float dt){
   if(sourceArena==null||!sourceJumping[i]||!combat.Alive(i))return false;
   float t=Mathf.Clamp01((sourceJumpTime[i]+dt)/.65f);var next=JumpArc(jumpFrom[i],jumpTo[i],t)+Vector3.up;
   if(!BodyMoveClear(i,actors[i].transform.position,next)){
    trafficWait[i]+=dt;if(trafficWait[i]>.2f)RequestTrafficYield(i,actors[i].transform.position,next);
    moving[i]=false;arrived[i]=false;return true;
   }
   trafficWait[i]=0;
   sourceJumpTime[i]+=dt;actors[i].transform.position=next;sourceFeet[i]=PlayerHeight(i);moving[i]=true;arrived[i]=false;
   AimWhileMoving(i,director.Objective(i),dt);
   if(t>=1){sourceJumping[i]=false;jumpCooldown[i]=1;routeSteps[i]=Math.Max(routeSteps[i],jumpNode[i]+1);autonomy.Footstep(i,MapPosition(i),2);}
   return true;
  }
 }
}
