using System;using UnityEngine;
namespace FpsManager {
 public static class PlayerCollision {
  public const float Diameter=1.0f;
  public static bool Blocked(Vector3 from,Vector3 to,Vector3 other,float height=1.8f,float otherHeight=1.8f){
   if(Mathf.Max(from.y,to.y)+height<=other.y||Mathf.Min(from.y,to.y)>=other.y+otherHeight)return false;
   var a=new Vector2(from.x-other.x,from.z-other.z);var b=new Vector2(to.x-other.x,to.z-other.z);var d=b-a;
   float t=d.sqrMagnitude<.000001f?0:Mathf.Clamp01(-Vector2.Dot(a,d)/d.sqrMagnitude);
   // Existing overlaps may separate, but may never move closer or pass through.
   if(a.sqrMagnitude<Diameter*Diameter-.001f&&from.y+height>other.y&&from.y<other.y+otherHeight)return b.sqrMagnitude<=a.sqrMagnitude||Vector2.Dot(a,b)<0;
   return (a+d*t).sqrMagnitude<Diameter*Diameter-.001f;
  }
 }
 public partial class Prototype {
  readonly Vector3[] collisionStart=new Vector3[10],yieldDirection=new Vector3[10];
  readonly float[] trafficWait=new float[10],yieldFor=new float[10];int collisionTurn;
  void ResetTraffic(){Array.Clear(trafficWait,0,10);Array.Clear(yieldFor,0,10);collisionTurn=0;}
  void BeginPlayerMovement(){for(int i=0;i<actors.Count;i++)collisionStart[i]=actors[i].transform.position;}
  bool ProtectedInteraction(int i){return director!=null&&((director.Defuser==i&&director.DefuseProgress>0)||(director.Carrier==i&&director.PlantProgress>0));}
  bool BodyMoveClear(int i,Vector3 from,Vector3 to,bool alternative=false){
   if(alternative&&sourceArena!=null&&(!sourceArena.WalkClear(from-Vector3.up,to-Vector3.up)||!SourceHullClear(from-Vector3.up,to-Vector3.up,IsCrouched(i)?1.2f:1.8f)))return false;
   if(alternative&&sourceArena==null&&!navigation.Clear(new Vector2(from.x,100-from.z),new Vector2(to.x,100-to.z)))return false;
   for(int j=0;j<actors.Count;j++)if(i!=j&&combat.Alive(j)){
    var other=actors[j].transform.position;
    // Reserve an airborne teammate's landing space, so another route cannot occupy it mid-jump.
    if(sourceJumping[j])for(int k=0;k<=12;k++){
     var reserved=JumpArc(jumpFrom[j],jumpTo[j],k/12f)+Vector3.up;
     if(PlayerCollision.Blocked(from,to,reserved,IsCrouched(i)?1.2f:1.8f,1.2f))return false;
    }
    if(!PlayerCollision.Blocked(from,to,other,IsCrouched(i)?1.2f:1.8f,IsCrouched(j)?1.2f:1.8f))continue;
    // Different elevations can clear vertically by crouching; never shrink the horizontal body.
    if(sourceArena!=null&&other.y-Mathf.Max(from.y,to.y)>=1.2f&&SourceHullClear(from-Vector3.up,to-Vector3.up,1.2f)){crouchFor[i]=.7f;continue;}
    return false;
   }return true;
  }
  // Own-team positions plus the shooter's remembered aim point; no hidden enemy transforms.
  public static bool InFiringLane(Vector2 shooter,Vector2 aim,Vector2 friend){
   var delta=aim-shooter;float range=delta.magnitude;if(range<1)return false;var direction=delta/range;var rel=friend-shooter;float along=Vector2.Dot(rel,direction);
   return along>.6f&&along<Mathf.Min(range-.4f,7)&&Mathf.Abs(rel.x*direction.y-rel.y*direction.x)<.65f;
  }
  void ClearFriendlyFiringLanes(){
   for(int i=0;i<actors.Count;i++){
    if(!combat.Alive(i)||combat.FocusTarget(i)<0||!combat.Engaging(i)||ProtectedInteraction(i))continue;
    var aim=combat.FocusPoint(i);var d=(aim-MapPosition(i)).normalized;var side=new Vector3(-d.y,0,-d.x);
    for(int j=0;j<actors.Count;j++){
     if(i==j||teamIndex[i]!=teamIndex[j]||!combat.Alive(j)||Math.Abs(PlayerHeight(i)-PlayerHeight(j))>.6f||!InFiringLane(MapPosition(i),aim,MapPosition(j)))continue;
     if(sourceArena!=null&&!sourceArena.Sight(actors[i].transform.position+Vector3.up*.6f,actors[j].transform.position+Vector3.up*.6f))continue;
     // A standing rear shooter can fire over a crouched front teammate.
     if(!ProtectedInteraction(j)&&!sourceJumping[j])crouchFor[j]=Math.Max(crouchFor[j],.7f);
     if(!ProtectedInteraction(j)&&!combat.Engaging(j)){yieldDirection[j]=side*(j%2==0?1:-1);yieldFor[j]=Math.Max(yieldFor[j],.45f);}
     else if(IsCrouched(i)||ProtectedInteraction(j)){yieldDirection[i]=side*(i%2==0?1:-1);yieldFor[i]=Math.Max(yieldFor[i],.3f);}
    }
   }
  }
  Vector3 GroundTrafficCandidate(Vector3 p){if(sourceArena==null)return p;var snap=sourceArena.Snap(p-Vector3.up);return (snap-(p-Vector3.up)).magnitude<.35f?snap+Vector3.up:p;}
  bool CanStandAfterCrouch(int i){
   var p=collisionStart[i];
   if(sourceArena!=null&&!SourceHullClear(p-Vector3.up,p-Vector3.up,1.8f))return false;
   for(int j=0;j<actors.Count;j++)if(i!=j&&combat.Alive(j)&&PlayerCollision.Blocked(p,p,actors[j].transform.position,1.8f,IsCrouched(j)?1.2f:1.8f))return false;
   return true;
  }
  void RequestTrafficYield(int i,Vector3 from,Vector3 next,bool ordered=false){
   var forward=new Vector3(next.x-from.x,0,next.z-from.z).normalized;
   if(forward.magnitude<.1f)return;
   var side=new Vector3(-forward.z,0,forward.x)*(i%2==0?1:-1);
   for(int j=0;j<actors.Count;j++){
    if(j==i||(ordered&&j<i)||teamIndex[j]!=teamIndex[i]||!combat.Alive(j)||ProtectedInteraction(j)||combat.Engaging(j)||sourceJumping[j]||yieldFor[j]>0)continue;
    var at=actors[j].transform.position;
    if(!PlayerCollision.Blocked(from,next,at,IsCrouched(i)?1.2f:1.8f,IsCrouched(j)?1.2f:1.8f))continue;
    foreach(var escape in new[]{forward,side,side*-1,forward*-1}){
     var free=GroundTrafficCandidate(at+escape*.35f);
     if(!BodyMoveClear(j,at,free,true))continue;
     yieldDirection[j]=escape;yieldFor[j]=.85f;break;
    }
   }
  }
  void ResolvePlayerMovement(float dt){
   ClearFriendlyFiringLanes();
   var intended=new Vector3[actors.Count];for(int i=0;i<actors.Count;i++){intended[i]=actors[i].transform.position;actors[i].transform.position=combat.Alive(i)?collisionStart[i]:intended[i];if(crouchFor[i]>0&&crouchFor[i]<=dt&&combat.Alive(i)&&!CanStandAfterCrouch(i))crouchFor[i]=.25f;
    crouchFor[i]=Mathf.Max(0,crouchFor[i]-dt);jumpCooldown[i]=Mathf.Max(0,jumpCooldown[i]-dt);}
   for(int k=0;k<actors.Count;k++){
    int i=(k+collisionTurn)%actors.Count;if(!combat.Alive(i))continue;var from=collisionStart[i];var to=intended[i];var delta=to-from;
    if(yieldFor[i]>0&&!ProtectedInteraction(i)&&!sourceJumping[i]){
     yieldFor[i]=Mathf.Max(0,yieldFor[i]-dt);var candidate=GroundTrafficCandidate(from+yieldDirection[i]*dt*2.4f);
     if(BodyMoveClear(i,from,candidate,true))to=candidate;
    }
    if(!BodyMoveClear(i,from,to)){
     trafficWait[i]+=dt;to=from;float step=Mathf.Min(2.4f*dt,Mathf.Max(.8f*dt,delta.magnitude));
     var forward=new Vector3(delta.x,0,delta.z).normalized;
     if(forward.magnitude<.1f)forward=new Vector3(i%2==0?1:-1,0,0);
     var side=new Vector3(-forward.z,0,forward.x)*(i%2==0?1:-1);
     foreach(var direction in new[]{side,side*-1,(side-forward).normalized,(side*-1-forward).normalized,forward*-1,forward*.35f}){
      var candidate=GroundTrafficCandidate(from+direction*step);if(BodyMoveClear(i,from,candidate,true)){to=candidate;break;}
     }
     if(trafficWait[i]>.45f&&yieldFor[i]<=0){
      for(int j=0;j<actors.Count;j++)if(j!=i&&teamIndex[j]==teamIndex[i]&&combat.Alive(j)&&!ProtectedInteraction(j)&&!combat.Engaging(j)&&!sourceJumping[j]&&(actors[j].transform.position-from).magnitude<1.7f&&yieldFor[j]<=0){
       var blockedAt=actors[j].transform.position;
       foreach(var escape in new[]{forward,side,side*-1,forward*-1}){
        var free=GroundTrafficCandidate(blockedAt+escape*.35f);
        if(!BodyMoveClear(j,blockedAt,free,true))continue;yieldDirection[j]=escape;yieldFor[j]=.85f;break;
       }
       break;
      }
     }
     // Keep NAV cursor. Resetting the route would send the queue back to the same center.
    }else if(!sourceJumping[i])trafficWait[i]=Mathf.Max(0,trafficWait[i]-dt*2);
    actors[i].transform.position=to;if(sourceArena!=null)sourceFeet[i]=to.y-1;
    float speed=new Vector2(to.x-from.x,to.z-from.z).magnitude/Mathf.Max(.001f,dt);combat.SetFiringSpeed(i,sourceJumping[i]?Math.Max(5,speed):speed,dt);moving[i]=speed>.01f;
   }
   collisionTurn=(collisionTurn+1)%actors.Count;
  }
 }
}
