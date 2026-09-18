using System;
using UnityEngine;
namespace FpsManager {
 public static class ThreatRules {
  // Conservative upper bound: never exclude an angle an enemy could already reach.
  public static bool CanReach(Vector2 last,float age,Vector2 point){return Vector2.Distance(last,point)<=3+Mathf.Max(0,age)*6.2f;}
  public static float Priority(bool direct,bool local,float distance,float age){return (direct?300:local?180:40)-distance*2-age*10;}
 }
 public partial class Prototype {
  readonly Vector2[] hurtPoint=new Vector2[10],tradePoint=new Vector2[10];
  readonly float[] hurtUntil=new float[10],tradeUntil=new float[10];
  readonly Vector2[,] remembered=new Vector2[2,10];
  readonly float[,] rememberedAt=new float[2,10];
  void ResetThreats(){Array.Clear(hurtUntil,0,10);Array.Clear(tradeUntil,0,10);for(int t=0;t<2;t++)for(int e=0;e<10;e++)rememberedAt[t,e]=-100;}
  void RememberThreats(){for(int t=0;t<2;t++)for(int e=0;e<10;e++){var k=vision.Knowledge(t,e);if(k.known&&!k.anonymous&&k.age<.2f){remembered[t,e]=k.lastKnownPosition;rememberedAt[t,e]=director.Clock-k.age;}}}
  bool ImmediateCue(int i,out Vector2 point){point=hurtPoint[i];if(hurtUntil[i]>director.Clock)return true;point=tradePoint[i];return tradeUntil[i]>director.Clock;}
  void RegisterHurt(int shooter,int victim){
   // Damage gives a coarse bearing, never the shooter's exact hidden position.
   var d=MapPosition(shooter)-MapPosition(victim);float a=Mathf.Atan2(d.y,d.x);a=(float)Math.Round(a/(Mathf.PI/4))*(Mathf.PI/4);
   hurtPoint[victim]=MapPosition(victim)+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*12;hurtUntil[victim]=director.Clock+2.5f;
   if(!(director.Defuser==victim&&director.CommittedDefuse)){director.InterruptDefuse(victim);combat.SetKnife(victim,false);}
   peeking[victim].Cancel();clutchSearch[victim].Cancel();
  }
  void RegisterTrade(KillEvent entry){
   int victim=entry.victim,enemy=entry.killer;var k=vision.Knowledge(teamIndex[victim],enemy);
   // A death alone does not reveal an unseen killer.
   if(!k.known||k.anonymous||k.age>1.5f)return;
   int responder=-1;float best=18;
   for(int i=0;i<10;i++){if(i==victim||teamIndex[i]!=teamIndex[victim]||!combat.Alive(i)||combat.FocusTarget(i)>=0||combat.Reloading(i))continue;
    float d=Vector2.Distance(MapPosition(i),MapPosition(victim));if(d>18)continue;if(director.Objective(i).stackHidden)d*=.65f;if(d<best){best=d;responder=i;}}
   if(responder<0)return;tradePoint[responder]=k.lastKnownPosition;tradeUntil[responder]=director.Clock+2.5f;peeking[responder].Cancel();
  }
  bool HasDirectFight(int i){int t=combat.FocusTarget(i);return t>=0&&vision.Sees(i,t);}
  bool SafeToDefuse(int i){
   if(director.Phase==RoundPhase.PostPlant&&combat.LivingCount(teamIndex,1-ctTeam)==0)return true;
   if(hurtUntil[i]>director.Clock||autonomy.Blinded[i])return false;
   for(int e=0;e<10;e++)if(teamIndex[e]!=teamIndex[i]&&combat.Alive(e)){
    if(vision.Sees(i,e))return false;
    var info=vision.Knowledge(teamIndex[i],e);
    if(info.known&&info.age<1.5f&&Vector2.Distance(info.lastKnownPosition,director.BombPosition)<12)return false;
   }
   var noise=autonomy.Sounds.Heard(i);
   return !noise.known||noise.kind==SoundKind.Beep||noise.kind==SoundKind.Defuse||noise.age>1.2f||Vector2.Distance(noise.position,director.BombPosition)>9;
  }
  bool StepDefusing(int i){
   if(director.Defuser!=i||director.DefuseProgress<=0)return false;
   if(!director.CommittedDefuse&&!SafeToDefuse(i)){director.InterruptDefuse(i);return false;}
   peeking[i].Cancel();clutchSearch[i].Cancel();moving[i]=false;arrived[i]=true;moveSpeed[i]=0;routeValid[i]=false;return true;
  }
  bool StepThreatResponse(int i,float dt){
   if(director.CommittedDefuse&&director.Defuser==i)return false;
   Vector2 point;if(HasDirectFight(i)||!ImmediateCue(i,out point))return false;
   if(director.Carrier==i&&director.PlantProgress>0&&hurtUntil[i]<=director.Clock)return false;
   clutchSearch[i].Cancel();peeking[i].Cancel();combat.SetKnife(i,false);FaceWatch(i,point-MapPosition(i),dt);
   var before=MapPosition(i);var next=before;
   var hold=director.Objective(i);
   if(hold.stackHidden&&layout.StackPeek!=null&&hurtUntil[i]<=director.Clock&&tradeUntil[i]>director.Clock&&Vector2.Dot(MapFacing(i),(point-before).normalized)>.9f){
    int site=Vector2.Distance(hold.destination,layout.Sites[0])<Vector2.Distance(hold.destination,layout.Sites[1])?0:1;
    var edge=layout.StackPeek[site][i%5];MoveTo(i,edge);if(routeValid[i]&&routeSteps[i]<routes[i].Count)StepRoute(i,dt);
    moving[i]=Vector2.Distance(before,MapPosition(i))>.001f;arrived[i]=!moving[i];tacticalCrouch[i]=false;return true;
   }
   // Turn first; trade only through a locally clear, short peek. Never chase hidden coordinates.
   if(Mathf.Abs(PlayerHeight(i)-MapFloor(before,PlayerHeight(i)))<.4f&&hurtUntil[i]<=director.Clock&&Vector2.Dot(MapFacing(i),(point-before).normalized)>.97f&&!navigation.SightClear(before,point)){
    var lateral=new Vector2(-(point-before).normalized.y,(point-before).normalized.x);
    for(int side=-1;side<=1;side+=2){var edge=before+lateral*side*.8f;if(navigation.Clear(before,edge)&&navigation.SightClear(edge,point)){next=Vector2.MoveTowards(before,edge,dt*2.4f);break;}}
   }
   moving[i]=Vector2.Distance(before,next)>.001f;if(moving[i]){actors[i].transform.position=World(next);GroundActor(i);}arrived[i]=!moving[i];moveSpeed[i]=0;routeValid[i]=false;autonomy.Walking[i]=true;return true;
  }
  bool ClutchKnownCue(int i,out Vector2 point){
   point=MapPosition(i);float best=float.MaxValue;bool found=false;
   for(int e=0;e<10;e++){if(teamIndex[e]==teamIndex[i]||!combat.Alive(e))continue;float age=director.Clock-rememberedAt[teamIndex[i],e];
    if(age>20)continue;var p=remembered[teamIndex[i],e];
    if(ThreatRules.CanReach(p,age,MapPosition(i)))continue;
    float score=Vector2.Distance(MapPosition(i),p)+age*4;if(score<best){best=score;point=p;found=true;}}
   // Only exclude unknown angles when EVERY remaining opponent is still constrained by info.
   for(int e=0;e<10;e++)if(teamIndex[e]!=teamIndex[i]&&combat.Alive(e)){
    float age=director.Clock-rememberedAt[teamIndex[i],e];if(age>20||ThreatRules.CanReach(remembered[teamIndex[i],e],age,MapPosition(i)))return false;}
   return found;
  }
 }
}

