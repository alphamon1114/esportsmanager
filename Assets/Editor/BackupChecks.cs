using System;
using System.Collections.Generic;
using UnityEngine;
using FpsManager;
public static class BackupChecks
{
 public static void Run()
 {
  var layout=new MapLayout{Sites=new[]{new Vector2(20,20),new Vector2(70,20)},SiteNames=new[]{"A","B"},Staging=new[]{new Vector2(15,20),new Vector2(75,20)},HoldRing=new[]{new[]{new Vector2(20,22)},new[]{new Vector2(70,22)}},Approaches=new[]{new[]{new Vector2(25,20)},new[]{new Vector2(65,20)}},PeekPost=new[]{new[]{new Vector2(19,20)},new[]{new Vector2(71,20)}},CoverPost=new[]{new[]{new Vector2(17,20)},new[]{new Vector2(73,20)}}};
  var nav=new DeploymentNavigation(new List<Rect>());
  var vision=new VisionSystem(nav,new VisionSettings{maxRange=15,peripheralRange=5,hearingRange=0},10);
  var combat=new CombatSystem(new CombatSettings(),new WeaponProfile(),10);
  var director=new RoundDirector(new RoundSettings(),layout,10);
  int[] teams=new int[10],composure=new int[10]; bool[] alive=new bool[10];
  Vector2[] positions=new Vector2[10],facing=new Vector2[10],anchors=new Vector2[10];
  for(int i=0;i<10;i++){ teams[i]=i<5?0:1; alive[i]=true; composure[i]=95; facing[i]=new Vector2(1,0); positions[i]=anchors[i]=new Vector2(90,90); }
  positions[0]=anchors[0]=new Vector2(20,20); positions[1]=anchors[1]=new Vector2(20,22);
  // All three remaining defenders are posted on B; closest must rotate, others remain.
  for(int i=2;i<5;i++) { anchors[i]=new Vector2(70,20); positions[i]=new Vector2(40+i*5,20); }
  positions[5]=new Vector2(23,20); positions[6]=new Vector2(24,20);
  director.Begin(1,teams,0);
  director.Tick(.05f,positions,teams,0,anchors,composure,vision,combat);
  if(director.Objective(2).task==PlayerTask.Rotate) throw new Exception("Backup used hidden enemies");
  for(int t=0;t<20;t++) vision.Tick(.05f,positions,facing,teams,alive);
  director.Tick(.05f,positions,teams,0,anchors,composure,vision,combat);
  if(director.PlantedSite>=0||director.Objective(2).task!=PlayerTask.Rotate||Vector2.Distance(director.Objective(2).destination,layout.Sites[0])>5) throw new Exception("Closest site defender failed preplant backup");
  if(director.Objective(3).task==PlayerTask.Rotate||director.Objective(4).task==PlayerTask.Rotate) throw new Exception("Excessive backup abandoned quiet site");
  positions[2]=director.Objective(2).destination;
  director.Tick(.05f,positions,teams,0,anchors,composure,vision,combat);
  if(director.Objective(2).task!=PlayerTask.DefendSite) throw new Exception("Backup did not hold upon arrival");
  // Expired contacts cannot keep the support assignment forever.
  positions[5]=positions[6]=new Vector2(90,90);
  for(int t=0;t<180;t++){ vision.Tick(.05f,positions,facing,teams,alive); director.Tick(.05f,positions,teams,0,anchors,composure,vision,combat); }
  if(Vector2.Distance(director.Objective(2).destination,layout.Sites[1])>5) throw new Exception("Backup did not release stale information");
  director.Begin(1,teams,0); vision.Reset();
  vision.ReportSound(0,5,0,new Vector2(23,20)); vision.ReportSound(0,6,0,new Vector2(24,20));
  director.Tick(.05f,positions,teams,0,anchors,composure,vision,combat);
  if(director.Objective(2).task==PlayerTask.Rotate) throw new Exception("Anonymous sounds became confirmed headcount");
  var sounds=new MatchSounds();
  for(int n=0;n<6;n++) sounds.Emit(5,new Vector2(22,20),SoundKind.Footstep);
  sounds.Tick(.05f,positions,teams,alive,nav,vision);
  if(sounds.AttackSignal(0,layout.Sites[0],20,12)) throw new Exception("Repeated single footsteps or listeners inflated crowd");
  sounds=new MatchSounds();
  sounds.Emit(5,new Vector2(22,14),SoundKind.Footstep);
  sounds.Emit(6,new Vector2(22,22),SoundKind.Footstep);
  sounds.Tick(.05f,positions,teams,alive,nav,vision);
  if(sounds.AttackSignal(0,layout.Sites[0],20,12)) throw new Exception("Two footstep tracks triggered three-track threshold");
  sounds.Emit(7,new Vector2(22,30),SoundKind.Footstep);
  sounds.Tick(.05f,positions,teams,alive,nav,vision);
  if(!sounds.AttackSignal(0,layout.Sites[0],20,12)) throw new Exception("Three audible tracks did not signal attack");
  positions[2]=new Vector2(50,20); director.Begin(1,teams,0); director.SoundIntel=sounds; vision.Reset();
  director.Tick(.05f,positions,teams,0,anchors,composure,vision,combat);
  if(director.Objective(2).task!=PlayerTask.Rotate) throw new Exception("Audible crowd did not request backup");
  sounds.Tick(1.3f,positions,teams,alive,nav,vision);
  if(sounds.AttackSignal(0,layout.Sites[0],20,12)) throw new Exception("Crowd signal never expires");
  sounds=new MatchSounds();
  sounds.Emit(5,layout.Sites[0],SoundKind.Throw); sounds.Emit(6,layout.Sites[0],SoundKind.Throw);
  sounds.EmitImpact(0,layout.Sites[0]); sounds.EmitImpact(1,layout.Sites[0]);
  sounds.Tick(.05f,positions,teams,alive,nav,vision);
  if(sounds.AttackSignal(0,layout.Sites[0],20,12)) throw new Exception("Launch or own utility became enemy landing signal");
  sounds.EmitImpact(5,layout.Sites[0]); sounds.Tick(.05f,positions,teams,alive,nav,vision);
  if(sounds.AttackSignal(0,layout.Sites[0],20,12)) throw new Exception("Multiple listeners counted one landing twice");
  sounds.EmitImpact(6,layout.Sites[0]); sounds.Tick(.05f,positions,teams,alive,nav,vision);
  if(!sounds.AttackSignal(0,layout.Sites[0],20,12)) throw new Exception("Two landings did not signal attack");
  positions[2]=new Vector2(50,20); director.Begin(1,teams,0); director.SoundIntel=sounds; vision.Reset();
  director.Tick(.05f,positions,teams,0,anchors,composure,vision,combat);
  if(director.Objective(2).task!=PlayerTask.Rotate||vision.KnownCount(0)!=0) throw new Exception("Landing failed backup or revealed thrower");
  sounds.Tick(3.1f,positions,teams,alive,nav,vision);
  if(sounds.AttackSignal(0,layout.Sites[0],20,12)) throw new Exception("Landing signal never expires");
  sounds=new MatchSounds();
  for(int i=0;i<5;i++) alive[i]=false;
  sounds.EmitImpact(5,layout.Sites[0]); sounds.EmitImpact(6,layout.Sites[0]);
  sounds.Tick(.05f,positions,teams,alive,nav,vision);
  if(sounds.AttackSignal(0,layout.Sites[0],20,12)) throw new Exception("Dead defenders heard attack signals");
  Debug.Log("BACKUP_SOUND_OK three-tracks, two-landings, dedup, expiry, no-own-no-launch-no-dead-intel");  // Friendly deaths alone must dispatch a cautious support, including a mid-map cluster.
  combat=new CombatSystem(new CombatSettings(),new WeaponProfile{damage=1000,baseSpreadDegrees=0,recoilPerShot=0},10);
  director.Begin(1,teams,0); vision.Reset();
  positions[0]=new Vector2(45,45); positions[1]=new Vector2(46,45); positions[2]=new Vector2(50,20);
  for(int i=0;i<10;i++) alive[i]=true;
  var fire=typeof(CombatSystem).GetMethod("Fire",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
  fire.Invoke(combat,new object[]{5,0,0f,10f,100});
  director.Tick(.05f,positions,teams,0,anchors,composure,vision,combat);
  if(!director.Objective(2).cautious) throw new Exception("Single friendly death did not trigger investigation");
  fire.Invoke(combat,new object[]{5,1,0f,10f,100});
  director.Tick(.05f,positions,teams,0,anchors,composure,vision,combat);
  var backup=director.Objective(2);
  if(!backup.cautious||backup.task!=PlayerTask.Rotate||Vector2.Distance(backup.destination,positions[1])>1||vision.KnownCount(0)!=0) throw new Exception("Friendly-loss cluster did not dispatch cautious support without enemy info");
  if(director.Objective(4).cautious) throw new Exception("Loss risk abandoned last quiet-site guard");
  var brain=new PlayerAutonomy(3,nav);
  var inventory=new PlayerMatchState{equipment=new string[0]};
  var player=new PlayerData{stats=new StatBlock{aim=50,movement=50,utility=50}};
  brain.Decide(2,backup,backup.destination+new Vector2(10,0),player,inventory,combat,vision,0,true);
  if(!brain.Walking[2]) throw new Exception("Support ran into friendly-loss area");
  brain.Footstep(2,backup.destination,3);
  if(brain.Sounds.Emitted[(int)SoundKind.Footstep]!=0) throw new Exception("Cautious approach made running footsteps");
  brain.Decide(2,backup,backup.destination+new Vector2(30,0),player,inventory,combat,vision,0,true);
  if(brain.Walking[2]) throw new Exception("Distant support walked the whole map");
  for(int tick=0;tick<800;tick++) director.Tick(.05f,positions,teams,0,anchors,composure,vision,combat);
  if(director.Objective(2).cautious) throw new Exception("Loss risk never expired");
  director.Begin(2,teams,0); combat.Reset(2);
  director.Tick(.05f,positions,teams,0,anchors,composure,vision,combat);
  if(director.Objective(2).cautious) throw new Exception("Loss risk survived new round");
  Debug.Log("BACKUP_LOSS_OK no-vision, single-and-two-deaths, mid-area, quiet-guard, silent-near-approach, expiry, reset");  Debug.Log("BACKUP_ALL_OK preplant, nearest, quiet-site-guard, arrival, expiry, no-hidden-or-sound-headcount");
 }
}