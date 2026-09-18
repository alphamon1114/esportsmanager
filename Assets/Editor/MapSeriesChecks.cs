using System;using System.Reflection;using System.Collections.Generic;using FpsManager;using UnityEngine;
public static class MapSeriesChecks {
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 public static void Run(){
  var variants=new HashSet<string>();
  for(int seed=0;seed<100;seed++){
   var series=new TournamentSeries{teamA="spirit",teamB="falcons",bestOf=3};MatchMaps.Draft(series,seed,"spirit","de_nuke");
   Check(series.banA=="de_nuke"&&series.banB!=series.banA&&series.mapOrder.Length==3&&MatchMaps.ValidDraft(series),"invalid BO3 veto");
   string order=string.Join(",",series.mapOrder);variants.Add(order);MatchMaps.Draft(series,seed+1);Check(order==string.Join(",",series.mapOrder),"order rerolled");
  }Check(variants.Count>3,"order not shuffled");
  var db=TournamentRoster.Load();var bracket=new MajorBracket(db.teams,42);
  while(bracket.Next<6){int i=bracket.Next;var s=bracket.series[i];bracket.RecordMap(i,s.teamA,9,3);}
  var final=bracket.series[6];MatchMaps.Draft(final,42);Check(final.mapOrder.Length==5&&string.IsNullOrEmpty(final.banA)&&MatchMaps.ValidDraft(final),"BO5 bans or missing map");
  for(int n=0;n<3;n++)bracket.RecordMap(6,final.teamA,9,4);
  Check(final.maps.Count==3&&bracket.Next<0&&bracket.Valid(),"BO5 did not stop at three wins");
  final.mapOrder[1]=final.mapOrder[0];Check(!bracket.Valid(),"duplicate map save accepted");
  var g=new GameObject("veto integration").AddComponent<Prototype>();g.Initialize();g.StartTournament("spirit",42,false);
  Check(!g.PlayTournamentMap(true)&&g.AwaitingMapBan,"human ban bypassed");Check(!g.BanMap("invalid"),"invalid ban accepted");
  Check(g.BanMap("de_nuke")&&!g.AwaitingMapBan,"human ban rejected");var fixture=g.Tournament.series[0];
  Check(g.PlayTournamentMap(true)&&g.ActiveMapId==fixture.mapOrder[0]&&g.AwaitingMapStart,"map order not used");
  Check(!g.BanMap("de_mirage"),"late veto accepted");
  Debug.Log("MAP_SERIES_ALL_OK BO3 two bans, random unique order, no reroll, BO5 five maps/three wins, save validation, human veto and active map");
 }
}
public static class SourceGameplayChecks {
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 public static void Run(){
  foreach(var id in new[]{"de_inferno","de_dust2","de_mirage","de_nuke","de_vertigo"}){
   var arena=SourceArena.Load(id);int routes=0;
   foreach(var spawn in new[]{arena.CT,arena.T})foreach(var site in arena.Sites){var path=arena.Route(spawn,site);Check(path.Count>1,"missing source route");var previous=spawn;foreach(var point in path){Check(arena.Sight(previous+Vector3.up*.8f,point+Vector3.up*.8f),"route crosses source wall: "+id);previous=point;}Check((previous-site).magnitude<.1f,"wrong route floor");routes++;}
   if(id=="de_nuke")Check(Math.Abs(arena.Sites[0].y-arena.Sites[1].y)>8,"Nuke floors flattened");
   var g=new GameObject(id+" gameplay").AddComponent<Prototype>();g.Initialize();typeof(Prototype).GetMethod("ActivateMatchMap",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,new object[]{id});g.StartMatch();g.AdvanceFrame(Prototype.BuySeconds+.01f);
   Check(g.SourceMapActive&&g.ActiveMapId==id,"wrong runtime map");var before=g.MapPosition(0);for(int i=0;i<10;i++)for(int j=i+1;j<10;j++)if(g.PlayerHeight(i)+(g.IsCrouched(i)?1.2f:1.8f)>g.PlayerHeight(j)&&g.PlayerHeight(j)+(g.IsCrouched(j)?1.2f:1.8f)>g.PlayerHeight(i))Check(Vector2.Distance(g.MapPosition(i),g.MapPosition(j))>=.99f,"spawn overlap: "+id);
   for(int frame=0;frame<1600&&g.CompletedRounds==0;frame++){g.AdvanceFrame(.1f);if(frame%16==0)for(int i=0;i<10;i++)for(int j=i+1;j<10;j++)if(g.Combat.Alive(i)&&g.Combat.Alive(j)&&g.PlayerHeight(i)+(g.IsCrouched(i)?1.2f:1.8f)>g.PlayerHeight(j)&&g.PlayerHeight(j)+(g.IsCrouched(j)?1.2f:1.8f)>g.PlayerHeight(i))Check(Vector2.Distance(g.MapPosition(i),g.MapPosition(j))>=.995f,"live body overlap: "+id+" / "+i+","+j);}
   Check(g.CompletedRounds>0&&g.Combat.Shots>0,"source map did not complete combat round: "+id);Check(Vector2.Distance(before,g.MapPosition(0))>2,"source bots stuck");
   typeof(Prototype).GetMethod("ActivateMatchMap",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g,new object[]{"de_inferno"});Check(g.SourceMapActive&&g.ActiveMapId=="de_inferno","public Inferno switch failed");
   Debug.Log("SOURCE_GAMEPLAY_OK "+id+" routes="+routes+" shots="+g.Combat.Shots+" result="+g.LastRoundOutcome);
  }
  Debug.Log("SOURCE_GAMEPLAY_ALL_OK five playable meshes, directed portals, no wall cuts, floor separation, natural rounds and switching");
 }
}
