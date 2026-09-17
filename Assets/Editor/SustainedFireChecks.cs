using System;
using System.Collections.Generic;
using System.Reflection;
using FpsManager;
using UnityEngine;
public static class SustainedFireChecks
{
 static readonly MethodInfo Fire=typeof(CombatSystem).GetMethod("Fire",BindingFlags.NonPublic|BindingFlags.Instance);
 static int Hits(int aim,float distance,bool rest,out int early)
 {
  int late=0;early=0;
  var nav=new DeploymentNavigation(new List<Rect>());
  for(int seed=1;seed<=200;seed++)
  {
   var gun=WeaponCatalog.Find("ak_47"); gun.damage=0;
   var c=new CombatSystem(new CombatSettings(),gun,2);c.Reset(seed);
   var vision=new VisionSystem(nav,new VisionSettings(),2);
   var p=new[]{new Vector2(20,20),new Vector2(20+distance,20)};
   var f=new[]{new Vector2(1,0),new Vector2(-1,0)};
   for(int shot=0;shot<30;shot++)
   {
    if(rest&&shot>0&&shot%3==0)
     for(int tick=0;tick<16;tick++) c.Tick(.05f,p,f,new[]{0,1},new[]{false,false},new[]{aim,aim},vision);
    Fire.Invoke(c,new object[]{0,1,0f,distance,aim});
    if(c.LastHit(0)!=HitRegion.Miss) { if(shot<5)early++;if(shot>=15)late++; }
   }
  }
  return late;
 }
 public static void Run()
 {
  int firstLow,firstHigh,firstTop,unused;
  int low=Hits(50,25,false,out firstLow), high=Hits(85,25,false,out firstHigh),top=Hits(100,25,false,out firstTop);
  int reset=Hits(85,25,true,out unused);
  // First 5 and last 15 have different denominators (1000 and 3000 shots).
  if(high>=firstHigh*3*.6f||top>=firstTop*3*.75f) throw new Exception("Late spray remains too close to fresh-shot accuracy");
  if(!(low<high&&high<top&&reset>high*1.5f)) throw new Exception("Aim or deliberate burst recovery has no effect");
  int near=Hits(100,10,false,out unused);
  if(near>=2700) throw new Exception("High aim became a close-range sustained laser");
  // Killing and then switching targets is not a recoil-reset event.
  var c=new CombatSystem(new CombatSettings(),new WeaponProfile{damage=1000,baseSpreadDegrees=0},3);
  Fire.Invoke(c,new object[]{0,1,0f,10f,85});float before=c.Recoil(0);
  Fire.Invoke(c,new object[]{0,2,0f,10f,85});
  if(c.Alive(1)||c.Recoil(0)<=before) throw new Exception("Kill/target switch cleared accumulated recoil");
  // A larger magazine may not stretch the early pattern into almost no recoil.
  var ak=WeaponCatalog.Find("ak_47");var enlarged=WeaponCatalog.Find("ak_47");enlarged.magazineSize=60;
  if(SprayPatterns.Raw(ak,8)!=SprayPatterns.Raw(enlarged,8)) throw new Exception("Magazine size changes early recoil");
  Debug.Log("SUSTAINED_FIRE_ALL_OK late hits /3000 aim50="+low+" aim85="+high+" aim100="+top+" rested85="+reset+"; early/late, near-range floor, recovery, kill transfer, magazine independence");
 }
}