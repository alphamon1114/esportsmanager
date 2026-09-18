using System;using System.Reflection;using FpsManager;using UnityEngine;
public static class HumanAimChecks {
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 public static void Run(){
  var aim=new HumanAim(123);var replay=new HumanAim(123);var facing=new Vector2(1,0);var other=facing;var position=Vector2.zero;var target=new Vector2(0,20);
  for(int n=0;n<120;n++){var previous=facing;facing=aim.Step(1f/60,1,position,facing,target,85);other=replay.Step(1f/60,1,position,other,target,85);Check((facing-other).sqrMagnitude<.000001f,"aim replay changed");if(n<6)Check((facing-new Vector2(1,0)).sqrMagnitude<.000001f,"no perception delay");Check(Mathf.Abs(CombatSystem.SignedAngle(previous,facing))<3.5f,"instant aim snap");}
  Check(Mathf.Abs(CombatSystem.SignedAngle(facing,target))<2,"failed to settle on visible target");
  var before=facing;facing=aim.Step(1f/60,2,position,facing,new Vector2(-20,0),85);Check((before-facing).sqrMagnitude<.000001f,"target switch has no delay");
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;var fire=typeof(CombatSystem).GetMethod("Fire",flags);
  int body=0,head=0;for(int seed=0;seed<200;seed++){
   var gun=WeaponCatalog.Find("ak_47");gun.baseSpreadDegrees=0;gun.recoilPerShot=0;
   var c=new CombatSystem(new CombatSettings{health=10000},gun,2);c.Reset(seed);c.PhysicalBullets=true;c.FeetHeight=i=>0;
   typeof(CombatSystem).GetField("shotPositions",flags).SetValue(c,new[]{Vector2.zero,new Vector2(20,0)});typeof(CombatSystem).GetField("shotTeams",flags).SetValue(c,new[]{0,1});
   var pitch=(float[])typeof(CombatSystem).GetField("aimElevation",flags).GetValue(c);
   pitch[0]=Mathf.Atan2(1.15f-1.65f,20)/Mathf.Deg2Rad;fire.Invoke(c,new object[]{0,1,0f,20f,85});if(c.LastHit(0)==HitRegion.Body)body++;
   pitch[0]=Mathf.Atan2(1.75f-1.65f,20)/Mathf.Deg2Rad;fire.Invoke(c,new object[]{0,1,0f,20f,85});if(c.LastHit(0)==HitRegion.Head)head++;
  }
  Check(body==200&&head==200,"bullet changed aim height independently of real crosshair");
  Debug.Log("HUMAN_AIM_ALL_OK perception/switch delay, bounded rotation, convergence, deterministic correction, actual pitch body/head 200/200");
 }
}
