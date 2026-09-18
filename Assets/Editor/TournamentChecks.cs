using System;using System.Reflection;using FpsManager;using UnityEngine;using UnityEditor.SceneManagement;
public static class TournamentChecks {
 static void Check(bool ok,string s){if(!ok)throw new Exception(s);}
 public static void Run(){
  var db=TournamentRoster.Load();Check(db.teams.Length==8&&db.players.Length==40,"catalog size");var roster=TournamentRoster.Pair(db,"mouz","spirit");Check(roster.teams[0].id=="spirit"&&roster.players.Length==10,"Spirit control side");
  var bracket=new MajorBracket(db.teams,9);Check(bracket.Valid()&&bracket.Next==0&&bracket.series[0].teamA=="spirit","seeding");
  int maps=0;while(bracket.Next>=0){int i=bracket.Next;var s=bracket.series[i];bool a=s.maps.Count%2==0;bracket.RecordMap(i,a?s.teamA:s.teamB,a?9:7,a?7:9);maps++;Check(bracket.Valid(),"valid bracket rejected");}
  Check(maps==23&&!string.IsNullOrEmpty(bracket.Champion)&&bracket.series[6].winsA==3,"BO3/BO5 max maps / champion");
#if UNITY_5_3_OR_NEWER
  var restored=JsonUtility.FromJson<MajorBracket>(JsonUtility.ToJson(bracket));Check(restored.Valid()&&restored.Champion==bracket.Champion,"JSON round trip");
#endif
  bool rejected=false;try{bracket.RecordMap(6,bracket.Champion,9,0);}catch{rejected=true;}Check(rejected,"finished bracket accepted extra map");bracket.series[6].winsA=8;Check(!bracket.Valid(),"corrupt save accepted");
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var g=new GameObject("tournament integration").AddComponent<Prototype>();g.Initialize();Check(g.StartTournament()&&g.TournamentBoard,"start board");Check(g.PlayTournamentMap(),"start map");g.ConfirmMapStart();g.AdvanceFrame(Prototype.BuySeconds+.01f);Check(g.Data.players.Length==10&&g.Data.teams[1].id=="mongolz"&&!g.ComputerOnlyMatch,"actual first fixture");
  var flags=BindingFlags.NonPublic|BindingFlags.Instance;var end=typeof(RoundDirector).GetMethod("End",flags);
  for(int r=0;r<9;r++){
   end.Invoke(g.Director,new object[]{g.CounterTerroristTeam==0?RoundOutcome.TimeExpired:RoundOutcome.CounterTerroristsEliminated});g.AdvanceFrame(.01f);g.AdvanceFrame(5.01f);
   if(r<8){g.AdvanceFrame(Prototype.BuySeconds+.01f);if(g.Stage==MatchStage.Timeout){g.AdvanceFrame(30.01f);g.AdvanceFrame(.01f);}Check(g.Stage==MatchStage.Live,"round resume");}
  }
  Check(g.Tournament.series[0].maps.Count==1&&g.Tournament.series[0].winsA==1,"real map not recorded");g.AdvanceFrame(1);Check(g.Tournament.series[0].maps.Count==1,"double recording");
  typeof(Prototype).GetField("tournamentBoard",flags).SetValue(g,true);Check(g.PlayTournamentMap(),"next map");Check(g.RoundWins(0)==0&&g.MatchState(0).credits==800&&g.TimeoutsRemaining==2,"map state not reset");
  // Complete this series at the bracket boundary; next actual fixture has no Spirit.
  var series=g.Tournament.series[0];g.Tournament.RecordMap(0,series.teamA,9,4);typeof(Prototype).GetField("tournamentBoard",flags).SetValue(g,true);Check(g.PlayTournamentMap(),"AI fixture");g.ConfirmMapStart();g.AdvanceFrame(Prototype.BuySeconds+.01f);
  Check(g.ComputerOnlyMatch&&!g.CanChangeStrategy&&!g.RequestTimeout(),"spectator can coach unrelated team");
  var ai=(AiCoach)typeof(Prototype).GetField("spectatorCoach",flags).GetValue(g);Check(ai.Initialized&&g.Director.SidePlansEnabled,"second AI coach not initialized");
  Check(g.Data.teams[0].id=="fut"&&g.Data.teams[1].id=="vitality","AI fixture roster");
  bool observedClock=false,observedShot=false;
  for(int f=0;f<300;f++){g.AdvanceFrame(.1f);observedClock|=g.Director.Clock>10;observedShot|=g.Combat.Shots>0;}
  Check(observedClock&&observedShot,"AI versus AI did not play: phase="+g.Stage+", rounds="+g.CompletedRounds+", map="+g.ActiveMapId);

  Debug.Log("TOURNAMENT_ALL_OK 8/40, seed, 23-map bracket, BO5 champion, invalid saves, actual map recording, next map reset, two AI coaches, spectator lock, natural combat");
 }
}


