using System;using UnityEngine;
namespace FpsManager {
 public static class TacticMatchup {
  // CT payoff. Enum order: Default/Forward/Stack vs Default/Rush/MidPlay.
  static readonly int[,] advantage={{1,0,-1},{0,-1,1},{-1,1,0}};
  public static int ForDefense(DefenseTactic ct,AttackTactic t){return advantage[(int)ct,(int)t];}
 }
 public sealed class AiCoach {
  public DefenseTactic Defense {get;private set;}
  public AttackTactic Attack {get;private set;}
  public int TimeoutsRemaining {get;private set;}=2;
  public bool Initialized {get;private set;}
  readonly float[,] belief={{1,1,1},{1,1,1}};
  readonly float[] roundEvidence=new float[3];int evidenceSide=-1;
  public float Evidence(int side,int plan){return belief[side,plan];}
  public void Observe(bool ownCt,int[] perSite,int mid,int forward,int seen,float clock){
   if(clock>25||seen<2)return;int side=ownCt?1:0;if(evidenceSide!=side){Array.Clear(roundEvidence,0,3);evidenceSide=side;}
   int plan=-1;float confidence=0;
   if(ownCt){if(mid>=3){plan=(int)AttackTactic.MidPlay;confidence=.9f;}else if(Math.Max(perSite[0],perSite[1])>=4&&clock<20){plan=(int)AttackTactic.Rush;confidence=.8f;}else if(perSite[0]>0&&perSite[1]>0&&seen>=3){plan=(int)AttackTactic.Default;confidence=.6f;}}
   else {if(Math.Max(perSite[0],perSite[1])>=4){plan=(int)DefenseTactic.Stack;confidence=.9f;}else if(forward>=2){plan=(int)DefenseTactic.Forward;confidence=.8f;}else if(perSite[0]>=1&&perSite[1]>=1&&seen>=3){plan=(int)DefenseTactic.Default;confidence=.6f;}}
   if(plan>=0)roundEvidence[plan]=Mathf.Max(roundEvidence[plan],confidence);
  }
  public void EndRound(){if(evidenceSide>=0)for(int p=0;p<3;p++)belief[evidenceSide,p]=1+(belief[evidenceSide,p]-1)*.8f+roundEvidence[p];Array.Clear(roundEvidence,0,3);evidenceSide=-1;}
  public float Score(bool ct,int plan,int rifles,int utility,int snipers){
   float sum=0,total=0;int side=ct?1:0;for(int e=0;e<3;e++){total+=belief[side,e];sum+=belief[side,e]*(ct?TacticMatchup.ForDefense((DefenseTactic)plan,(AttackTactic)e):-TacticMatchup.ForDefense((DefenseTactic)e,(AttackTactic)plan));}
   float gear=0;if(ct){if(plan==(int)DefenseTactic.Forward)gear=rifles>=3?.2f:-.4f;if(plan==(int)DefenseTactic.Stack&&rifles<3)gear=.25f;if(plan==0&&snipers>0)gear=.15f;}
   else {if(plan==(int)AttackTactic.Rush)gear=utility>=4?.25f:-.25f;if(plan==(int)AttackTactic.MidPlay)gear=rifles>=3?.15f:-.35f;if(plan==0&&snipers>0)gear=.2f;}
   return sum/total+gear;
  }
  public static float TimeoutChance(int losses,int enemyScore,int ownScore){
   float chance=losses>=3?Mathf.Min(.75f,.4f+(losses-3)*.1f):0;
   if(enemyScore>=7&&enemyScore>=ownScore)chance=Mathf.Max(chance,.35f);
   if(enemyScore>=8&&enemyScore>ownScore)chance=Mathf.Max(chance,.65f);
   return chance;
  }
  public bool TryTimeout(int losses,int enemyScore,int ownScore,int seed){
   if(TimeoutsRemaining<=0)return false;var rng=new DeterministicRandom(seed);
   if(rng.Next01()>=TimeoutChance(losses,enemyScore,ownScore))return false;
   TimeoutsRemaining--;return true;
  }
  int Best(bool ct,int rifles,int utility,int snipers,int seed){var rng=new DeterministicRandom(seed);int best=0;float value=float.MinValue;for(int p=0;p<3;p++){float score=Score(ct,p,rifles,utility,snipers)+rng.Next01()*.3f;if(score>value){value=score;best=p;}}return best;}
  public bool Prepare(bool ct,int rifles,int utility,int snipers,int seed,bool sharedTimeout=false){
   if(!Initialized){Defense=(DefenseTactic)Best(true,rifles,utility,snipers,seed);Attack=(AttackTactic)Best(false,rifles,utility,snipers,seed^777);Initialized=true;return false;}
   if(!sharedTimeout)return false;int side=ct?1:0;float intel=0;for(int p=0;p<3;p++)intel+=belief[side,p]-1;
   if(intel<.5f)return false;int next=Best(ct,rifles,utility,snipers,seed),current=ct?(int)Defense:(int)Attack;
   if(next==current||Score(ct,next,rifles,utility,snipers)-Score(ct,current,rifles,utility,snipers)<.12f)return false;
   if(ct)Defense=(DefenseTactic)next;else Attack=(AttackTactic)next;return true;
  }
 }
 public partial class Prototype {
  AiCoach enemyCoach=new AiCoach();bool coachPrepared;int coachLossStreak;
  public bool OpponentTimeout {get;private set;}
  public int OpponentTimeoutsRemaining {get{return enemyCoach.TimeoutsRemaining;}}
  DefenseTactic DefenseFor(int team){if(FastForwarding)return DefenseTactic.Default;return team==AlliedTeamIndex?(ComputerOnlyMatch?spectatorCoach.Defense:DefensePlan):enemyCoach.Defense;}
  AttackTactic AttackFor(int team){if(FastForwarding)return AttackTactic.Default;return team==AlliedTeamIndex?(ComputerOnlyMatch?spectatorCoach.Attack:AttackPlan):enemyCoach.Attack;}
  void ObserveCoach(){
   ObserveCoachTeam(1-AlliedTeamIndex,enemyCoach);if(ComputerOnlyMatch)ObserveCoachTeam(AlliedTeamIndex,spectatorCoach);
  }
  void ObserveCoachTeam(int viewer,AiCoach coach){
   if(!AutomaticMatch||director.PlantedSite>=0)return;int[] sitesSeen=new int[2];int mid=0,forward=0,seen=0;
   for(int e=0;e<10;e++){if(teamIndex[e]==viewer)continue;var k=vision.Knowledge(viewer,e);if(!k.known||k.anonymous||k.age>1)continue;seen++;var p=k.lastKnownPosition;
    float a=Vector2.Distance(p,layout.Sites[0]),b=Vector2.Distance(p,layout.Sites[1]);if(Mathf.Min(a,b)<24)sitesSeen[a<b?0:1]++;
    if(Vector2.Distance(p,layout.Mid)<17)mid++;
    if(Mathf.Min(a,b)>20&&Vector2.Distance(p,layout.AttackerSpawn)<55)forward++;
   }
   coach.Observe(viewer==ctTeam,sitesSeen,mid,forward,seen,director.Clock);
  }
  void CoachSharedTimeout(){coachPrepared=false;PrepareCoach(true);coachPrepared=true;}
  bool PrepareCoach(bool sharedTimeout=false){
   if(coachPrepared)return false;coachPrepared=true;int team=1-AlliedTeamIndex,rifles=0,utility=0,snipers=0;
   for(int i=0;i<10;i++)if(teamIndex[i]==team){if(Array.IndexOf(WeaponCatalog.Ids,WeaponCatalog.Equipped(matchState[i].equipment).id)>=11)rifles++;utility+=MatchEconomy.UtilityCount(matchState[i]);if(Array.IndexOf(matchState[i].equipment,"awp")>=0)snipers++;}
      if(!enemyCoach.Initialized){enemyCoach.Prepare(team==ctTeam,rifles,utility,snipers,unchecked(roundSeed^98317));return false;}
   enemyCoach.Prepare(team==ctTeam,rifles,utility,snipers,unchecked(roundSeed^98317),true);
   if(!sharedTimeout&&!enemyCoach.TryTimeout(coachLossStreak,roundWins[AlliedTeamIndex],roundWins[team],unchecked(roundSeed^472391)))return false;
   enemyCoach.Prepare(team==ctTeam,rifles,utility,snipers,unchecked(roundSeed^98317),true);
   if(sharedTimeout)return false;OpponentTimeout=true;buyRemaining=0;Stage=MatchStage.Timeout;StageSeconds=30;BeginTimeoutTalks();return true;
  }
 }
}

