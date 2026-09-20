using System;using FpsManager;using UnityEngine;
public static class VictoryCelebrationChecks {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Finish(Prototype g,bool win){
  while(g.Tournament.Next>=0){
   Check(!g.ShowVictoryCelebration,"celebration shown before championship");
   int next=g.Tournament.Next;var s=g.Tournament.series[next];
   string winner=s.teamA==g.Tournament.controlledTeam||s.teamB==g.Tournament.controlledTeam?(win?g.Tournament.controlledTeam:s.teamA==g.Tournament.controlledTeam?s.teamB:s.teamA):s.teamA;
   bool first=winner==s.teamA;g.Tournament.RecordMap(next,winner,first?9:3,first?3:9);
  }
 }
 public static void Run(){
  var field=typeof(Prototype).GetField("TournamentSaveKey",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
  string previous=(string)field.GetValue(null),temporary="fps.victory-check."+Guid.NewGuid().ToString("N");field.SetValue(null,temporary);
  try{RunCases();}finally{
   field.SetValue(null,previous);
#if UNITY_5_3_OR_NEWER
   PlayerPrefs.DeleteKey(temporary);PlayerPrefs.Save();
#endif
  }
 }
 static void RunCases(){
  var g=new GameObject("victory checks").AddComponent<Prototype>();g.Initialize();
  Check(g.StartTournament("spirit",42,false),"could not start tournament");Finish(g,true);
  Check(g.ShowVictoryCelebration&&g.Tournament.Valid(),"controlled champion not celebrated");
  var champion=g.Tournament.Champion;g.DismissVictoryCelebration();g.DismissVictoryCelebration();
  Check(!g.ShowVictoryCelebration&&g.Tournament.Champion==champion&&g.Tournament.Valid(),"dismissal repeated or changed results");
#if UNITY_5_3_OR_NEWER
  Check(g.LoadTournament()&&!g.ShowVictoryCelebration,"dismissed celebration returned after loading");
#endif
  Check(g.StartTournament("mouz",17,false),"new tournament failed");Finish(g,true);
  Check(g.ShowVictoryCelebration,"second tournament/other controlled team not celebrated");g.DismissVictoryCelebration();
  Check(g.StartTournament("spirit",25,false),"loss tournament failed");Finish(g,false);
  Check(!g.ShowVictoryCelebration,"opponent champion celebrated as player victory");
  Debug.Log("VICTORY_CELEBRATION_ALL_OK final-only, controlled champion, dismiss once, results preserved, new tournament, opponent exclusion");
 }
}

