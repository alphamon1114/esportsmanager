using System;using System.Collections.Generic;using System.Reflection;using FpsManager;using UnityEngine;
public static class UtilityDecisionChecks
{
 sealed class Case
 {
  public DeploymentNavigation nav=new DeploymentNavigation(new List<Rect>());
  public Vector2[] p=new Vector2[10],f=new Vector2[10];public int[] teams=new int[10];public bool[] alive=new bool[10];
  public PlayerAutonomy ai;public VisionSystem vision;public CombatSystem combat;
  public PlayerData player=new PlayerData{handle="test",stats=new StatBlock{utility=100}};
  public PlayerMatchState inventory=new PlayerMatchState();
  public PlayerObjective order=new PlayerObjective{valid=true,task=PlayerTask.PushSite,destination=new Vector2(38,20)};
  public Case(bool heard=true,SoundKind kind=SoundKind.Footstep)
  {
   for(int i=0;i<10;i++){p[i]=new Vector2(90,90);f[i]=new Vector2(1,0);teams[i]=i<5?0:1;alive[i]=true;}
   p[0]=new Vector2(20,20);p[5]=new Vector2(36,20);
   vision=new VisionSystem(nav,new VisionSettings{hearingRange=0},10);vision.LegacyFootsteps=false;
   combat=new CombatSystem(new CombatSettings(),new WeaponProfile(),10);ai=new PlayerAutonomy(1,nav);ai.SetUtilityContext(teams,p);
   if(heard){ai.Sounds.Emit(kind==SoundKind.Beep?-1:5,p[5],kind);ai.Sounds.Tick(0,p,teams,alive,nav,vision);}
  }
  public void Decide(bool ct=false){ai.Decide(0,order,p[0],player,inventory,combat,vision,0,ct);}
 }
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 public static void Run()
 {
  var flash=new Case();flash.Decide();Check(flash.ai.Throws==1&&!flash.ai.UtilityAt(0).smoke,"Entry did not flash likely enemy");
  flash.ai.Tick(1.01f,flash.p,flash.f,flash.alive);Check(flash.ai.Blinded[5]&&!flash.ai.Blinded[0],"Flash did not affect enemy safely");
  var ally=new Case();ally.p[1]=new Vector2(40,20);ally.Decide();Check(ally.ai.Throws==0&&ally.inventory.equipment.Length==3,"Flash endangered teammate");
  var defender=new Case(false);defender.p[6]=new Vector2(37,22);for(int tick=0;tick<10;tick++)defender.vision.Tick(.05f,defender.p,defender.f,defender.teams,defender.alive);defender.order.task=PlayerTask.DefendSite;defender.order.destination=defender.p[0];defender.Decide(true);
  Check(defender.ai.Throws==1&&defender.ai.UtilityAt(0).smoke,"Defender did not block incoming lane");
  defender.ai.Tick(1.01f,defender.p,defender.f,defender.alive);Check(!defender.ai.ClearSight(defender.p[0],defender.p[5]),"Defensive smoke misses lane");
  var quietDefender=new Case();quietDefender.order.task=PlayerTask.DefendSite;quietDefender.Decide(true);Check(quietDefender.ai.Throws==0,"One footstep exhausted defensive smoke");
  var postPlant=new Case(false);postPlant.p[6]=new Vector2(37,22);for(int tick=0;tick<10;tick++)postPlant.vision.Tick(.05f,postPlant.p,postPlant.f,postPlant.teams,postPlant.alive);postPlant.order.task=PlayerTask.HoldSite;postPlant.Decide(false);Check(postPlant.ai.Throws==1&&postPlant.ai.UtilityAt(0).smoke,"Attacker post-plant hold cannot smoke incoming retake");
  var retreat=new Case();retreat.order.task=PlayerTask.FallBack;retreat.order.disengage=true;retreat.order.destination=new Vector2(10,20);retreat.Decide();Check(retreat.ai.Throws==1&&retreat.ai.UtilityAt(0).smoke,"Retreat not concealed");
  var crossing=new Case(false);crossing.p[5]=new Vector2(42,20);crossing.ai.Sounds.Emit(5,crossing.p[5],SoundKind.Gunshot);crossing.ai.Sounds.Tick(2,crossing.p,crossing.teams,crossing.alive,crossing.nav,crossing.vision);crossing.order.destination=new Vector2(20,40);crossing.Decide();Check(crossing.ai.Throws==1&&crossing.ai.UtilityAt(0).smoke,"Crossfire not screened");
  var direct=new Case();direct.inventory.equipment=new[]{"smoke"};direct.Decide();Check(direct.ai.Throws==0,"Smoke blocked our own straight entry");
  foreach(var kind in new[]{SoundKind.Beep,SoundKind.Throw}){var noise=new Case(true,kind);noise.Decide();Check(noise.ai.Throws==0,"Non-enemy cue triggered utility");}
  var stale=new Case();stale.ai.Sounds.Tick(2,stale.p,stale.teams,stale.alive,stale.nav,stale.vision);stale.Decide();Check(stale.ai.Throws==0,"Stale sound triggered utility");
  var noIntel=new Case(false);noIntel.Decide();Check(noIntel.ai.Throws==0,"Hidden transforms triggered utility");
  var wiped=new Case();wiped.combat.Equip(0,new WeaponProfile{damage=10000,baseSpreadDegrees=0,recoilPerShot=0});
  var fire=typeof(CombatSystem).GetMethod("Fire",BindingFlags.NonPublic|BindingFlags.Instance);
  for(int i=5;i<10;i++)fire.Invoke(wiped.combat,new object[]{0,i,0f,10f,100});
  wiped.Decide();Check(wiped.ai.Throws==0&&wiped.inventory.equipment.Length==3,"Dead enemy team left utility trigger behind");
  var defuse=new Case();defuse.order.task=PlayerTask.Defuse;defuse.Decide();Check(defuse.ai.Throws==0,"Utility interrupted bomb interaction");
  var repeat=new Case();repeat.Decide();repeat.p[1]=repeat.p[0];repeat.ai.Decide(1,repeat.order,repeat.p[1],repeat.player,new PlayerMatchState{equipment=new[]{"flash"}},repeat.combat,repeat.vision,0,false);Check(repeat.ai.Throws==1,"Duplicate entry flash");
  var farAlly=new Case();farAlly.p[1]=new Vector2(55,20);farAlly.Decide();Check(farAlly.ai.Throws==0,"Flash ignored ally beyond target");
  var blocked=new Case();blocked.nav=new DeploymentNavigation(new List<Rect>{new Rect(39,0,1,100)});blocked.ai=new PlayerAutonomy(1,blocked.nav);blocked.ai.SetUtilityContext(blocked.teams,blocked.p);blocked.ai.Sounds.Emit(5,blocked.p[5],SoundKind.Gunshot);blocked.ai.Sounds.Tick(0,blocked.p,blocked.teams,blocked.alive,blocked.nav,blocked.vision);blocked.Decide();Check(blocked.ai.Throws==0,"Flash crossed wall");
  for(int seed=0;seed<30;seed++)
  {
   var inaccurate=new Case();inaccurate.player.stats.utility=0;inaccurate.ai=new PlayerAutonomy(seed,inaccurate.nav);inaccurate.ai.SetUtilityContext(inaccurate.teams,inaccurate.p);
   inaccurate.ai.Sounds.Emit(5,inaccurate.p[5],SoundKind.Gunshot);inaccurate.ai.Sounds.Tick(0,inaccurate.p,inaccurate.teams,inaccurate.alive,inaccurate.nav,inaccurate.vision);inaccurate.Decide();
   if(inaccurate.ai.Throws>0)Check(Vector2.Distance(inaccurate.p[0],inaccurate.ai.UtilityAt(0).position)>=20.1f,"Throw error bypassed safety");
  }
  Debug.Log("UTILITY_DECISIONS_ALL_OK defensive lane, retreat, crossfire, safe entry flash, no friendly flash, no own-lane smoke, wiped team, stale/beep/throw/no intel, interaction priority, duplicate");
 }
}