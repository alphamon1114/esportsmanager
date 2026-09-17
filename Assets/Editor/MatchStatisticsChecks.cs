using System;
using System.Reflection;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class MatchStatisticsChecks
{
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 public static void Run()
 {
  int[] teams={0,0,0,0,0,1,1,1,1,1};var stats=new MatchStatistics();var combat=new CombatSystem(new CombatSettings(),new WeaponProfile(),10);
  stats.Begin(teams,0);stats.Damage(0,5,25);stats.Damage(0,5,25);stats.Damage(1,5,50);stats.Damage(0,1,90);
  stats.Kill(new KillEvent{killer=1,victim=5,region=HitRegion.Head},1);
  stats.Kill(new KillEvent{killer=6,victim=2},2);stats.Kill(new KillEvent{killer=3,victim=6},6);
  stats.Kill(new KillEvent{killer=7,victim=4},7);stats.Kill(new KillEvent{killer=0,victim=7},13);
  stats.End(combat);stats.End(combat);
  Check(stats.Result(0).assists==1&&stats.Result(0).damage==50&&stats.Result(1).headshots==1,"damage/assist/headshot");
  Check(stats.Result(2).kast==1&&stats.Result(4).kast==0,"trade window");
  Check(stats.Result(0).rounds==1&&stats.Result(0,1).rounds==1&&stats.Result(0,2).rounds==0,"side attribution/idempotent end");
  stats.Begin(teams,1);stats.End(combat);Check(stats.Result(0).rounds==2&&stats.Result(0,2).rounds==1&&stats.Result(0).Adr==25,"side switch/ADR denominator");
  Check(!float.IsNaN(new PlayerResult().Rating)&&new PlayerResult().Rating==0,"empty stats");
  var boundary=new MatchStatistics();boundary.Begin(teams,0);boundary.Damage(0,5,49.9f);boundary.Damage(1,5,50.1f);boundary.Kill(new KillEvent{killer=1,victim=5},1);
  Check(boundary.Result(0).assists==0&&boundary.Result(1).assists==0,"below 50 or killer counted as assist");
  boundary.Begin(teams,0);boundary.Damage(0,5,.1f);boundary.Kill(new KillEvent{killer=1,victim=5},1);Check(boundary.Result(0).assists==0,"assist damage carried across rounds");
  boundary.Begin(teams,0);boundary.Damage(0,5,50);boundary.Kill(new KillEvent{killer=1,victim=5},1);boundary.Kill(new KillEvent{killer=1,victim=5},1);
  Check(boundary.Result(0).assists==1&&boundary.Result(0,1).assists==1&&boundary.Result(1).kills==3,"50 boundary/live update/duplicate kill");
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var game=new GameObject("Stats integration").AddComponent<Prototype>();game.Initialize();game.StartMatch();game.AdvanceFrame(3.01f);
  var gun=WeaponCatalog.Find("awp");game.Combat.Equip(0,gun);
  typeof(CombatSystem).GetMethod("ApplyHit",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game.Combat,new object[]{0,5,HitRegion.Head});
  Check(game.Statistics.Result(0).damage==100&&game.Statistics.Result(0).kills==1,"overkill clamp/live events");
  for(int round=0;round<9;round++)
  {
   typeof(RoundDirector).GetMethod("End",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game.Director,new object[]{game.CounterTerroristTeam==0?RoundOutcome.TimeExpired:RoundOutcome.CounterTerroristsEliminated});
   game.AdvanceFrame(.01f);game.AdvanceFrame(3.01f);if(round<8)game.AdvanceFrame(3.01f);
  }
  Check(game.Stage==MatchStage.Finished&&game.Statistics.Result(0).rounds==9&&game.Statistics.Result(0).kills==1,"full map aggregation");
  Check(game.Statistics.Result(0,1).rounds==8&&game.Statistics.Result(0,2).rounds==1,"halftime split");
  game.StartMatch();Check(game.Statistics.Result(0).rounds==0&&game.Statistics.Result(0).kills==0,"new map resets stats");
  Debug.Log("MATCH_STATS_ALL_OK damage clamp, assists, trades, survival, side splits, duplicate end, map finish, new map reset");
 }
}
