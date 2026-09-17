using System;
using System.Reflection;
using FpsManager;
using UnityEngine;
public static class GunfireChecks
{
 public static void Run()
 {
  var fire=typeof(CombatSystem).GetMethod("Fire",BindingFlags.NonPublic|BindingFlags.Instance);
  float low=0,high=0;
  for(int skill=20;skill<=100;skill+=80)
  {
   var a=new CombatSystem(new CombatSettings{health=100000},WeaponCatalog.Find("ak_47"),2);
   var b=new CombatSystem(new CombatSettings{health=100000},WeaponCatalog.Find("ak_47"),2);
   int events=0;float energy=0;a.VisualShot=(i,k)=>{if(i!=0)throw new Exception("Wrong firing actor");events++;energy+=k.sqrMagnitude;};
   for(int n=0;n<20;n++){fire.Invoke(a,new object[]{0,1,0f,15f,skill});fire.Invoke(b,new object[]{0,1,0f,15f,skill});}
   if(events!=20||a.Health(1)!=b.Health(1)||a.Hits!=b.Hits)throw new Exception("Visual event changed combat");
   if(skill==20)low=energy;else high=energy;
  }
  if(!(low>high&&high>0))throw new Exception("Real shot recoil ignores aim or becomes laser");
  Debug.Log("GUNFIRE_ALL_OK real-shot event, deterministic combat unchanged, aim-dependent nonzero recoil");
 }
}
