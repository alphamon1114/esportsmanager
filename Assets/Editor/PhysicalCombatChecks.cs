using System;using System.Reflection;using System.Collections.Generic;using FpsManager;using UnityEngine;
public static class PhysicalCombatChecks {
 static BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Trace(CombatSystem c,Vector2[] positions,int[] teams,float slope=0){typeof(CombatSystem).GetField("shotPositions",F).SetValue(c,positions);typeof(CombatSystem).GetField("shotTeams",F).SetValue(c,teams);typeof(CombatSystem).GetMethod("TracePlayers",F).Invoke(c,new object[]{0,new Vector2(1,0),slope});}
 public static void Run(){
  Check(PlayerCollision.Blocked(new Vector3(0,0,0),new Vector3(4,0,0),new Vector3(2,0,0)),"body tunnelling");
  Check(!PlayerCollision.Blocked(Vector3.zero,new Vector3(4,0,0),new Vector3(2,3,0)),"separate floors collide");
  Check(!PlayerCollision.Blocked(new Vector3(.5f,0,0),new Vector3(1.5f,0,0),Vector3.zero),"overlap cannot separate");
  var positions=new[]{new Vector2(0,0),new Vector2(5,0),new Vector2(10,0)};var teams=new[]{0,1,1};
  var gun=WeaponCatalog.Find("ak_47");var c=new CombatSystem(new CombatSettings(),gun,3);c.Reset(4);Trace(c,positions,teams,-.02f);
  Check(c.Health(1)<100&&c.Health(2)<100&&100-c.Health(1)>100-c.Health(2),"flesh penetration/falloff");
  c.Reset(4);teams[1]=0;Trace(c,positions,teams,-.02f);Check(c.Health(1)<100&&c.Health(1)>80&&c.Health(2)<100,"friendly damage/penetration");
  c.Reset(4);c.BulletClear=(a,ay,b,by)=>b.x<7;Trace(c,positions,teams,-.02f);Check(c.Health(1)<100&&c.Health(2)==100,"wall failed to stop penetration");
  c.Reset(4);c.BulletClear=null;c.FeetHeight=i=>i==2?3:0;Trace(c,positions,teams,-.02f);Check(c.Health(2)==100,"bullet hit separate floor");
  c.SetFiringSpeed(0,5,.1f);Check(c.MovementSpread(0)>5,"movement accuracy missing");c.SetFiringSpeed(0,0,.2f);Check(c.MovementSpread(0)==0,"stop recovery missing");
  var armor=new PlayerMatchState{armor=100,helmet=true};float damage=ArmorRules.Damage(armor,gun,HitRegion.Body,.33f);Check(damage<gun.damage*.33f&&armor.armor>95,"friendly armor scaling");
  var stats=new MatchStatistics();stats.Begin(new[]{0,0,1,0,0,1,1,1,1,1},0);stats.Damage(0,1,100);stats.Kill(new KillEvent{killer=0,victim=1,region=HitRegion.Head},0);Check(stats.Result(0).kills==0&&stats.Result(0).damage==0&&stats.Result(1).deaths==1,"team kill rewarded in stats");
  int kills=0,heads=0,movingHits=0,stillHits=0;
  var fire=typeof(CombatSystem).GetMethod("Fire",F);var nav=new DeploymentNavigation(new List<Rect>());
  for(int seed=1;seed<=1000;seed++){
   var duel=new CombatSystem(new CombatSettings(),WeaponCatalog.Find("ak_47"),2);duel.Reset(seed);duel.BindProtection(1,new PlayerMatchState{armor=100,helmet=true});var vision=new VisionSystem(nav,new VisionSettings(),2);
   var p=new[]{new Vector2(0,0),new Vector2(20,0)};var face=new[]{new Vector2(1,0),new Vector2(-1,0)};
   for(int shot=0;shot<30&&duel.Alive(1);shot++){duel.Tick(.1f,p,face,new[]{0,1},new[]{false,false},new[]{85,85},vision);fire.Invoke(duel,new object[]{0,1,0f,20f,85});}
   if(!duel.Alive(1)){kills++;if(duel.LastHit(0)==HitRegion.Head)heads++;}
   foreach(bool moving in new[]{false,true}){var test=new CombatSystem(new CombatSettings(),WeaponCatalog.Find("ak_47"),2);test.Reset(seed);if(moving)test.SetFiringSpeed(0,5,.1f);fire.Invoke(test,new object[]{0,1,0f,20f,85});if(test.Hits>0){if(moving)movingHits++;else stillHits++;}}
  }
  Check(movingHits<stillHits*.65f,"running as accurate as standing");Check(kills>100&&heads/(float)kills<.6f,"headshot kill share excessive");
  Debug.Log("PHYSICAL_COMBAT_ALL_OK sweep, floors, flesh, wall, friendly armor/stats; AK aim85 20m armored kills="+kills+" HS="+heads+" ("+(100f*heads/kills).ToString("F1")+"%), fresh standing/moving hits="+stillHits+"/"+movingHits);
 }
}
