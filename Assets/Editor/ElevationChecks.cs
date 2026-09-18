using System;
using System.Reflection;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class ElevationChecks
{
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 public static void Run()
 {
  var map=new ElevationMap();map.Add(new Rect(10,10,10,10),2.75f,3);
  Check(!map.Sight(new Vector2(15,15),1.65f,new Vector2(15,15),4.65f),"floor failed to separate stories");
  Check(map.Sight(new Vector2(11,11),1.65f,new Vector2(19,19),1.65f),"underfloor passage blocked");
  Check(map.Sight(new Vector2(11,11),4.65f,new Vector2(19,19),4.65f),"upper floor passage blocked");
  map.Add(new Rect(25,10,3,3),0,1.2f,true);
  Check(!map.GroundClear(new Vector2(24,11),new Vector2(30,11)),"box can be walked through");
  Check(!map.Sight(new Vector2(24,11),.8f,new Vector2(30,11),.8f)&&map.Sight(new Vector2(24,11),1.7f,new Vector2(30,11),1.7f),"box height cover");
  Check(ElevationMap.Ground(new Vector2(45,58))==1.2f&&ElevationMap.Ground(new Vector2(45,48))==0,"mid ramp");
  var combat=new CombatSystem(new CombatSettings(),new WeaponProfile(),2);combat.FeetHeight=i=>i==0?0:3;
  var track=typeof(CombatSystem).GetMethod("TrackElevation",BindingFlags.NonPublic|BindingFlags.Instance);
  track.Invoke(combat,new object[]{0,1,10f,.01f,20});float low=combat.AimElevation(0);combat.Reset(0);
  track.Invoke(combat,new object[]{0,1,10f,.01f,100});Check(combat.AimElevation(0)>low,"aim stat not affecting vertical adjustment");
  combat.HeightShotClear=(a,b,h)=>false;
  typeof(CombatSystem).GetMethod("Fire",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(combat,new object[]{0,1,0f,10f,100});Check(combat.Health(1)==100,"bullet penetrated floor");
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var game=new GameObject("Vertical checks").AddComponent<Prototype>();game.Initialize();
  var flags=BindingFlags.NonPublic|BindingFlags.Instance;
  Check(!(bool)typeof(Prototype).GetField("fogOfWar",flags).GetValue(game),"observer map still hides enemy");
  game.StartMatch();game.AdvanceFrame(Prototype.BuySeconds+.01f);Check(game.ElevatedMatch&&game.Combat.FeetHeight!=null,"automatic match height disabled");
  var nav=game.Navigation;
  foreach(var link in new ElevationMap().Links)Check(nav.Clear(link.entry,link.entry),"entry obstructed "+link.name);
  var actors=(System.Collections.Generic.List<GameObject>)typeof(Prototype).GetField("actors",flags).GetValue(game);
  var step=typeof(Prototype).GetMethod("StepElevation",flags);
  foreach(int id in new[]{0,1,2})
  {
   game.PrepareNextRound();game.BeginRound();var link=new ElevationMap().Links[id];actors[0].transform.position=new Vector3(link.entry.x,1,100-link.entry.y-1.25f);
   var order=new PlayerObjective{valid=true,task=PlayerTask.DefendSite,destination=link.entry};float peak=0;
   for(int t=0;t<240;t++){step.Invoke(game,new object[]{0,order,.1f});peak=Math.Max(peak,game.PlayerHeight(0));}
   Check(peak>=(id==0?2.9f:1.19f),"AI did not ascend "+link.name);
   Check(game.PlayerHeight(0)<.1f,"AI did not return from "+link.name);
  }
  game.PrepareNextRound();Check(game.Combat.FeetHeight==null&&game.PlayerHeight(0)==0,"height survived spawn reset");
  Debug.Log("ELEVATION_ALL_OK floors, cover, mid ramp, observer map, aim skill, blocked shots, AI stairs/jump/return, spawn reset");
 }
}
