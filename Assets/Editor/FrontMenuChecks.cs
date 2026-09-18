using System;using System.Reflection;using UnityEngine;using UnityEditor.SceneManagement;using FpsManager;
public static class FrontMenuChecks {
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 public static void Run(){
  var db=TournamentRoster.Load();var draws=new System.Collections.Generic.HashSet<string>();
  for(int n=0;n<20;n++){var b=new MajorBracket(db.teams,n,true);Check(b.Valid(),"random bracket invalid");draws.Add(string.Join(",",b.entrants));var s=b.series[0];b.RecordMap(0,s.teamA,9,3);Check(b.Valid(),"random result invalid");}
  Check(draws.Count>1,"draw not random");
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var g=new GameObject("menu checks").AddComponent<Prototype>();g.Initialize();
  foreach(var t in db.teams){Check(g.StartTournament(t.id,42,true),"selection rejected");var b=g.Tournament;Check(b.controlledTeam==t.id,"owner lost");
   // Advance bracket fixtures until selected team appears, without starting unrelated live matches.
   while(b.Next>=0&&b.series[b.Next].teamA!=t.id&&b.series[b.Next].teamB!=t.id){var s=b.series[b.Next];b.RecordMap(b.Next,s.teamA,9,3);}
   Check(g.PlayTournamentMap()&&!g.ComputerOnlyMatch,"selected team spectator");Check(g.Data.teams[g.AlliedTeamIndex].id==t.id,"wrong allied index");int count=0;for(int i=0;i<10;i++)if(g.IsAlliedPlayer(i))count++;Check(count==5,"wrong allied players");
   typeof(Prototype).GetProperty("Stage").GetSetMethod(true).Invoke(g,new object[]{MatchStage.Finished});
  }
  g.SetLanguage(true);Check(g.Ui("SELECT TEAM")=="팀 선택"&&g.Ui("donk")=="donk"&&g.Ui("Team Vitality")=="Team Vitality","translation scope");g.SetLanguage(false);Check(g.Ui("SELECT TEAM")=="SELECT TEAM","English changed");
  g.OpenMainMenu();float clock=g.Director.Clock;g.AdvanceFrame(10);Check(g.Director.Clock==clock,"menu runs simulation");
  Check(Prototype.TeamOverall(db,"spirit")>0&&Prototype.TeamOverall(db,"spirit")<=100,"overall");Debug.Log("FRONT_MENU_ALL_OK random draws, saved owner, all eight controlled teams, language scope, menu freeze, overall");
 }
}
