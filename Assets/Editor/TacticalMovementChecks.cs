using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor.SceneManagement;
using FpsManager;
public static class TacticalMovementChecks
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static void Run()
 {
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var game=new GameObject("Tactics checks").AddComponent<Prototype>();game.Initialize();
  var flags=BindingFlags.NonPublic|BindingFlags.Instance;
  var nav=(DeploymentNavigation)typeof(Prototype).GetField("navigation",flags).GetValue(game);
  var p=new Vector2[10];var homes=new Vector2[10];var teams=new int[10];var comp=new int[10];
  for(int side=0;side<2;side++)
  {
   if(side==1){game.PrepareNextRound();game.SwapPreviewSides();}
   game.SetRoundSeed(17);game.BeginRound();
   for(int i=0;i<10;i++){p[i]=game.MapPosition(i);homes[i]=game.HomeAnchor(i);teams[i]=game.TeamIndexOf(i);comp[i]=90;}
   var d=game.Director;int lurker=d.Lurker;
   Check(lurker>=0&&lurker!=d.Carrier&&teams[lurker]!=game.CounterTerroristTeam,"Lurker assignment");
   d.Tick(.05f,p,teams,game.CounterTerroristTeam,homes,comp,game.Vision,game.Combat);
   Check(d.Objective(lurker).task==PlayerTask.Lurk,"Lurker follows main group");
   int start=d.LurkStep;
   p[lurker]=d.Objective(lurker).destination;
   for(int tick=0;tick<40;tick++)d.Tick(.05f,p,teams,game.CounterTerroristTeam,homes,comp,game.Vision,game.Combat);
   Check(d.LurkStep>start,"Lurker never extends control");
   // Hidden world positions must not affect the lurker's waypoint.
   var destination=d.Objective(lurker).destination;
   for(int i=0;i<10;i++)if(teams[i]==game.CounterTerroristTeam)p[i]=new Vector2(98,98);
   d.Tick(.05f,p,teams,game.CounterTerroristTeam,homes,comp,game.Vision,game.Combat);
   Check(d.Objective(lurker).destination==destination,"Lurker tracks unseen enemies");
  }
  foreach(var variants in game.Layout.FlankRoutes)foreach(var path in variants)
   foreach(var point in path)Check(nav.Clear(point,point),"Flank anchor inside wall: "+point);
  // Both cross-map directions can advance via CT/mid instead of returning to T spawn.
  foreach(var pair in new[]{new[]{new Vector2(18,20),new Vector2(83,33)},new[]{new Vector2(83,33),new Vector2(18,20)},new[]{new Vector2(45,50),new Vector2(18,20)}})
  {
   var route=nav.CombatRoute(pair[0],pair[1],game.Layout.AttackerSpawn,new List<Vector2>());
   Vector2 last=pair[0];float length=0;
   foreach(var point in route)
   {
    Check(nav.Clear(last,point),"Combat route crossed geometry");
    Check(Vector2.Distance(point,game.Layout.AttackerSpawn)>24,"Advanced rotation returned to spawn");
    length+=Vector2.Distance(last,point);last=point;
   }
   Check(Vector2.Distance(last,pair[1])<2&&length<150,"Combat route failed arrival/bounded detour");
  }
  // A single death dispatches support even without any surviving observer.
  game.PrepareNextRound();game.BeginRound();
  for(int i=0;i<10;i++){teams[i]=game.TeamIndexOf(i);homes[i]=game.HomeAnchor(i);p[i]=new Vector2(90,90);comp[i]=90;}
  int ct=game.CounterTerroristTeam;var defenders=new List<int>();int attacker=-1;
  for(int i=0;i<10;i++)if(teams[i]==ct)defenders.Add(i);else attacker=i;
  p[defenders[0]]=homes[defenders[0]]=game.Layout.Sites[1];
  for(int i=1;i<5;i++){p[defenders[i]]=homes[defenders[i]]=game.Layout.Sites[0];}
  game.Combat.Equip(attacker,new WeaponProfile{damage=10000,baseSpreadDegrees=0,recoilPerShot=0});
  typeof(CombatSystem).GetMethod("Fire",flags).Invoke(game.Combat,new object[]{attacker,defenders[0],0f,10f,100});
  game.Vision.Reset();var director=game.Director;
  director.Tick(.05f,p,teams,ct,homes,comp,game.Vision,game.Combat);
  int scout=-1;
  foreach(int i in defenders)if(game.Combat.Alive(i)&&director.Objective(i).cautious)scout=i;
  Check(scout>=0&&game.Vision.KnownCount(ct)==0,"Site death without witness ignored");
  p[scout]=director.Objective(scout).destination;
  for(int tick=0;tick<40;tick++)director.Tick(.05f,p,teams,ct,homes,comp,game.Vision,game.Combat);
  Check(director.Objective(scout).task==PlayerTask.Patrol&&Vector2.Distance(director.Objective(scout).destination,p[scout])>3,"Investigator stayed on corpse");
  int victim=-1;foreach(int i in defenders)if(i!=scout&&game.Combat.Alive(i)){victim=i;break;}
  p[victim]=p[defenders[0]];
  typeof(CombatSystem).GetMethod("Fire",flags).Invoke(game.Combat,new object[]{attacker,victim,0f,10f,100});
  director.Tick(.05f,p,teams,ct,homes,comp,game.Vision,game.Combat);
  Check(director.Objective(scout).task==PlayerTask.Rotate,"New nearby casualty did not reopen investigation");
  director.Reset();Check(director.Lurker==-1,"Lurk state survived reset");
  Debug.Log("TACTICAL_MOVEMENT_ALL_OK both sides, independent lurker, staged advance, no hidden tracking, valid anchors, forward rotations, single death investigation, patrol, reset");
 }
}