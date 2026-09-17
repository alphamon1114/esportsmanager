using System;
using System.Collections.Generic;
using System.Reflection;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class PreAimChecks
{
 static void Check(bool value,string text){if(!value)throw new Exception(text);}
 public static void Run()
 {
  var nav=new DeploymentNavigation(new List<Rect>());var planner=new PreAimPlanner();var pos=new Vector2(20,20);
  var route=new List<Vector2>{new Vector2(21,20),new Vector2(25,20),new Vector2(25,30)};
  var order=new PlayerObjective{valid=true,task=PlayerTask.MoveToLane,destination=new Vector2(25,30),watch=new Vector2(0,1)};
  var anchor=planner.Choose(.1f,pos,new Vector2(1,0),order,route,0,nav);
  Check(anchor==new Vector2(25,30),"preaim followed nearest footstep instead of future angle");
  Check(planner.Choose(.1f,new Vector2(21,20),new Vector2(-1,0),order,route,0,nav)==anchor,"world aim drifts with strafe");
  var geometry=new DeploymentNavigation(new List<Rect>{new Rect(42,49,2,2)});
  int hiddenLow=0,hiddenHigh=0;
  foreach(var task in new[]{PlayerTask.DefendSite,PlayerTask.HoldSite,PlayerTask.Lurk,PlayerTask.Patrol})
   foreach(int calm in new[]{20,95})
   {
    var peek=new PeekMovement(31,geometry,70,true,calm,true);var at=new Vector2(40,50);var probe=new Vector2(60,50);bool exposed=false,returned=false;int hidden=0;
    order=new PlayerObjective{valid=true,task=task,destination=at};
    for(int frame=0;frame<300;frame++)
    {
     Vector2 target,watch;bool active=peek.Step(.05f,at,order,probe,false,out target,out watch);
     if(active)at=Vector2.MoveTowards(at,target,.175f);
     Check(geometry.Clear(at,at),"information peek walked through cover");
     bool visible=geometry.SightClear(at,probe);if(active&&!visible)hidden++;
     exposed|=visible;returned|=exposed&&!visible;if(peek.Completed>0)break;
    }
    Check(exposed&&returned&&peek.Completed>0,"no-contact hold/lurk never checked and hid");
    if(calm==20)hiddenLow+=hidden;else hiddenHigh+=hidden;
    Vector2 t,w;order.task=PlayerTask.Defuse;Check(!peek.Step(.1f,at,order,probe,false,out t,out w),"info peek delayed urgent objective");
   }
  Check(hiddenHigh>hiddenLow,"composure did not extend hidden wait");
  var c=new CombatSystem(new CombatSettings(),new WeaponProfile(),2);c.FeetHeight=i=>0;
  c.PreAimHeight(0,3,10,1,80);float up=c.AimElevation(0);c.PreAimHeight(0,0,10,1,80);Check(up>0&&c.AimElevation(0)<0,"stale vertical aim did not reset to expected surface");
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var g=new GameObject("Natural preaim").AddComponent<Prototype>();g.Initialize();g.StartMatch();int peekTicks=0,shotFrames=0;float distance=0;
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;var previous=g.MapPosition(0);
  for(int frame=0;frame<1400;frame++)
  {
   g.AdvanceFrame(.1f);distance+=Vector2.Distance(previous,g.MapPosition(0));previous=g.MapPosition(0);
   if(g.Combat.Shots>0)shotFrames++;
   var peeks=(PeekMovement[])typeof(Prototype).GetField("peeking",flags).GetValue(g);foreach(var peek in peeks)if(peek!=null&&peek.Active)peekTicks++;
  }
  Check(peekTicks>0&&shotFrames>0&&g.CompletedRounds>0&&distance>20,"natural AI stalled or never peeked/fired");
  Debug.Log("PREAIM_ALL_OK future angles, fixed world point, no-contact hold/lurk patrol, real cover, hidden timing, urgent release, height reset; natural peek ticks="+peekTicks+" rounds="+g.CompletedRounds);
 }
}
