using System;
using System.Collections.Generic;
using UnityEngine;
namespace FpsManager {
 public static class EquipmentSaveRules {
  public static bool ShouldSave(BuyPlan plan,bool ct,int friends,int enemies,float clock,bool planted,bool economyRisk,bool valuable,bool matchOverOnLoss){
   return plan==BuyPlan.Full&&friends>0&&enemies-friends>=3&&economyRisk&&valuable&&!matchOverOnLoss&&(ct||(!planted&&clock<=30));
  }
 }
 public sealed partial class DeploymentNavigation {
  public List<Vector2> SaveCoverCandidates(){var result=new List<Vector2>();foreach(var r in sightBlockers)foreach(var p in new[]{new Vector2(r.xMin-1.1f,r.yMin-1.1f),new Vector2(r.xMin-1.1f,r.yMax+1.1f),new Vector2(r.xMax+1.1f,r.yMin-1.1f),new Vector2(r.xMax+1.1f,r.yMax+1.1f)})if(Clear(p,p))result.Add(p);return result;}
 }
 public partial class Prototype {
  readonly bool[] savingEquipment=new bool[10],saveCrouched=new bool[10],saveGoalValid=new bool[10];
  readonly Vector2[] saveGoal=new Vector2[10];readonly float[] saveRecheck=new float[10],saveFightUntil=new float[10];
  float saveDecisionAt;
  public bool IsSavingEquipment(int i){return savingEquipment[i]&&combat.Alive(i)&&Stage==MatchStage.Live;}
  public bool IsSaveCrouched(int i){return IsSavingEquipment(i)&&saveCrouched[i];}
  float StanceHeight(int i){return PlayerHeight(i)-(IsCrouched(i)?.55f:0);}
  void ResetEquipmentSave(){Array.Clear(savingEquipment,0,10);Array.Clear(saveCrouched,0,10);Array.Clear(saveGoalValid,0,10);Array.Clear(saveRecheck,0,10);Array.Clear(saveFightUntil,0,10);saveDecisionAt=0;}
  bool EconomyNeedsSave(int team){
   int cannot=0;for(int i=0;i<10;i++)if(teamIndex[i]==team){var empty=new PlayerMatchState{credits=matchState[i].credits+2400,armor=0,helmet=false,equipment=new string[0]};if(!MatchEconomy.CanFullBuy(empty,Data.players[i],team==ctTeam))cannot++;}return cannot>=3;
  }
  void DecideEquipmentSave(){
   if(director.Clock<saveDecisionAt)return;saveDecisionAt=director.Clock+.5f;
   for(int team=0;team<2;team++){
    int friends=combat.LivingCount(teamIndex,team),enemies=combat.LivingCount(teamIndex,1-team);
    bool terminal=WinningScore(roundWins[team],roundWins[1-team]+1),risk=EconomyNeedsSave(team);
    for(int i=0;i<10;i++)if(teamIndex[i]==team&&combat.Alive(i)){
     if(buyPlans[team]!=BuyPlan.Full||terminal||enemies==0||enemies<=friends||(team!=ctTeam&&director.Phase==RoundPhase.PostPlant)){savingEquipment[i]=saveCrouched[i]=saveGoalValid[i]=false;continue;}
     if(savingEquipment[i]||director.Defuser==i&&director.DefuseProgress>0)continue;
     if(!EquipmentSaveRules.ShouldSave(buyPlans[team],team==ctTeam,friends,enemies,director.Clock,director.Phase==RoundPhase.PostPlant,risk,WeaponDropRules.Strength(StrongestWeapon(i))>=40,terminal))continue;
     savingEquipment[i]=true;saveGoalValid[i]=false;saveRecheck[i]=0;peeking[i].Cancel();clutchSearch[i].Cancel();routeValid[i]=false;director.InterruptDefuse(i);
    }
   }
  }
  List<Vector2> SaveThreats(int i){
   var result=new List<Vector2>();int team=teamIndex[i];
   for(int e=0;e<10;e++)if(teamIndex[e]!=team&&combat.Alive(e)){
    var info=vision.Knowledge(team,e);if(info.known&&info.age<12)result.Add(info.lastKnownPosition);
    else if(director.Clock-rememberedAt[team,e]<12)result.Add(remembered[team,e]);
   }
   var noise=autonomy.Sounds.Heard(i);if(noise.known&&noise.age<3&&noise.kind!=SoundKind.Beep)result.Add(noise.position);
   if(hurtUntil[i]>director.Clock)result.Add(hurtPoint[i]);return result;
  }
  bool SaveSight(Vector2 a,Vector2 b,float floor){return sourceArena!=null?sourceArena.Sight(SourceArena.At(a,MapFloor(a,floor)+1.1f),SourceArena.At(b,MapFloor(b,floor)+1.65f)):elevation.Sight(a,MapFloor(a,floor)+1.1f,b,MapFloor(b,floor)+1.65f);}
  bool SafeSaveSpot(Vector2 p,List<Vector2> threats,float floor){
   if(director.Phase==RoundPhase.PostPlant&&Vector2.Distance(p,director.BombPosition)<30)return false;
   foreach(var threat in threats)if(Vector2.Distance(p,threat)<12||SaveSight(p,threat,floor))return false;return true;
  }
  bool FindSaveCover(int i,List<Vector2> threats){
   var start=MapPosition(i);float floor=PlayerHeight(i);var candidates=new List<Vector2>();
   if(sourceArena==null)candidates=navigation.SaveCoverCandidates();
   else {int n=0;foreach(var area in sourceArena.Areas.Values){if(area.hull!=0||n++%8!=0)continue;var p=area.Center;if(Mathf.Abs(p.y-floor)<1.2f)candidates.Add(SourceArena.Flat(p));}}
   candidates.Add(start);var ranked=new List<KeyValuePair<Vector2,float>>();
   foreach(var p in candidates){float distance=Vector2.Distance(start,p);if(distance>80||!SafeSaveSpot(p,threats,floor))continue;
    int cover=0;for(int d=0;d<8;d++){float a=d*Mathf.PI/4;var edge=p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*4;if(!SaveSight(p,edge,floor))cover++;}
    if(cover<2)continue;float score=cover*8-distance*.35f;
    for(int other=0;other<10;other++)if(other!=i&&teamIndex[other]==teamIndex[i]&&saveGoalValid[other]&&Vector2.Distance(saveGoal[other],p)<3)score-=10;
    ranked.Add(new KeyValuePair<Vector2,float>(p,score));
   }
   ranked.Sort((a,b)=>b.Value.CompareTo(a.Value));
   for(int c=0;c<Math.Min(16,ranked.Count);c++){
    var p=ranked[c].Key;try{var route=navigation.Route(start,p,new List<Vector2>());if(route.Count==0&&Vector2.Distance(start,p)>1)continue;bool danger=false;
     // Never choose a hiding place whose route takes us closer to a known close threat.
     foreach(var step in route)foreach(var threat in threats)if(Vector2.Distance(step,threat)<Mathf.Min(10,Vector2.Distance(start,threat)-1)){danger=true;break;}
     if(danger)continue;
     if(sourceArena!=null){sourceRoutes[i]=sourceArena.Route(actors[i].transform.position-Vector3.up,SourceArena.At(p,MapFloor(p,floor)));route=new List<Vector2>();foreach(var point in sourceRoutes[i])route.Add(SourceArena.Flat(point));}
     saveGoal[i]=p;saveGoalValid[i]=true;routes[i]=route;destinations[i]=p;routeSteps[i]=0;routeValid[i]=true;repathDelay[i]=0;return true;
    }catch(InvalidOperationException){}
   }return false;
  }
  bool StepEquipmentSave(int i,float dt){
   if(!IsSavingEquipment(i))return false;
   saveCrouched[i]=false;combat.SetKnife(i,false);peeking[i].Cancel();clutchSearch[i].Cancel();combat.SpamAllowed[i]=false;
   var threats=SaveThreats(i);bool direct=HasDirectFight(i),hurt=hurtUntil[i]>director.Clock;
   if(direct&&saveFightUntil[i]<=0)saveFightUntil[i]=director.Clock+1.2f;
   if(!direct)saveFightUntil[i]=0;
   if(direct&&director.Clock<saveFightUntil[i]){moving[i]=false;arrived[i]=true;moveSpeed[i]=0;return true;}
   if(sourceArena==null&&climbers[i].link>=0&&StepElevation(i,new PlayerObjective{valid=true,task=PlayerTask.FallBack,destination=layout.Mid,disengage=true},dt)){saveGoalValid[i]=false;saveRecheck[i]=0;return true;}
   if(director.Clock>=saveRecheck[i]){saveRecheck[i]=director.Clock+3;if(!saveGoalValid[i]||!SafeSaveSpot(saveGoal[i],threats,PlayerHeight(i))||hurt){saveGoalValid[i]=false;routeValid[i]=false;FindSaveCover(i,threats);}}
   if(saveGoalValid[i]&&Vector2.Distance(MapPosition(i),saveGoal[i])>.8f){
    var before=MapPosition(i);autonomy.Walking[i]=Vector2.Distance(before,saveGoal[i])<9&&!hurt&&!direct;if(routeValid[i])StepRoute(i,dt);moving[i]=Vector2.Distance(before,MapPosition(i))>.001f;arrived[i]=!moving[i];
    var look=threats.Count>0?threats[0]-MapPosition(i):saveGoal[i]-MapPosition(i);FaceWatch(i,look,dt);return true;
   }
   moving[i]=false;arrived[i]=true;moveSpeed[i]=0;routeValid[i]=false;autonomy.Walking[i]=true;
   saveCrouched[i]=!direct&&!hurt&&saveGoalValid[i]&&SafeSaveSpot(MapPosition(i),threats,PlayerHeight(i));
   Vector2 watch=threats.Count>0?threats[0]:director.Phase==RoundPhase.PostPlant?director.BombPosition:layout.Mid;
   var aim=navigation.VisibleAimPoint(MapPosition(i),watch);if(Vector2.Distance(aim,MapPosition(i))>1)FaceWatch(i,aim-MapPosition(i),dt);
   return true;
  }
 }
}
