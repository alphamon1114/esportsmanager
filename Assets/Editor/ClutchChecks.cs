using System;
using System.Collections.Generic;
using System.Reflection;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class ClutchChecks
{
 static readonly BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static object Call(Prototype g,string method,params object[] args){return typeof(Prototype).GetMethod(method,F).Invoke(g,args);}
 public static void Run()
 {
  var search=new ClutchSearch();var nav=new DeploymentNavigation(new List<Rect>());
  var p=new Vector2(30,40);var facing=new Vector2(1,0);Vector2 target;var point=Vector2.zero;bool previous=false;
  for(int n=0;n<300;n++)
  {
   bool active=search.Step(.05f,p,facing,new Vector2(1,0),nav,out target);
   if(active){if(previous)Check(point==search.Point,"scan target jitters within an angle check");point=search.Point;p=Vector2.MoveTowards(p,target,.12f);CombatSystem.TurnTowards(ref facing,point-p,9);}
   previous=active;
  }
  Check(search.Checks>=2&&search.RearChecks==0,"clutch performed a speculative rear scan");
  search.Cancel();Check(!search.Active,"clutch scan cancellation failed");
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var g=new GameObject("Clutch checks").AddComponent<Prototype>();g.Initialize();g.StartMatch();g.AdvanceFrame(Prototype.BuySeconds+.01f);
  var actors=(List<GameObject>)typeof(Prototype).GetField("actors",F).GetValue(g);
  Call(g,"UpdateClutch",0);var modes=(bool[])typeof(Prototype).GetField("clutchMode",F).GetValue(g);Check(!modes[0],"full team received clutch behaviour");
  g.Combat.Equip(5,new WeaponProfile{damage=1000});for(int i=1;i<5;i++)typeof(CombatSystem).GetMethod("ApplyHit",F).Invoke(g.Combat,new object[]{5,i,HitRegion.Head});
  Call(g,"ClearGroundWeapons");for(int i=5;i<10;i++)actors[i].transform.position=new Vector3(75,1,12);
  actors[0].transform.position=new Vector3(30,1,12);actors[0].transform.rotation=Quaternion.LookRotation(new Vector3(1,0,0));
  g.Vision.Reset();g.MatchState(0).equipment=new[]{"awp","desert_eagle"};g.Combat.EquipSaved(0,"awp",new WeaponAmmo{rounds=3,spares=1});Call(g,"UpdateClutch",0);Check(modes[0],"last survivor not detected");
  var drops=(List<GroundWeapon>)typeof(Prototype).GetField("groundWeapons",F).GetValue(g);
  var rifle=new GroundWeapon{weapon="ak_47",ammo=new WeaponAmmo{rounds=0,spares=0},owner=5,position=new Vector3(31,0,12)};drops.Add(rifle);
  Check((bool)Call(g,"ClutchRifleSafe",0,rifle),"safe nearby clutch rifle rejected");
  Check((bool)Call(g,"CollectClutchRifle",0,.1f)&&g.Combat.WeaponFor(0).id=="ak_47","clutch did not exchange AWP for AR");
  Check(g.Combat.Magazine(0)==0&&Array.IndexOf(g.MatchState(0).equipment,"desert_eagle")>=0,"clutch inspected/refilled hidden ammo or removed pistol");
  Check((bool)Call(g,"ClutchKeepsRifle",0,"awp"),"clutch immediately wants discarded AWP again");
  // Travel + own kit/interaction duration, not just a fixed bomb timer threshold.
  typeof(RoundDirector).GetProperty("PlantedSite").SetValue(g.Director,0,null);
  typeof(RoundDirector).GetProperty("BombPosition").SetValue(g.Director,g.MapPosition(0),null);
  typeof(RoundDirector).GetProperty("BombTimer").SetValue(g.Director,12f,null);
  g.MatchState(0).defuseKit=false;Call(g,"UpdateClutch",0);var urgent=(bool[])typeof(Prototype).GetField("clutchUrgent",F).GetValue(g);Check(urgent[0],"10-second defuse did not suppress optional checks");
  g.MatchState(0).defuseKit=true;Call(g,"UpdateClutch",0);Check(!urgent[0],"kit time not included in clutch budget");
  typeof(RoundDirector).GetProperty("BombPosition").SetValue(g.Director,g.MapPosition(0)+new Vector2(20,0),null);Call(g,"UpdateClutch",0);Check(urgent[0],"travel time not included in clutch budget");
  var order=new PlayerObjective{valid=true,task=PlayerTask.Retake,destination=g.Director.BombPosition,watch=new Vector2(1,0)};
  Check(!(bool)Call(g,"StepClutchSearch",0,order,.1f),"urgent objective delayed by rear scan");
  g.MatchState(0).equipment=new[]{"awp","desert_eagle"};g.Combat.Equip(0,WeaponCatalog.Find("awp"));Check(!(bool)Call(g,"ClutchRifleSafe",0,rifle),"urgent objective delayed by weapon hunt");
  typeof(RoundDirector).GetProperty("BombTimer").SetValue(g.Director,40f,null);Call(g,"UpdateClutch",0);
  Check(!(bool)Call(g,"StepClutchSearch",0,order,.1f),"postplant multi-enemy scan delayed objective");
  Check((bool)Call(g,"SuppressIdlePeek",0),"postplant idle peek still allowed with multiple enemies");
  g.Combat.Equip(0,new WeaponProfile{damage=1000});for(int e=6;e<10;e++)typeof(CombatSystem).GetMethod("ApplyHit",F).Invoke(g.Combat,new object[]{0,e,HitRegion.Head});
  Check((bool)Call(g,"PostPlantDuel",0)&&(bool)Call(g,"SuppressIdlePeek",0),"postplant duel idle peek still allowed");
  typeof(RoundDirector).GetProperty("PlantedSite").SetValue(g.Director,-1,null);
  Check(!(bool)Call(g,"SuppressIdlePeek",0),"preplant information peek disabled");
  Call(g,"ResetClutch");Check(!modes[0]&&!urgent[0],"clutch state survived reset");
  Debug.Log("CLUTCH_ALL_OK stable forward angles, no speculative rear, postplant objective priority, normal-team exclusion, last survivor, safe AWP-AR exchange, hidden ammo, sidearm kept, no swap-back, travel/kit deadline, urgent release, reset");
 }
}
