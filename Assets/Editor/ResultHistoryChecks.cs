using System;using System.Reflection;using FpsManager;using UnityEngine;
public static class ResultHistoryChecks {
 public static void Run(){
  var a=new PlayerResult{rounds=10,kills=10,damage=800,kast=7};var b=new PlayerResult{rounds=10,kills=15,damage=1100,kast=9};
  if(PlayerResult.Mvp(new[]{a,b})!=1||PlayerResult.Mvp(new PlayerResult[0])!=-1)throw new Exception("MVP selection");
  // The losing team's better overall rating must never take the winner's MVP.
  if(PlayerResult.Mvp(new[]{a,b},i=>i==0)!=0||PlayerResult.Mvp(new[]{a,b},i=>i==1)!=1||PlayerResult.Mvp(new[]{a,b},i=>false)!=-1)throw new Exception("team awards leaked across teams");
  if(PlayerResult.Mvp(new[]{a,b},i=>i==1)!=1)throw new Exception("reversed winning team");
  var roster=TournamentRoster.Load();int mixed=0;for(int seed=0;seed<100;seed++){var seen=new System.Collections.Generic.HashSet<DailyCondition>();for(int i=0;i<5;i++)seen.Add(ConditionRules.Roll("DAY 1",roster.players[i].id,seed));if(seen.Count>1)mixed++;}if(mixed<90)throw new Exception("conditions grouped by team");
  var g=new GameObject("history checks").AddComponent<Prototype>();g.Initialize();g.StartTournament();if(g.OpenSeriesStats(0)||g.OpenSeriesStats(-1))throw new Exception("empty history opened");
  var series=g.Tournament.series[0];g.Tournament.RecordMap(0,series.teamA,9,0);if(!g.OpenSeriesStats(0))throw new Exception("completed map unavailable");
#if !UNITY_5_3_OR_NEWER
  var data=g.Data;var stats=g.Statistics;var stage=g.Stage;typeof(Prototype).GetMethod("DrawMatchResults",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,null);
  if(g.Data!=data||g.Statistics!=stats||g.Stage!=stage)throw new Exception("history changed live match");
#endif
  g.CloseSeriesStats();Debug.Log("RESULT_HISTORY_ALL_OK winning MVP / losing EVP, independent player conditions, empty/unplayed refusal, legacy stats fallback, read-only history");
 }
}
