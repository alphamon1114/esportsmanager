using System;
namespace FpsManager {
 public sealed partial class CombatSystem {
  public bool StatCanFire(int i){return Alive(i)&&WeaponFor(i).damage>0&&(!AmmoEnabled||magazine[i]>0||spareMagazines[i]>0||reload[i]>0);}
  public float StatReloadWait(int i){return reload[i]>0?reload[i]:AmmoEnabled&&magazine[i]==0&&spareMagazines[i]>0?WeaponFor(i).reloadSeconds:0;}
  public float StatWait(int i){return Math.Max(Math.Max(states[i].cooldown,fireHold[i]),StatReloadWait(i));}
  // Statistical resolution deliberately skips movement, sight, recoil and animation ticks.
  public void StatShot(int shooter,int target,HitRegion region){
   if(!Alive(shooter)||!Alive(target))return;
   if(AmmoEnabled){
    if(reload[shooter]>0){magazine[shooter]=WeaponFor(shooter).magazineSize;reload[shooter]=0;}
    if(magazine[shooter]<=0){if(spareMagazines[shooter]>0){spareMagazines[shooter]--;magazine[shooter]=WeaponFor(shooter).magazineSize;}else return;}
    magazine[shooter]--;reload[shooter]=0;
   }
   Shots++;ApplyHit(shooter,target,region);
  }
 }
 // An event clock, not a live geometry simulation. Each player keeps a target
 // and their own weapon cadence; kills never grant extra turns or a quota.
 public sealed class StatExchange {
  readonly CombatSystem combat;readonly int[] teams,targets;readonly StatBlock[] skills;
  readonly float[] ready;readonly DeterministicRandom rng;float clock;
  public StatExchange(CombatSystem combat,int[] teams,Func<int,StatBlock> stats,DeterministicRandom rng){
   this.combat=combat;this.teams=teams;this.rng=rng;targets=new int[teams.Length];ready=new float[teams.Length];skills=new StatBlock[teams.Length];
   for(int i=0;i<teams.Length;i++){skills[i]=stats(i);targets[i]=-1;ready[i]=combat.StatWait(i)+Reaction(i)+rng.Next01()*1.5f;}
  }
  float Reaction(int i){return (.2f+rng.Next01()*.5f)*(1.25f-skills[i].aim*.005f);}
  public bool Next(out int shooter,out int target,out float elapsed){
   shooter=target=-1;elapsed=0;
   for(int attempt=0;attempt<teams.Length*2;attempt++){
    int next=-1;float at=float.PositiveInfinity;
    for(int i=0;i<teams.Length;i++)if(combat.StatCanFire(i)&&ready[i]<at){next=i;at=ready[i];}
    if(next<0)return false;
    int old=targets[next];
    if(old<0||!combat.Alive(old)){
     int count=0;for(int i=0;i<teams.Length;i++)if(teams[i]!=teams[next]&&combat.Alive(i))count++;
     if(count==0)return false;
     int pick=(int)(rng.NextUInt()%(uint)count);
     for(int i=0;i<teams.Length;i++)if(teams[i]!=teams[next]&&combat.Alive(i)&&pick--==0){targets[next]=i;break;}
     // Reacquiring after a kill/death costs time, including for a surviving star.
     if(old>=0){ready[next]+=Reaction(next);continue;}
    }
    shooter=next;target=targets[next];elapsed=Math.Max(0,at-clock);clock=at;return true;
   }
   return false;
  }
  public void Fire(int shooter,int target){
   var s=skills[shooter];var gun=combat.WeaponFor(shooter);
   float skill=s.aim*.65f+s.composure*.2f+s.movement*.1f+s.utility*.05f;
   float precision=Math.Max(-.15f,Math.Min(.15f,(3-gun.baseSpreadDegrees)*.06f));
   float hit=Math.Max(.1f,Math.Min(.85f,.25f+skill*.005f-skills[target].movement*.001f+precision));
   var region=rng.Next01()>=hit?HitRegion.Miss:rng.Next01()<(gun.id=="awp"?.03f:.06f+.08f*s.aim/100f)?HitRegion.Head:HitRegion.Body;
   combat.StatShot(shooter,target,region);
   ready[shooter]=clock+Math.Max(.01f,gun.fireInterval)+combat.StatReloadWait(shooter);
  }
 }
 public sealed partial class RoundDirector {
  public void AdvanceStatClock(float dt){Clock+=dt;if(PlantedSite>=0){BombTimer=Math.Max(0,BombTimer-dt);if(BombTimer<=0)End(RoundOutcome.BombExploded);}}
  public void FinishStatRound(RoundOutcome result){End(result);}
 }
 public partial class Prototype {
  float StatStrength(int i){
   var s=EffectiveStats(i);var gun=combat.WeaponFor(i);
   float skill=s.aim*.55f+s.movement*.15f+s.composure*.2f+s.utility*.1f;
   float gear=1+Math.Min(2,MatchEconomy.Price(gun.id)/2500f);
   return Math.Max(1,skill*skill)*gear*(.5f+combat.Health(i)/200f)*(teamIndex[i]==ctTeam?1.04f:1);
  }
  void AdvanceStatSkip(){
   if(Stage==MatchStage.Timeout){if(skipMode==2)ResumeTimeout();return;}
   if(Stage==MatchStage.Buying){AdvanceMatch(BuySeconds+.01f);return;}
   if(Stage==MatchStage.Result){StageSeconds=0;AdvanceMatch(.001f);return;}
   if(Stage!=MatchStage.Live)return;
   director.Strategy=TeamStrategy.Balanced;director.DefensePlan=DefenseTactic.Default;director.AttackPlan=AttackTactic.Default;
   var rng=new DeterministicRandom(unchecked(roundSeed^CompletedRounds*7919^combat.Kills*101^0x314159));
   var exchanges=new StatExchange(combat,teamIndex,EffectiveStats,rng);
   // At most a few hundred lightweight exchanges, independent of round duration.
   for(int exchange=0;exchange<512;exchange++){
    int shooter,target;float elapsed;if(!exchanges.Next(out shooter,out target,out elapsed))break;
    director.AdvanceStatClock(elapsed);if(director.Phase==RoundPhase.Ended)break;
    exchanges.Fire(shooter,target);
   }
   float[] power=new float[2];for(int i=0;i<10;i++)if(combat.Alive(i))power[teamIndex[i]]+=StatStrength(i);
   int winner=power[0]<=0?1:power[1]<=0?0:rng.Next01()<power[0]/(power[0]+power[1])?0:1;
   // Exhausted ammunition can end on the objective; do not fabricate extra deaths.
   var outcome=winner==ctTeam?(director.PlantedSite>=0?RoundOutcome.BombDefused:power[1-ctTeam]<=0?RoundOutcome.TerroristsEliminated:RoundOutcome.TimeExpired)
    :(power[ctTeam]<=0?RoundOutcome.CounterTerroristsEliminated:RoundOutcome.BombExploded);
   if(director.Phase!=RoundPhase.Ended)director.FinishStatRound(outcome);CompleteRoundOnce();Stage=MatchStage.Result;StageSeconds=0;BeginRecovery();
  }
 }
}
