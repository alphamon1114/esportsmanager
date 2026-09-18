using System;
using System.Collections.Generic;
namespace FpsManager
{
 public enum MatchStage { Lobby, Buying, Live, Result, Timeout, Finished }
 public enum TeamStrategy { Balanced, Aggressive, Defensive }
 public partial class Prototype
 {
  public const float BuySeconds=7f;
  public MatchStage Stage { get; private set; }
  public TeamStrategy Strategy { get { return (TeamStrategy)(AlliedTeamIndex==ctTeam?(int)DefensePlan:(int)AttackPlan); } }
  public float StageSeconds { get; private set; }
  public int TimeoutsRemaining { get; private set; } = 2;
  public bool TimeoutPending { get; private set; }
  public bool AutomaticMatch { get; private set; }
  public bool CanChangeStrategy { get { return !FastForwarding&&!ComputerOnlyMatch&&(Stage==MatchStage.Lobby||Stage==MatchStage.Buying&&StageSeconds>0||Stage==MatchStage.Timeout&&buyRemaining>0); } }
  bool roundScored;
  int purchasedPlayers;
  readonly BuyPlan[] buyPlans=new BuyPlan[2];
  bool[] ecoBuyers=new bool[10];
  public BuyPlan TeamBuyPlan(int team){return buyPlans[team];}
  float buyRemaining;
  public bool ChangeStrategy(TeamStrategy value)
  {
   if(!CanChangeStrategy)return false;
   if(AlliedTeamIndex==ctTeam)DefensePlan=(DefenseTactic)(int)value;else AttackPlan=(AttackTactic)(int)value;return true;
  }
  public bool StartMatch()
  {
   if(Tournament!=null&&!startingTournamentMap)return false;
   if(AutomaticMatch&&Stage!=MatchStage.Finished)return false;
   spectatorCoach=new AiCoach();spectatorPrepared=false;spectatorLossStreak=0;enemyCoach=new AiCoach();coachLossStreak=0;OpponentTimeout=false;coachPrepared=false;AutomaticMatch=true;Stage=MatchStage.Buying;TimeoutsRemaining=2;TimeoutPending=false;
   ResetMapCondition();
   Statistics=new MatchStatistics();resultSide=0;ResetRecoveryTrades();
   CompletedRounds=0;LastRoundOutcome=RoundOutcome.None;LastRoundWinner=-1;
   Array.Clear(roundWins,0,2);roundWinners.Clear();killFeed.Clear();roundScored=false;
   PlaceTeams();ShuffleSpawnPlayers();PrepareIglOrders();
   for(int i=0;i<10;i++)
   {
    matchState[i].defuseKit=false;matchState[i].credits=800;matchState[i].armor=0;matchState[i].helmet=false;combat.BindProtection(i,matchState[i]);
    matchState[i].equipment=new[]{teamIndex[i]==ctTeam?"usp_s":"glock_18"};combat.SetKnife(i,true);
    combat.Equip(i,WeaponCatalog.Equipped(matchState[i].equipment));
   }
   StartBuying();return true;
  }
  void CompleteRoundOnce()
  {
   if(roundScored||!roundMode||director.Phase!=RoundPhase.Ended)return;
   if(AutomaticMatch){Statistics.End(combat);enemyCoach.EndRound();if(ComputerOnlyMatch)spectatorCoach.EndRound();}
   roundScored=true;LastRoundOutcome=director.Outcome;CompletedRounds++;RecordRoundResult(director.Outcome);if(AutomaticMatch)coachLossStreak=LastRoundWinner==AlliedTeamIndex?coachLossStreak+1:0;if(ComputerOnlyMatch)spectatorLossStreak=LastRoundWinner!=AlliedTeamIndex?spectatorLossStreak+1:0;
   if(AutomaticMatch)
    for(int i=0;i<10;i++)matchState[i].credits=Math.Min(10000,matchState[i].credits+(teamIndex[i]==LastRoundWinner?3000:2400));
  }
  public static bool WinningScore(int a,int b) { return Math.Max(a,b)>=9&&Math.Abs(a-b)>=2; }
  public static bool SwitchAfterRound(int completed) { return completed==8||completed>=16; }
  public bool RequestTimeout()
  {
   if(FastForwarding||AwaitingMapStart||ComputerOnlyMatch||!AutomaticMatch||TimeoutsRemaining<=0||TimeoutPending||Stage==MatchStage.Timeout||Stage==MatchStage.Finished)return false;
   TimeoutPending=true;
   if(Stage==MatchStage.Buying)ActivateTimeout();
   return true;
  }
  void ActivateTimeout()
  {
   buyRemaining=StageSeconds;Stage=MatchStage.Timeout;StageSeconds=30;
   TimeoutPending=false;TimeoutsRemaining--;CoachSharedTimeout();BeginTimeoutTalks();
  }
  public void ResumeTimeout()
  {
   if(Stage!=MatchStage.Timeout)return;OpponentTimeout=false;
   Stage=MatchStage.Buying;StageSeconds=buyRemaining;
  }
  void StartBuying()
  {
   spectatorPrepared=false;coachPrepared=false;Stage=MatchStage.Buying;StageSeconds=BuySeconds;purchasedPlayers=0;buySupportPlanned=false;
   for(int i=0;i<10;i++)if(!HasSecondary(i))
   {var items=new List<string>(matchState[i].equipment);items.Add(teamIndex[i]==ctTeam?"usp_s":"glock_18");matchState[i].equipment=items.ToArray();}
   StartRecoveryTrades();
   RefreshBuyPlans();
   for(int i=0;i<10;i++)combat.Equip(i,WeaponCatalog.Equipped(matchState[i].equipment));
   PlanKits();
   if(TimeoutPending)ActivateTimeout();
  }
  void RefreshBuyPlans()
  {
   Array.Clear(ecoBuyers,0,ecoBuyers.Length);
   for(int team=0;team<2;team++)
   {
    buyPlans[team]=MatchEconomy.Choose(matchState,Data.players,teamIndex,team,team==ctTeam,CompletedRounds==0,roundWins[1-team]>=8&&roundWins[1-team]>roundWins[team]);
    if(buyPlans[team]==BuyPlan.Eco){var picks=MatchEconomy.EcoBuyers(matchState,Data.players,teamIndex,team,CompletedRounds,team==ctTeam);for(int i=0;i<10;i++)ecoBuyers[i]|=picks[i];}
   }
  }
  void AdvanceMatch(float delta)
  {
   if(delta<=0||Stage==MatchStage.Finished||Stage==MatchStage.Lobby)return;
   if(Stage==MatchStage.Live)
   {
    SimulateMovement(delta);
    if(director.Phase==RoundPhase.Ended){CompleteRoundOnce();Stage=MatchStage.Result;StageSeconds=5;BeginRecovery();}
    return;
   }
   if(Stage==MatchStage.Result)TickRoundRecovery(UnityEngine.Mathf.Min(delta,StageSeconds));
   StageSeconds=UnityEngine.Mathf.Max(0,StageSeconds-delta);
   if(Stage==MatchStage.Buying)
   {
    DeliverSupport(delta);
    if(PendingRecoveryDelivery())return;
    if(!buySupportPlanned){RefreshBuyPlans();PlanAceSupport();PlanKits();buySupportPlanned=true;}
    int due=Math.Min(10,(int)((BuySeconds-StageSeconds)/(BuySeconds/10f)));
    if(StageSeconds<=0)due=10;
    while(purchasedPlayers<due)
    {
     int i=purchasedPlayers++;
     MatchEconomy.Buy(matchState[i],Data.players[i],teamIndex[i]==ctTeam,buyPlans[teamIndex[i]],ecoBuyers[i],kitBuyers[i]);
     combat.Equip(i,WeaponCatalog.Equipped(matchState[i].equipment));
    }
   }
   if(StageSeconds>0)return;
   if(Stage==MatchStage.Timeout){ResumeTimeout();return;}
   if(Stage==MatchStage.Result)
   {
    if(WinningScore(roundWins[0],roundWins[1])){Stage=MatchStage.Finished;return;}
    if(SwitchAfterRound(CompletedRounds))ctTeam=1-ctTeam;
    PreserveRecoveryDeliveries();PrepareNextRound();StartBuying();return;
   }
   if(Stage==MatchStage.Buying){if(FastForwarding){BeginRound();Stage=MatchStage.Live;return;}if(PrepareCoach()){PrepareSpectatorCoach(true);return;}if(PrepareSpectatorCoach())return;BeginRound();Stage=MatchStage.Live;}
  }
 }
}
