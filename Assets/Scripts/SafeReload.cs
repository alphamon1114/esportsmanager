using System;using System.Collections.Generic;using UnityEngine;
namespace FpsManager {
 public partial class Prototype {
  readonly bool[] reloadCoverValid=new bool[10];
  readonly Vector2[] reloadCover=new Vector2[10];
  readonly float[] reloadSearchAt=new float[10];
  void ResetSafeReload(){Array.Clear(reloadCoverValid,0,10);Array.Clear(reloadSearchAt,0,10);}
  bool ReloadCovered(Vector2 p,float floor,List<Vector2> threats){
   foreach(var threat in threats){
    if(Vector2.Distance(p,threat)<2)return false;
    float enemyFloor=MapFloor(threat,floor);
    if(sourceArena!=null){
     if(sourceArena.Sight(SourceArena.At(p,floor+1.65f),SourceArena.At(threat,enemyFloor+1.65f))||sourceArena.Sight(SourceArena.At(p,floor+1),SourceArena.At(threat,enemyFloor+1.65f)))return false;
    }else if(navigation.SightClear(p,threat))return false;
   }return true;
  }
  bool CanReloadSafely(int i){return !AutomaticMatch||(!ProtectedInteraction(i)&&!sourceJumping[i]&&ReloadCovered(MapPosition(i),PlayerHeight(i),SaveThreats(i)));}
  bool FindReloadCover(int i,List<Vector2> threats){
   var start=MapPosition(i);float floor=PlayerHeight(i);var candidates=new List<Vector2>();var order=director.Objective(i);
   if(order.hasCover)candidates.Add(order.cover);
   foreach(float radius in new[]{2f,4f,6f,9f,12f})for(int n=0;n<12;n++){
    float angle=n*Mathf.PI/6;var p=start+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
    if(sourceArena!=null)p=SourceArena.Flat(sourceArena.Snap(SourceArena.At(p,floor)));
    candidates.Add(p);
   }
   candidates.Sort((a,b)=>Vector2.Distance(start,a).CompareTo(Vector2.Distance(start,b)));
   foreach(var p in candidates){
    float height=MapFloor(p,floor);if(Vector2.Distance(start,p)<.5f||Vector2.Distance(start,p)>14||Mathf.Abs(height-floor)>1.2f||!ReloadCovered(p,height,threats))continue;
    if(sourceArena!=null&&!SourceHullClear(SourceArena.At(p,height),SourceArena.At(p,height),1.8f))continue;
    try{
     List<Vector3> path3=null;List<Vector2> path;
     if(sourceArena!=null){path3=sourceArena.Route(actors[i].transform.position-Vector3.up,SourceArena.At(p,height));path=new List<Vector2>();foreach(var point in path3)path.Add(SourceArena.Flat(point));}
     else path=navigation.Route(start,p,new List<Vector2>());
     float length=0;bool unsafePath=false;var prev=start;
     foreach(var point in path){length+=Vector2.Distance(prev,point);prev=point;foreach(var threat in threats)if(Vector2.Distance(point,threat)<Mathf.Min(8,Vector2.Distance(start,threat)-2))unsafePath=true;}
     if(length>24||unsafePath)continue;
     reloadCover[i]=p;reloadCoverValid[i]=true;routes[i]=path;if(path3!=null)sourceRoutes[i]=path3;
     destinations[i]=p;routeSteps[i]=0;routeValid[i]=true;repathDelay[i]=0;return true;
    }catch(InvalidOperationException){}
   }return false;
  }
  bool StepSafeReload(int i,float dt){
   if(!AutomaticMatch||ProtectedInteraction(i))return false;
   bool threat=HasDirectFight(i)||hurtUntil[i]>director.Clock;
   if(!combat.Reloading(i)&&!combat.ShouldReload(i,threat)){reloadCoverValid[i]=false;return false;}
   var threats=SaveThreats(i);var before=MapPosition(i);
   bool safe=ReloadCovered(before,PlayerHeight(i),threats);
   combat.SetKnife(i,false);peeking[i].Cancel();clutchSearch[i].Cancel();autonomy.Walking[i]=false;
   if(safe){
    combat.RequestReload(i);reloadCoverValid[i]=false;moving[i]=false;arrived[i]=true;moveSpeed[i]=0;routeValid[i]=false;
    if(threats.Count>0)FaceWatch(i,threats[0]-before,dt);return true;
   }
   if(director.Clock>=reloadSearchAt[i]){
    reloadSearchAt[i]=director.Clock+.75f;
    if(!reloadCoverValid[i]||!ReloadCovered(reloadCover[i],MapFloor(reloadCover[i],PlayerHeight(i)),threats)){
     reloadCoverValid[i]=false;FindReloadCover(i,threats);
    }
   }
   if(reloadCoverValid[i]){
    MoveTo(i,reloadCover[i]);if(routeValid[i]&&routeSteps[i]<routes[i].Count)StepRoute(i,dt);
   }else{
    // No reachable cover: defend with the sidearm, or retreat while searching again.
    string pistol=HasDirectFight(i)&&Vector2.Distance(before,combat.FocusPoint(i))<=25&&combat.Magazine(i)==0&&Array.IndexOf(WeaponCatalog.Ids,combat.WeaponFor(i).id)>=6?ReadyPistol(i):null;
    if(pistol!=null){finishingPrimary[i]=combat.WeaponFor(i).id;SwitchAwperWeapon(i,pistol);return false;}
    if(threats.Count>0){var away=(before-threats[0]).normalized;var next=before+away*dt*3;
     if(navigation.Clear(before,next)){if(sourceArena!=null){var from=actors[i].transform.position;var to=SourceArena.At(next,PlayerHeight(i))+Vector3.up;if(BodyMoveClear(i,from,to,true))actors[i].transform.position=to;}else actors[i].transform.position=World(next);}
    }
    autonomy.Footstep(i,MapPosition(i),Vector2.Distance(before,MapPosition(i)));
   }
   moving[i]=Vector2.Distance(before,MapPosition(i))>.001f;arrived[i]=!moving[i];
   if(threats.Count>0)FaceWatch(i,threats[0]-MapPosition(i),dt);return true;
  }
 }
}
