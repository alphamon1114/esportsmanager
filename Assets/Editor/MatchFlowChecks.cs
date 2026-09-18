using System;
using System.Reflection;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class MatchFlowChecks
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void End(Prototype game,int winner)
 {
  var outcome=winner==game.CounterTerroristTeam?RoundOutcome.TimeExpired:RoundOutcome.CounterTerroristsEliminated;
  typeof(RoundDirector).GetMethod("End",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game.Director,new object[]{outcome});
  game.AdvanceFrame(.05f);
 }
 public static void Run()
 {
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var game=new GameObject("Match flow checks").AddComponent<Prototype>();game.Initialize();
  Check(game.ChangeStrategy(TeamStrategy.Aggressive),"Lobby strategy denied");
  Check(game.StartMatch()&&game.Stage==MatchStage.Buying&&!game.StartMatch(),"Start match duplication");
  game.AdvanceFrame(Prototype.BuySeconds-.1f);Check(game.Stage==MatchStage.Buying&&game.Combat.Shots==0,"Buying simulated combat");
  game.AdvanceFrame(.11f);Check(game.Stage==MatchStage.Live&&game.RoundMode,"Buy did not auto start");
  Check(game.Director.Strategy==TeamStrategy.Aggressive,"Strategy not applied");
  Check(!game.ChangeStrategy(TeamStrategy.Defensive),"Live strategy changed");
  Check(game.RequestTimeout()&&!game.RequestTimeout()&&game.Stage==MatchStage.Live,"Timeout interrupted live round / duplicate");
  int money=game.MatchState(0).credits;End(game,game.TeamIndexOf(0));
  Check(game.Stage==MatchStage.Result&&game.CompletedRounds==1&&game.MatchState(0).credits==money+3000,"Result / reward not recorded once");
  game.AdvanceFrame(4.9f);Check(game.Stage==MatchStage.Result&&game.CompletedRounds==1,"Result reset too early");
  game.AdvanceFrame(.11f);
  Check(game.Stage==MatchStage.Timeout&&game.TimeoutsRemaining==1&&game.Combat.Shots==0,"Queued timeout missing");
  Check(game.ChangeStrategy(TeamStrategy.Defensive),"Timeout strategy locked");
  game.ResumeTimeout();Check(game.Stage==MatchStage.Buying,"Resume bypassed buying");
  game.AdvanceFrame(Prototype.BuySeconds+.01f);Check(game.Stage==MatchStage.Live&&game.Director.Strategy==TeamStrategy.Defensive,"Next round strategy/start");
  End(game,game.TeamIndexOf(0));game.AdvanceFrame(5.01f);
  Check(game.Stage==MatchStage.Buying,"Automatic reset missing");
  Check(game.RequestTimeout()&&game.TimeoutsRemaining==0,"Second timeout missing");
  game.AdvanceFrame(30.1f);Check(game.Stage==MatchStage.Buying&&!game.RequestTimeout(),"Timeout expiry / limit");
  game.AdvanceFrame(Prototype.BuySeconds+.01f);Check(game.Stage==MatchStage.Live,"Auto resume failed");
  int startingCt=game.CounterTerroristTeam;
  while(game.CompletedRounds<8){End(game,0);game.AdvanceFrame(5.01f);game.AdvanceFrame(Prototype.BuySeconds+.01f);}
  Check(game.CounterTerroristTeam!=startingCt,"Halftime missing");
  End(game,0);game.AdvanceFrame(5.01f);
  Check(game.Stage==MatchStage.Finished&&game.RoundWins(0)==9,"Map continued after winning score");
  game.AdvanceFrame(20);Check(game.CompletedRounds==9,"Finished map kept running");
  Check(!Prototype.WinningScore(9,8)&&Prototype.WinningScore(10,8)&&Prototype.SwitchAfterRound(16)&&Prototype.SwitchAfterRound(17)&&!Prototype.SwitchAfterRound(9),"Deuce/OT rules");
  var state=new PlayerMatchState{credits=6500,equipment=new[]{"usp_s"}};
  MatchEconomy.Buy(state,game.Data.players[0],true);
  Check(state.credits>=0&&state.credits<6500&&Array.IndexOf(state.equipment,game.Data.players[0].weaponPosition=="awper"?"awp":"m4a1_s")>=0,"AI purchase did not spend credits");
  var kept=WeaponCatalog.Equipped(state.equipment).id;
  MatchEconomy.Buy(state,game.Data.players[0],true);Check(WeaponCatalog.Equipped(state.equipment).id==kept,"Owned weapon lost");
  Check(game.StartMatch()&&game.CompletedRounds==0&&game.TimeoutsRemaining==2,"New map did not reset");
  Debug.Log("MATCH_FLOW_ALL_OK 5s result / 7s buy, automatic rounds, rewards, timeout queue/limit/expiry, strategy lock/application, halftime, map end, deuce rules, buying, new map");
 }
}
