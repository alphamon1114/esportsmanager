using System;
using System.Collections.Generic;
namespace FpsManager {
 public sealed partial class CombatSystem {
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
   var a=new List<int>();var b=new List<int>();
   // At most a few hundred lightweight exchanges, independent of round duration.
   for(int exchange=0;exchange<512;exchange++){
    a.Clear();b.Clear();for(int i=0;i<10;i++)if(combat.Alive(i)){if(teamIndex[i]==0)a.Add(i);else b.Add(i);}
    if(a.Count==0||b.Count==0)break;
    director.AdvanceStatClock(.5f+rng.Next01());if(director.Phase==RoundPhase.Ended)break;
    int x=a[(int)(rng.NextUInt()%(uint)a.Count)],y=b[(int)(rng.NextUInt()%(uint)b.Count)];
    float sx=StatStrength(x),sy=StatStrength(y);int shooter=rng.Next01()<sx/(sx+sy)?x:y,target=shooter==x?y:x;
    combat.StatShot(shooter,target,rng.Next01()<(combat.WeaponFor(shooter).id=="awp"?.03f:.06f+.08f*EffectiveStats(shooter).aim/100f)?HitRegion.Head:HitRegion.Body);
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
