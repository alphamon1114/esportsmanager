using System;using UnityEngine;
namespace FpsManager {
 public enum DefenseTactic { Default, Forward, Stack }
 public enum AttackTactic { Default, Rush, MidPlay }
 public partial class Prototype {
  public DefenseTactic DefensePlan {get;private set;}
  public AttackTactic AttackPlan {get;private set;}
  public bool ChangeDefense(DefenseTactic value){if(!CanChangeStrategy||AlliedTeamIndex!=ctTeam)return false;DefensePlan=value;return true;}
  public bool ChangeAttack(AttackTactic value){if(!CanChangeStrategy||AlliedTeamIndex==ctTeam)return false;AttackPlan=value;return true;}
    void KeepMidCarrierWithGroup(){
   if(!AutomaticMatch||AttackFor(1-ctTeam)!=AttackTactic.MidPlay||director.Carrier<0)return;
   int carrier=director.Carrier;if(assignments[carrier]==1)return;
   for(int i=0;i<10;i++)if(teamIndex[i]==teamIndex[carrier]&&assignments[i]==1){int old=assignments[carrier];assignments[carrier]=1;assignments[i]=old;homeAnchor[carrier]=tZones[1];homeAnchor[i]=tZones[old];destinations[carrier]=homeAnchor[carrier];destinations[i]=homeAnchor[i];break;}
  }
  bool StepForwardAdvance(int i,PlayerObjective order,float dt){
   if(!order.forwardAdvance||Vector2.Distance(MapPosition(i),order.destination)<3||combat.Engaging(i)||HasDirectFight(i))return false;
   Vector2 cue;if(ImmediateCue(i,out cue))return false;
   // March through our own quiet approaches, but resume careful entry near reported threats.
   for(int e=0;e<10;e++)if(teamIndex[e]!=teamIndex[i]){var info=vision.Knowledge(teamIndex[i],e);if(info.known&&info.age<4&&Vector2.Distance(MapPosition(i),info.lastKnownPosition)<20)return false;}
   peeking[i].Cancel();clutchSearch[i].Cancel();autonomy.Walking[i]=false;
   MoveTo(i,order.destination);var before=MapPosition(i);
   if(routeValid[i]&&routeSteps[i]<routes[i].Count)StepRoute(i,dt);
   moving[i]=Vector2.Distance(before,MapPosition(i))>.001f;arrived[i]=!moving[i];AimWhileMoving(i,order,dt);return true;
  }
  int OpeningSite(){var rng=new DeterministicRandom(roundSeed);return (int)(rng.NextUInt()%2);}
  int StackSite(){var rng=new DeterministicRandom(unchecked(roundSeed^9137));return (int)(rng.NextUInt()%2);}
  int[] SideSlots(int team){
   bool ct=team==ctTeam;int site=OpeningSite()==0?2:0;
   if(ct)return DefenseFor(team)==DefenseTactic.Stack?new[]{StackSite()==0?2:0,StackSite()==0?2:0,StackSite()==0?2:0,StackSite()==0?2:0,StackSite()==0?2:0}:new[]{0,0,1,2,2};
   var plan=AttackFor(team);
   if(plan==AttackTactic.Rush)return new[]{site,site,site,site,site};
   if(plan==AttackTactic.MidPlay)return new[]{1,1,1,0,2};
   return new[]{site,site,site,1,2-site};
  }
 }
 public sealed partial class RoundDirector {
  public bool SidePlansEnabled;
  public DefenseTactic DefensePlan;public AttackTactic AttackPlan;
  int stackSite,midPhase;float midPhaseAt;bool flankWon;PlayerData[] planPlayers;
  readonly bool[] retreating=new bool[10];readonly float[] rushReadyAt=new float[10];
  Vector2 midPoint {get{return layout.Mid;}}
  public int MidPhase {get{return midPhase;}}
  public void ConfigureSidePlans(bool enabled,DefenseTactic defense,AttackTactic attack,int stack,PlayerData[] players){SidePlansEnabled=enabled;DefensePlan=defense;AttackPlan=attack;stackSite=stack;planPlayers=players;midPhase=0;midPhaseAt=Clock;flankWon=false;Array.Clear(retreating,0,10);Array.Clear(rushReadyAt,0,10);if(enabled&&attack!=AttackTactic.Default)lurker=-1;}
  public void PlanKill(int killer,int victim,Vector2 location,int[] teams,int ct){
   if(!SidePlansEnabled||PlantedSite>=0)return;
   if(killer==lurker){flankWon=true;lurkStep=0;lurkStarted=0;}
   if(AttackPlan!=AttackTactic.MidPlay)return;
   if(midPhase==0&&Vector2.Distance(location,midPoint)<22){midPhase=teams[killer]!=ct?1:3;midPhaseAt=Clock;}
   else if(midPhase==1){if(teams[killer]!=ct){TargetSite=NearestSite(location);midPhase=2;}else midPhase=3;midPhaseAt=Clock;}
  }
  bool MidContact(int viewer,int[] teams,VisionSystem vision){for(int e=0;e<count;e++)if(teams[e]!=viewer){var k=vision.Knowledge(viewer,e);if(k.known&&k.age<3&&Vector2.Distance(k.lastKnownPosition,midPoint)<22)return true;}return false;}
  PlayerObjective PlanOrder(PlayerTask task,Vector2 target,Vector2 watch,bool careful=false){return new PlayerObjective{valid=true,task=task,destination=target,watch=watch,cautious=careful};}
  void UpdateSidePlans(Vector2[] positions,int[] teams,int ct,Vector2[] anchors,VisionSystem vision,CombatSystem combat){
   if(!SidePlansEnabled||PlantedSite>=0)return;
   if(AttackPlan==AttackTactic.MidPlay&&midPhase<2){
    if(midPhase==0){int arrived=0;bool danger=false;for(int i=0;i<count;i++){if(teams[i]!=ct&&combat.Alive(i)&&Vector2.Distance(positions[i],midPoint)<10)arrived++;if(teams[i]==ct){var k=vision.Knowledge(1-ct,i);if(k.known&&k.age<3&&Vector2.Distance(k.lastKnownPosition,midPoint)<20)danger=true;}}
     if(arrived>=2&&!danger){if(Clock-midPhaseAt>6){midPhase=1;midPhaseAt=Clock;}}else midPhaseAt=Clock;
     if(Clock>35){midPhase=3;midPhaseAt=Clock;}
    }else if(Clock-midPhaseAt>10){midPhase=3;midPhaseAt=Clock;}
   }
   if(AttackPlan==AttackTactic.MidPlay&&midPhase==3&&lurker<0)for(int j=0;j<count;j++)if(teams[j]!=ct&&j!=Carrier&&combat.Alive(j)&&planPlayers[j].riflerRole=="anchor_lurker"){lurker=j;lurkStep=0;break;}
   for(int i=0;i<count;i++){
    if(!combat.Alive(i))continue;var current=objectives[i];
    if(current.task==PlayerTask.RecoverBomb||current.task==PlayerTask.Defuse||PlantProgress>0&&Carrier==i)continue;
    if(teams[i]==ct){
          int home=HomeSite(anchors[i]);
     if(current.task==PlayerTask.FallBack||current.task==PlayerTask.Regroup)retreating[i]=true;
     if(retreating[i]&&home>=0){int allies=0;for(int j=0;j<count;j++)if(teams[j]==ct&&combat.Alive(j)&&Vector2.Distance(positions[j],layout.Staging[home])<12)allies++;
      if(Vector2.Distance(positions[i],layout.Staging[home])>5){var retreat=PlanOrder(PlayerTask.FallBack,layout.Staging[home],layout.Sites[home]-positions[i],true);retreat.disengage=true;objectives[i]=retreat;continue;}
      if(allies<2&&combat.LivingCount(teams,ct)>1){objectives[i]=PlanOrder(PlayerTask.Regroup,layout.Staging[home],layout.Sites[home]-positions[i],true);continue;}
      objectives[i]=PlanOrder(PlayerTask.Retake,HoldSpot(home,slot[i]),layout.Sites[home]-positions[i],true);retreating[i]=false;continue;
     }
     if(DefensePlan==DefenseTactic.Stack){int threat=-1;for(int s=0;s<layout.SiteCount;s++)if(s!=stackSite&&BackupThreat(s,ct,teams,vision)>=2)threat=s;
      if(threat>=0){int gathered=0;for(int j=0;j<count;j++)if(teams[j]==ct&&combat.Alive(j)&&Vector2.Distance(positions[j],layout.Staging[threat])<12)gathered++;objectives[i]=PlanOrder(PlayerTask.Retake,gathered>=2?HoldSpot(threat,slot[i]):layout.Staging[threat],layout.Sites[threat]-positions[i],true);}
      else if(current.task!=PlayerTask.FallBack&&current.task!=PlayerTask.Regroup&&current.task!=PlayerTask.Trade){
       int n=slot[i]%5;var post=layout.StackPost!=null?layout.StackPost[stackSite][n]:HoldSpot(stackSite,n);
       var watch=layout.StackPeek!=null?layout.StackPeek[stackSite][n]:layout.Approaches[stackSite][n%layout.Approaches[stackSite].Length];
       var hold=PlanOrder(PlayerTask.DefendSite,post,watch-positions[i]);hold.stackHold=true;hold.stackHidden=n>=2;hold.hasCover=true;hold.cover=post;objectives[i]=hold;
      }
     }else if(DefensePlan==DefenseTactic.Forward&&planPlayers[i].weaponPosition!="awper"&&supportSite[i]<0&&current.task==PlayerTask.DefendSite){
      int rank=0;for(int j=0;j<i;j++)if(teams[j]==ct&&HomeSite(anchors[j])==home&&planPlayers[j].weaponPosition!="awper")rank++;
      var target=home<0?midPoint:layout.Approaches[home][rank%layout.Approaches[home].Length];
      var advance=PlanOrder(PlayerTask.Patrol,target,target-positions[i]);advance.forwardAdvance=true;objectives[i]=advance;
     }else if(DefensePlan==DefenseTactic.Default&&home>=0&&supportSite[i]<0&&current.task==PlayerTask.DefendSite&&BackupThreat(home,ct,teams,vision)==0&&BackupThreat(1-home,ct,teams,vision)>=2){
      var target=layout.Approaches[home][slot[i]%layout.Approaches[home].Length];objectives[i]=PlanOrder(PlayerTask.Patrol,target,target-positions[i],true);
     }
    }else{
     if(AttackPlan==AttackTactic.MidPlay&&midPhase<2){
      // Three mid players, one watcher at each site. Carrier stays with mid group.
      bool guard=Vector2.Distance(anchors[i],new Vector2(45,61))>10&&i!=Carrier;
      if(midPhase==0){var target=guard?anchors[i]:midPoint;objectives[i]=PlanOrder(guard?PlayerTask.HoldSite:PlayerTask.PushSite,target,guard?layout.Sites[NearestSite(target)]-positions[i]:layout.Staging[0]-positions[i],guard);}
      else {int site=NearestSite(anchors[i]);objectives[i]=guard?PlanOrder(PlayerTask.PushSite,HoldSpot(site,slot[i]),layout.Staging[site]-positions[i]):PlanOrder(PlayerTask.HoldSite,midPoint,layout.Staging[0]-positions[i],true);}
          }else if(AttackPlan==AttackTactic.Rush){
      var entry=layout.Approaches[TargetSite][0];
      if(rushReadyAt[i]==0&&Vector2.Distance(positions[i],entry)<6)rushReadyAt[i]=Clock;
      if(rushReadyAt[i]==0||Clock-rushReadyAt[i]<2){var prep=PlanOrder(PlayerTask.PushSite,entry,layout.Sites[TargetSite]-positions[i]);prep.plannedExecute=true;prep.utilityTarget=layout.Sites[TargetSite];prep.utilityBlockTarget=layout.Staging[TargetSite];objectives[i]=prep;continue;}
      objectives[i]=PlanOrder(i==Carrier?PlayerTask.PlantBomb:PlayerTask.PushSite,i==Carrier?layout.Sites[TargetSite]:HoldSpot(TargetSite,slot[i]),layout.Staging[TargetSite]-positions[i]);}
     else if(AttackPlan==AttackTactic.Default||midPhase==3){
      if(i==lurker&&!flankWon&&Clock<35){int opposite=1-TargetSite;objectives[i]=PlanOrder(PlayerTask.Lurk,layout.Approaches[opposite][0],layout.Sites[opposite]-positions[i],true);}
      else if(i!=lurker&&planPlayers[i].weaponPosition=="awper"&&i!=Carrier&&(Clock<18||MidContact(teams[i],teams,vision)))objectives[i]=PlanOrder(PlayerTask.HoldSite,midPoint,layout.Staging[0]-positions[i],true);
      else if(i!=lurker)objectives[i]=PlanOrder(i==Carrier?PlayerTask.PlantBomb:PlayerTask.PushSite,i==Carrier?layout.Sites[TargetSite]:HoldSpot(TargetSite,slot[i]),layout.Staging[TargetSite]-positions[i]);
     }
    }
   }
  }
 }
}
