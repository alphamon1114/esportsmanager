using System;
using System.Reflection;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class MatchHudChecks
{
 public static void Run()
 {
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var game=new GameObject("HUD checks").AddComponent<Prototype>(); game.Initialize(); game.BeginRound();
  var fire=typeof(CombatSystem).GetMethod("Fire",BindingFlags.Instance|BindingFlags.NonPublic);
  var gun=WeaponCatalog.Find("awp"); gun.baseSpreadDegrees=0; gun.recoilPerShot=0; game.Combat.Equip(0,gun);
  fire.Invoke(game.Combat,new object[]{0,5,0f,10f,100});
  if(game.KillFeedCount!=1||game.KillFeedAt(0).killer!=0||game.KillFeedAt(0).victim!=5||game.KillFeedAt(0).weapon!="awp"||game.KillFeedAt(0).region!=game.Combat.LastHit(0)) throw new Exception("Kill feed did not capture actual kill");
  game.PrepareNextRound();
  if(game.RoundWins(0)+game.RoundWins(1)!=0||game.KillFeedCount!=1||game.KillFeedAt(0).weapon!="awp") throw new Exception("Aborted round scored or lost kill snapshot");
  var end=typeof(RoundDirector).GetMethod("End",BindingFlags.Instance|BindingFlags.NonPublic);
  int total=0;
  foreach(var outcome in new[]{RoundOutcome.BombDefused,RoundOutcome.TerroristsEliminated,RoundOutcome.TimeExpired,RoundOutcome.BombExploded,RoundOutcome.CounterTerroristsEliminated})
  {
   game.SwapPreviewSides(); game.BeginRound();
   if(game.KillFeedCount!=0) throw new Exception("New round retained old kill feed");
   int winner=(outcome==RoundOutcome.BombDefused||outcome==RoundOutcome.TerroristsEliminated||outcome==RoundOutcome.TimeExpired)?game.CounterTerroristTeam:1-game.CounterTerroristTeam;
   int before=game.RoundWins(winner);
   end.Invoke(game.Director,new object[]{outcome}); game.PrepareNextRound(); total++;
   if(game.LastRoundWinner!=winner||game.RoundWins(winner)!=before+1||game.RoundWins(0)+game.RoundWins(1)!=total||game.CompletedRounds!=total) throw new Exception("Wrong winner/score after side swap");
   game.PrepareNextRound(); game.AdvanceFrame(.1f);
   if(game.RoundWins(0)+game.RoundWins(1)!=total) throw new Exception("Round scored twice");
  }
  Debug.Log("MATCH_HUD_ALL_OK actual kill/weapon snapshot, new-round clear, aborted round, all outcomes, side swaps, no double score");
 }
}