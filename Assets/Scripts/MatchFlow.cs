using System;
using System.Collections.Generic;
namespace FpsManager
{
 public enum MatchStage { Lobby, Buying, Live, Result, Timeout, Finished }
 public enum TeamStrategy { Balanced, Aggressive, Defensive }
 public partial class Prototype
 {
  public MatchStage Stage { get; private set; }
  public TeamStrategy Strategy { get; private set; }
  public float StageSeconds { get; private set; }
  public int TimeoutsRemaining { get; private set; } = 2;
  public bool TimeoutPending { get; private set; }
  public bool AutomaticMatch { get; private set; }
  public bool CanChangeStrategy { get { return Stage==MatchStage.Lobby||Stage==MatchStage.Timeout; } }
  bool roundScored;
  int purchasedPlayers;
  readonly BuyPlan[] buyPlans=new BuyPlan[2];
  bool[] ecoBuyers=new bool[10];
  public BuyPlan TeamBuyPlan(int team){return buyPlans[team];}
  float buyRemaining;
  public bool ChangeStrategy(TeamStrategy value)
  {
   if(!CanChangeStrategy)return false;
   Strategy=value;return true;
  }
  public bool StartMatch()
  {
   if(AutomaticMatch&&Stage!=MatchStage.Finished)return false;
   AutomaticMatch=true;Stage=MatchStage.Buying;TimeoutsRemaining=2;TimeoutPending=false;
   Statistics=new MatchStatistics();resultSide=0;
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
   if(AutomaticMatch)Statistics.End(combat);
   roundScored=true;LastRoundOutcome=director.Outcome;CompletedRounds++;RecordRoundResult(director.Outcome);
   if(AutomaticMatch)
    for(int i=0;i<10;i++)matchState[i].credits=Math.Min(10000,matchState[i].credits+(teamIndex[i]==LastRoundWinner?3000:2400));
  }
  public static bool WinningScore(int a,int b) { return Math.Max(a,b)>=9&&Math.Abs(a-b)>=2; }
  public static bool SwitchAfterRound(int completed) { return completed==8||completed>=16; }
  public bool RequestTimeout()
  {
   if(!AutomaticMatch||TimeoutsRemaining<=0||TimeoutPending||Stage==MatchStage.Timeout||Stage==MatchStage.Finished)return false;
   TimeoutPending=true;
   if(Stage==MatchStage.Buying)ActivateTimeout();
   return true;
  }
  void ActivateTimeout()
  {
   buyRemaining=StageSeconds;Stage=MatchStage.Timeout;StageSeconds=30;
   TimeoutPending=false;TimeoutsRemaining--;
  }
  public void ResumeTimeout()
  {
   if(Stage!=MatchStage.Timeout)return;
   Stage=MatchStage.Buying;StageSeconds=buyRemaining;
  }
  void StartBuying()
  {
   Stage=MatchStage.Buying;StageSeconds=3;purchasedPlayers=0;
   for(int i=0;i<10;i++)if(WeaponCatalog.Equipped(matchState[i].equipment).id=="unarmed")
   {var items=new List<string>(matchState[i].equipment);items.Add(teamIndex[i]==ctTeam?"usp_s":"glock_18");matchState[i].equipment=items.ToArray();}
   Array.Clear(ecoBuyers,0,ecoBuyers.Length);
   for(int team=0;team<2;team++)
   {
    buyPlans[team]=MatchEconomy.Choose(matchState,Data.players,teamIndex,team,team==ctTeam,CompletedRounds==0,roundWins[1-team]>=8&&roundWins[1-team]>roundWins[team]);
    if(buyPlans[team]==BuyPlan.Eco){var picks=MatchEconomy.EcoBuyers(matchState,Data.players,teamIndex,team,CompletedRounds,team==ctTeam);for(int i=0;i<10;i++)ecoBuyers[i]|=picks[i];}
   }
   for(int i=0;i<10;i++)combat.Equip(i,WeaponCatalog.Equipped(matchState[i].equipment));
   PlanAceSupport();PlanKits();
   if(TimeoutPending)ActivateTimeout();
  }
  void AdvanceMatch(float delta)
  {
   if(delta<=0||Stage==MatchStage.Finished||Stage==MatchStage.Lobby)return;
   if(Stage==MatchStage.Live)
   {
    SimulateMovement(delta);
    if(director.Phase==RoundPhase.Ended){CompleteRoundOnce();Stage=MatchStage.Result;StageSeconds=3;}
    return;
   }
   StageSeconds=UnityEngine.Mathf.Max(0,StageSeconds-delta);
   if(Stage==MatchStage.Buying)
   {
    DeliverSupport(delta);
    int due=Math.Min(10,(int)((3-StageSeconds)/.3f));
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
    PrepareNextRound();StartBuying();return;
   }
   if(Stage==MatchStage.Buying){BeginRound();Stage=MatchStage.Live;}
  }
 }
}
