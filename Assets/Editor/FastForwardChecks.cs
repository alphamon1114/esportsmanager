using System;using System.IO;using System.Reflection;using UnityEngine;using UnityEditor.SceneManagement;using FpsManager;
public static class FastForwardChecks {
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static Prototype Game(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var g=new GameObject("skip checks").AddComponent<Prototype>();g.Initialize();g.StartTournament("spirit",42,false);g.PlayTournamentMap();return g;}
 static string Snapshot(Prototype g){string s=g.RoundWins(0)+":"+g.RoundWins(1);for(int i=0;i<10;i++){var p=g.Statistics.Result(i);s+="|"+p.kills+","+p.deaths+","+p.assists+","+p.damage.ToString("R")+","+g.MatchState(i).credits+","+g.Combat.Magazine(i);}return s;}
 public static void Run(){
  var g=Game();g.AdvanceFrame(30);Check(g.AwaitingMapStart&&g.Stage==MatchStage.Lobby&&g.CompletedRounds==0&&g.MatchState(0).credits==800,"prestart not frozen");Check(g.ChangeDefense(DefenseTactic.Stack),"prestart strategy locked");Check(!g.SkipRound(),"prestart skip allowed");g.ConfirmMapStart();
  var clock=System.Diagnostics.Stopwatch.StartNew();
  Check(g.SkipRound(),"skip rejected");for(int n=0;g.FastForwarding&&n<100;n++)g.PumpFastForward();
  Check(!g.FastForwarding&&g.CompletedRounds==1&&g.Stage==MatchStage.Buying,"stat skip boundary");string expected=Snapshot(g);Check(g.DefensePlan==DefenseTactic.Stack,"saved strategy overwritten");
  g=Game();g.ConfirmMapStart();g.SkipRound();for(int n=0;g.FastForwarding&&n<100;n++)g.PumpFastForward();
  Check(expected==Snapshot(g),"stat skip not deterministic");Check(g.StageSeconds==Prototype.BuySeconds,"next buy already advanced");
  Check(g.DefensePlan==DefenseTactic.Default,"skip overwrote saved human tactics");
  g.RequestTimeout();Check(!g.SkipRound(),"timeout skipped");g.ResumeTimeout();g.SkipRound();g.CancelFastForward();Check(!g.FastForwarding,"cancel failed");
  // Preserve a real death that happened before the button was pressed.
  var partial=Game();partial.ConfirmMapStart();partial.AdvanceFrame(Prototype.BuySeconds+.01f);
  partial.Combat.Equip(5,new WeaponProfile{id="ak_47",damage=1000});
  typeof(CombatSystem).GetMethod("ApplyHit",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(partial.Combat,new object[]{5,0,HitRegion.Head});
  Check(partial.Statistics.Result(0).deaths==1,"partial fixture missing death");partial.SkipRound();
  for(int n=0;partial.FastForwarding&&n<100;n++)partial.PumpFastForward();
  Check(partial.Statistics.Result(0).deaths==1&&partial.Statistics.Result(5).kills>=1,"skip erased or duplicated existing kill");
  Check(partial.CompletedRounds==1&&partial.Stage==MatchStage.Buying,"midround skip failed");
  // Force the first series at a completed-map boundary, then naturally simulate an AI series.
  typeof(Prototype).GetProperty("Stage").GetSetMethod(true).Invoke(g,new object[]{MatchStage.Finished});g.StartTournament("spirit",42,false);var b=g.Tournament;for(int i=0;i<2;i++)b.RecordMap(0,b.series[0].teamA,9,3);
  var seriesClock=System.Diagnostics.Stopwatch.StartNew();
  Check(g.SkipOtherMatch(),"AI series skip rejected");for(int n=0;g.FastForwarding&&n<1000;n++)g.PumpFastForward();
  seriesClock.Stop();
  Check(!g.FastForwarding&&g.TournamentBoard&&!string.IsNullOrEmpty(b.series[1].winner),"AI series incomplete");Check(b.Valid(),"skipped bracket invalid");
  foreach(var map in b.series[1].maps){Check(map.playerStats!=null&&map.playerStats.Length==10,"KDA not archived");int kills=0,deaths=0;foreach(var p in map.playerStats){Check(p.rounds==map.scoreA+map.scoreB,"round stats mismatch");kills+=p.kills;deaths+=p.deaths;}Check(kills==deaths&&kills>0,"no real combat results");}
  Check(clock.Elapsed.TotalSeconds<10,"stat skip took over ten seconds");
  Debug.Log("FAST_FORWARD_ALL_OK statistical deterministic rounds, score/KDA/economy/ammo, next buy, timeout, cancel, full AI BO3 + archived KDA; elapsed="+clock.Elapsed.TotalSeconds.ToString("F3")+"s; AI series="+seriesClock.Elapsed.TotalSeconds.ToString("F3")+"s");
 }
}
public static class SourceMapChecks {
 public static void Run(){foreach(string map in new[]{"de_inferno","de_dust2","de_mirage","de_nuke","de_vertigo"}){string root="Assets/MapSources/2000908/";var m=SourceMapData.ReadMesh(root+map+".awmh");var nav=SourceMapData.ReadNav(root+map+".nav");if(m.vertices.Length<1000||nav.Count<100)throw new Exception("Missing geometry/nav "+map);int links=0;float min=float.MaxValue,max=float.MinValue;foreach(var a in nav.Values){min=Math.Min(min,a.Center.y);max=Math.Max(max,a.Center.y);foreach(int to in a.connections){if(nav[to].hull!=a.hull)continue;if(SourceMapData.Route(nav,a.id,to).Count<2&&a.id!=to)throw new Exception("Lost directed link");links++;}}if(max-min<2||links==0)throw new Exception("Map flattened");Debug.Log("SOURCE_MAP_OK "+map+" verts="+m.vertices.Length+" tris="+m.triangles.Length/3+" areas="+nav.Count+" links="+links+" height="+(max-min));}
  string temp=Path.GetTempFileName();try{File.WriteAllBytes(temp,new byte[16]);bool rejected=false;try{SourceMapData.ReadMesh(temp);}catch(InvalidDataException){rejected=true;}if(!rejected)throw new Exception("Invalid mesh accepted");}finally{File.Delete(temp);}Debug.Log("SOURCE_MAP_ALL_OK five real meshes/navs, coordinate heights, directed paths, invalid file rejection");
 }
}
