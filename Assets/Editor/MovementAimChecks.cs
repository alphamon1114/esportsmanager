using System;
using System.Collections.Generic;
using System.Reflection;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class MovementAimChecks
{
 static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
 public static void Run()
 {
  var nav=new DeploymentNavigation(new List<Rect>());var vision=new VisionSystem(nav,new VisionSettings{hearingRange=0},3);vision.LegacyFootsteps=false;
  int[] teams={0,1,1};var selector=new MovementAim();var origin=new Vector2(20,20);var point=new Vector2(32,20);
  vision.ReportSound(0,1,0,point);
  var watch=selector.Choose(0,origin,new Vector2(10,20),teams,vision);
  Check(selector.HasContact&&Vector2.Dot(watch.normalized,new Vector2(1,0))>.99f,"Backward travel discarded threat");
  var sideways=new Vector2(20,22);watch=selector.Choose(0,sideways,new Vector2(20,30),teams,vision);
  Check(Vector2.Distance(selector.Point,point)<.001f&&Vector2.Dot(watch.normalized,(point-sideways).normalized)>.99f,"Strafe drifted aim point");
  // Changing an unseen transform cannot update a reported world point.
  var p=new[]{origin,new Vector2(80,80),new Vector2(90,90)};var f=new[]{new Vector2(-1,0),new Vector2(1,0),new Vector2(1,0)};
  vision.Tick(.1f,p,f,teams,new[]{true,true,true});selector.Choose(0,sideways,new Vector2(20,30),teams,vision);
  Check(selector.Point==point,"Aim read hidden enemy transform");
  vision.Tick(3,p,f,teams,new[]{true,true,true});var fallback=new Vector2(20,40);selector.Choose(0,origin,fallback,teams,vision);
  Check(!selector.HasContact&&selector.Point==fallback,"Expired aim cue did not return to route");
  vision.Reset();selector=new MovementAim();vision.ReportSound(0,1,0,new Vector2(35,20));selector.Choose(0,origin,fallback,teams,vision);
  vision.ReportSound(0,2,0,new Vector2(34,20));selector.Choose(0,origin,fallback,teams,vision);
  Check(selector.Point==new Vector2(35,20),"Nearly equal cues caused aim chatter");
  // Actual movement route: keep a sideways facing, then aim at a world point while retreating.
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var game=new GameObject("movement aim check").AddComponent<Prototype>();game.Initialize();game.BeginRound();
  var type=typeof(Prototype);var flags=BindingFlags.NonPublic|BindingFlags.Instance;
  var actors=(List<GameObject>)type.GetField("actors",flags).GetValue(game);
  var routes=(List<List<Vector2>>)type.GetField("routes",flags).GetValue(game);
  var steps=(int[])type.GetField("routeSteps",flags).GetValue(game);
  var position=new Vector2(30,88);Check(game.Navigation.Clear(position,new Vector2(26,88)),"Test path blocked");
  actors[0].transform.position=new Vector3(position.x,1,100-position.y);
  actors[0].transform.rotation=Quaternion.LookRotation(new Vector3(0,0,-1));
  routes[0]=new List<Vector2>{new Vector2(26,88)};steps[0]=0;
  var before=game.MapFacing(0);type.GetMethod("StepRoute",flags).Invoke(game,new object[]{0,.05f});
  Check(game.MapPosition(0).x<position.x&&Vector2.Dot(before,game.MapFacing(0))>.999f,"Route overwrote strafe facing");
  var gameVision=(VisionSystem)type.GetField("vision",flags).GetValue(game);
  gameVision.ReportSound(0,5,0,new Vector2(38,88));
  var order=new PlayerObjective{valid=true,disengage=true,task=PlayerTask.FallBack,destination=new Vector2(26,88)};
  for(int tick=0;tick<12;tick++) {type.GetMethod("StepRoute",flags).Invoke(game,new object[]{0,.05f});type.GetMethod("AimWhileMoving",flags).Invoke(game,new object[]{0,order,.05f});}
  Check(game.MapPosition(0).x<position.x&&game.MapFacing(0).x>.98f,"Retreat did not keep looking at known threat");
  game.PrepareNextRound();game.BeginRound();var brains=(MovementAim[])type.GetField("movementAim",flags).GetValue(game);
  Check(!brains[0].HasContact,"Round reset retained aim lock");
  var moving=(bool[])type.GetField("moving",flags).GetValue(game);int shots=0;
  game.Combat.ShotFired+=i=>{Check(!moving[i],"Route movement fired a stationary-accuracy shot");shots++;};
  for(int tick=0;tick<1600&&game.Director.Phase!=RoundPhase.Ended;tick++) game.SimulateMovement(.05f);
  Check(shots>0,"Live integration never fired");
  Debug.Log("MOVEMENT_AIM_STATIONARY_FIRE_OK shots="+shots);
  Debug.Log("MOVEMENT_AIM_ALL_OK strafe, backwards, fixed world point, no hidden tracking, stale expiry, stable focus, actual route, retreat, round reset");
 }
}