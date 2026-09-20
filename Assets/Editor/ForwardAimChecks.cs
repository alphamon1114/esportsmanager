using System;using System.Collections.Generic;using System.Reflection;using FpsManager;using UnityEngine;using UnityEditor.SceneManagement;
public static class ForwardAimChecks {
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static void Run(){
  var nav=new DeploymentNavigation(new List<Rect>());var planner=new PreAimPlanner();
  var order=new PlayerObjective{valid=true,task=PlayerTask.Patrol,forwardAdvance=true,destination=new Vector2(40,40),watch=new Vector2(0,12)};
  var route=new List<Vector2>{new Vector2(40,40)};
  planner.Choose(.1f,new Vector2(40,36),new Vector2(0,1),order,route,0,nav);
  var at=new Vector2(40,39);var point=planner.Choose(.1f,at,new Vector2(0,1),order,route,0,nav);
  Check(point.y>at.y+5,"forward arrival kept cached destination instead of watching the approach");
  at=new Vector2(40,41);point=planner.Choose(2,at,new Vector2(0,1),order,route,1,nav);
  Check(point.y>at.y+5,"forward overshoot looks back at destination");
  foreach(var id in new[]{"de_vertigo","de_inferno","de_dust2","de_mirage","de_nuke"}){
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   var g=new GameObject("Forward aim "+id).AddComponent<Prototype>();g.Initialize();typeof(Prototype).GetMethod("ActivateMatchMap",F).Invoke(g,new object[]{id});g.ChangeDefense(DefenseTactic.Forward);g.StartMatch();g.AdvanceFrame(Prototype.BuySeconds+.01f);
   var arena=SourceArena.Load(id);var actors=(List<GameObject>)typeof(Prototype).GetField("actors",F).GetValue(g);
   var positions=new Vector2[10];var anchors=new Vector2[10];var teams=new int[10];var calm=new int[10];
   for(int i=0;i<10;i++){positions[i]=g.MapPosition(i);anchors[i]=g.HomeAnchor(i);teams[i]=g.TeamIndexOf(i);calm[i]=80;}
   g.Vision.Reset();g.Director.Tick(.01f,positions,teams,g.CounterTerroristTeam,anchors,calm,g.Vision,g.Combat);
   int checkedPlayers=0;
   for(int i=0;i<10;i++){
    var initial=g.Director.Objective(i);if(!initial.forwardAdvance)continue;
    float height=(float)typeof(Prototype).GetMethod("GoalFloor",F).Invoke(g,new object[]{initial.destination,arena.CT.y});
    actors[i].transform.position=SourceArena.At(initial.destination,height)+Vector3.up;positions[i]=initial.destination;
    g.Vision.Reset();g.Director.Tick(.01f,positions,teams,g.CounterTerroristTeam,anchors,calm,g.Vision,g.Combat);
    order=g.Director.Objective(i);Check(order.forwardAdvance,id+" lost forward order");
    Check(order.watch.magnitude>3,id+" player="+i+" arrival watch collapses onto own feet");
    typeof(Prototype).GetField("sourceNavHeight",F).SetValue(g,height);
    point=new PreAimPlanner().Choose(2,positions[i],new Vector2(0,1),order,new List<Vector2>{positions[i]},1,g.Navigation);
    var visible=g.Navigation.VisibleAimPoint(positions[i],point);
    Check(Vector2.Distance(visible,positions[i])>1,id+" player="+i+" has no visible approach angle");
    Check(g.Navigation.SightClear(positions[i],visible),id+" aims through wall");
    var facing=(visible-positions[i]).normalized;
    Check(Vector2.Dot(facing,order.watch.normalized)>0,id+" watches return route");
    foreach(int fps in new[]{60,144}){
     actors[i].transform.rotation=Quaternion.LookRotation(new Vector3(-facing.x,0,facing.y));
     var applied=(bool[])typeof(Prototype).GetField("aimApplied",F).GetValue(g);
     for(int frame=0;frame<fps*2;frame++){applied[i]=false;typeof(Prototype).GetMethod("AimWhileMoving",F).Invoke(g,new object[]{i,order,1f/fps});}
     Check(Vector2.Dot(g.MapFacing(i),facing)>.95f,id+" actual facing did not turn toward approach fps="+fps);
     var vision=new VisionSystem(g.Navigation,new VisionSettings(),2);
     vision.Tick(1,new[]{positions[i],visible},new[]{g.MapFacing(i),facing*-1},new[]{0,1});
     Check(vision.Sees(0,1),id+" misses enemy entering watched approach fps="+fps);
    }
    checkedPlayers++;
   }
   Check(checkedPlayers>=3,id+" forward cases missing");Debug.Log("FORWARD_AIM_MAP_OK "+id+" players="+checkedPlayers);
  }
  Debug.Log("FORWARD_AIM_ALL_OK");
 }
}


