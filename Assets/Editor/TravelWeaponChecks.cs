using System;
using System.Collections.Generic;
using System.Reflection;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class TravelWeaponChecks
{
 static void Check(bool v,string message){if(!v)throw new Exception(message);}
 public static void Run()
 {
  var nav=new DeploymentNavigation(new List<Rect>{new Rect(42,49,2,2)});var p=new Vector2(40,50);var probe=new Vector2(60,50);
  var aim=nav.VisibleAimPoint(p,probe);Check(aim!=p&&nav.SightClear(p,aim),"preaim still inside wall");
  var peek=new PeekMovement(31,nav,70,true);peek.InformationAllowed=false;var order=new PlayerObjective{valid=true,task=PlayerTask.PushSite,destination=probe};Vector2 target,watch;
  Check(!peek.Step(.1f,p,order,probe,false,out target,out watch),"opening blind information peek");
  Check(peek.Step(.1f,p,order,probe,true,out target,out watch),"opening contact response disabled");
  Check(nav.SightClear(p,p+watch),"peek watches wall interior");
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var g=new GameObject("Travel checks").AddComponent<Prototype>();g.Initialize();
  for(int i=0;i<10;i++)Check(g.HeldWeapon(i)=="knife","lobby not knife");g.StartMatch();
  for(int i=0;i<10;i++)Check(g.HeldWeapon(i)=="knife","first buying not knife");g.AdvanceFrame(Prototype.BuySeconds+.01f);
  var f=BindingFlags.Instance|BindingFlags.NonPublic;var update=typeof(Prototype).GetMethod("UpdateTravelWeapon",f);
  order=new PlayerObjective{valid=true,task=PlayerTask.MoveToLane,destination=g.MapPosition(0)+new Vector2(30,0)};
  update.Invoke(g,new object[]{0,order});Check(g.Combat.KnifeOut(0),"safe distant opening did not draw knife");
  var peeks=(PeekMovement[])typeof(Prototype).GetField("peeking",f).GetValue(g);Check(!peeks[0].InformationAllowed,"opening gate not connected");
  typeof(RoundDirector).GetProperty("Clock").SetValue(g.Director,15f,null);update.Invoke(g,new object[]{0,order});Check(peeks[0].InformationAllowed,"15 second gate did not expire");
  g.Vision.ReportSound(g.TeamIndexOf(0),5,0,g.MapPosition(0)+new Vector2(5,0));update.Invoke(g,new object[]{0,order});Check(!g.Combat.KnifeOut(0),"nearby known threat did not draw gun");
  var actors=(List<GameObject>)typeof(Prototype).GetField("actors",f).GetValue(g);var routes=(List<List<Vector2>>)typeof(Prototype).GetField("routes",f).GetValue(g);var steps=(int[])typeof(Prototype).GetField("routeSteps",f).GetValue(g);var speeds=(float[])typeof(Prototype).GetField("moveSpeed",f).GetValue(g);var autonomy=(PlayerAutonomy)typeof(Prototype).GetField("autonomy",f).GetValue(g);
  float gunDistance=0,knifeDistance=0;
  foreach(bool knife in new[]{false,true})
  {
   actors[0].transform.position=new Vector3(30,1,12);routes[0]=new List<Vector2>{new Vector2(40,88)};steps[0]=0;speeds[0]=0;autonomy.Walking[0]=false;g.Combat.SetKnife(0,knife);
   for(int t=0;t<6;t++)typeof(Prototype).GetMethod("StepRoute",f).Invoke(g,new object[]{0,.1f});
   float distance=g.MapPosition(0).x-30;if(knife)knifeDistance=distance;else gunDistance=distance;
  }
  Check(knifeDistance>gunDistance*1.1f,"knife run is not faster");
  var c=new CombatSystem(new CombatSettings(),WeaponCatalog.Find("ak_47"),2);c.AmmoEnabled=true;c.EquipSaved(0,"ak_47",new WeaponAmmo{rounds=7,spares=1});c.SetKnife(0,true);
  var positions=new[]{new Vector2(10,10),new Vector2(15,10)};var facing=new[]{new Vector2(1,0),new Vector2(-1,0)};int[] teams={0,1};var vision=new VisionSystem(new DeploymentNavigation(new List<Rect>()),new VisionSettings(),2);
  for(int t=0;t<10;t++){vision.Tick(.1f,positions,facing,teams);c.Tick(.1f,positions,facing,teams,new[]{true,false},new[]{90,90},vision);}
  Check(c.Shots==0&&c.Magazine(0)==7&&c.SpareMagazines(0)==1,"knife fired or changed ammo");c.SetKnife(0,false);Check(c.Magazine(0)==7&&c.SpareMagazines(0)==1,"draw refilled ammo");c.Reset(0);Check(!c.KnifeOut(0),"knife state survived reset");
  Debug.Log("TRAVEL_WEAPON_ALL_OK lobby knife, 15s gate/contact exception, visible corner aim, safe travel/draw, measured speed "+gunDistance+" -> "+knifeDistance+", no knife fire, ammo preserved, reset");
 }
}
