using System;using System.Reflection;using System.Collections.Generic;using FpsManager;using UnityEngine;
public static class TrustedDefuseChecks {
 static BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 public static void Run(){
  var g=new GameObject("trusted defuse").AddComponent<Prototype>();g.Initialize();g.StartMatch();g.AdvanceFrame(Prototype.BuySeconds+.01f);
  var d=g.Director;var c=g.Combat;var actors=(List<GameObject>)typeof(Prototype).GetField("actors",F).GetValue(g);
  var teams=new int[10];var positions=new Vector2[10];var facing=new Vector2[10];
  var defenders=new List<int>();var enemies=new List<int>();
  for(int i=0;i<10;i++){teams[i]=g.TeamIndexOf(i);if(teams[i]==g.CounterTerroristTeam)defenders.Add(i);else enemies.Add(i);}
  int ace=defenders[0],awper=defenders[1],chosen=defenders[2];
  Set(d,"Phase",RoundPhase.PostPlant);Set(d,"PlantedSite",0);Set(d,"BombPosition",new Vector2(30,88));Set(d,"BombTimer",35f);
  d.InteractionAllowed=null;
  for(int i=0;i<10;i++){
   g.Data.players[i].stats=new StatBlock{aim=50,utility=50,movement=50,mental=50,composure=50};g.Data.players[i].weaponPosition="rifler";
   positions[i]=new Vector2(32+i*.1f,88);facing[i]=new Vector2(-1,0);actors[i].transform.position=new Vector3(positions[i].x,1,100-positions[i].y);
  }
  g.Data.players[ace].stats=new StatBlock{aim=100,utility=100,movement=100,mental=100,composure=100};g.Data.players[awper].weaponPosition="awper";
  var vision=new VisionSystem(new DeploymentNavigation(new List<Rect>()),new VisionSettings(),10);typeof(Prototype).GetField("vision",F).SetValue(g,vision);
  Check((int)Call(g,"ChooseTrustedDefuser")==-1,"equal counts triggered trust");
  c.Equip(ace,new WeaponProfile{damage=1000});for(int n=0;n<3;n++)Call(c,"ApplyHit",ace,enemies[n],HitRegion.Head);
  Check((int)Call(g,"ChooseTrustedDefuser")==chosen,"closest non-ace/non-awper not chosen");
  Call(d,"UpdatePlantedBomb",.1f,positions,teams,g.CounterTerroristTeam,c);
  Check(d.Defuser==chosen&&d.CommittedDefuse,"trusted defuse not committed");
  float progress=d.DefuseProgress;Call(g,"RegisterHurt",enemies[4],chosen);
  Check(d.Defuser==chosen&&d.DefuseProgress==progress,"damage interrupted trusted defuse");
  Check(!(bool)Call(g,"StepThreatResponse",chosen,.1f)&&(bool)Call(g,"StepDefusing",chosen),"committed defuser reacted to damage");
  Call(d,"UpdatePlantedBomb",.1f,positions,teams,g.CounterTerroristTeam,c);Check(d.DefuseProgress>progress,"trusted progress stopped after damage");
  vision.Tick(.5f,positions,facing,teams);
  int attacker=enemies[4];
  Check((int)Call(g,"PostPlantTargetPriority",attacker,chosen)==-1,"T did not deprioritize visible defuser");
  Set(d,"BombTimer",10f);Check((int)Call(g,"PostPlantTargetPriority",attacker,chosen)==1,"10-second deadline did not prioritize defuser");
  vision.PairGeometry=(i,j,a,b)=>j!=chosen;vision.Tick(.5f,positions,facing,teams);
  Check((int)Call(g,"PostPlantTargetPriority",attacker,chosen)==0,"hidden defuser identity leaked");
  c.Equip(attacker,new WeaponProfile{damage=1000});Call(c,"ApplyHit",attacker,chosen,HitRegion.Head);
  Call(d,"UpdatePlantedBomb",.1f,positions,teams,g.CounterTerroristTeam,c);Check(d.Defuser!=chosen,"dead defuser retained ownership");
  // Once the attackers are eliminated, stale intel must not delay a direct run to C4.
  c.Equip(ace,new WeaponProfile{damage=1000});foreach(int enemy in enemies)if(c.Alive(enemy))Call(c,"ApplyHit",ace,enemy,HitRegion.Head);
  actors[ace].transform.position=new Vector3(36,1,12);var before=g.MapPosition(ace);
  Check((bool)Call(g,"StepUncontestedDefuse",ace,.1f),"all-dead objective bypass missing");
  Check(Vector2.Distance(before,g.MapPosition(ace))>.001f&&c.KnifeOut(ace),"CT did not run directly after wipe");
  Check((bool)Call(g,"SafeToDefuse",ace),"stale information blocks uncontested defuse");
  // Actual stable engagement selection must obey priority changes, not oscillate by distance.
  var fight=new CombatSystem(new CombatSettings(),new WeaponProfile{damage=0},3){UnifiedAim=true};
  var sight=new VisionSystem(new DeploymentNavigation(new List<Rect>()),new VisionSettings(),3);
  var ps=new[]{new Vector2(20,20),new Vector2(30,20),new Vector2(31,21)};var fs=new[]{new Vector2(1,0),new Vector2(-1,0),new Vector2(-1,0)};var ts=new[]{0,1,1};
  fight.TargetPriority=(i,e)=>i==0&&e==1?-1:0;sight.Tick(.5f,ps,fs,ts);fight.UpdateEngagementFocus(.1f,ps,fs,ts,sight);Check(fight.FocusTarget(0)==2,"guard not selected");
  fight.TargetPriority=(i,e)=>i==0&&e==1?1:0;fight.UpdateEngagementFocus(.1f,ps,fs,ts,sight);Check(fight.FocusTarget(0)==1,"deadline failed to switch existing focus");
  Debug.Log("TRUSTED_DEFUSE_ALL_OK selection, advantage, commitment, damage, death, observed identity, deadline and actual target switch");
 }
}
