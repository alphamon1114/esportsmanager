using System;
using System.Collections.Generic;
using System.Reflection;
using FpsManager;
using UnityEngine;
public static class MagazineChecks
{
 static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
 public static void Run()
 {
  var fire=typeof(CombatSystem).GetMethod("Fire",BindingFlags.NonPublic|BindingFlags.Instance);
  foreach(string id in WeaponCatalog.Ids)
  {
   var w=WeaponCatalog.Find(id); w.damage=0;
   var c=new CombatSystem(new CombatSettings(),w,2); c.AmmoEnabled=true;
   var p=new[]{new Vector2(10,10),new Vector2(40,40)};
   var f=new[]{new Vector2(1,0),new Vector2(1,0)};
   var teams=new[]{0,1}; var ready=new[]{false,false}; var aim=new[]{50,50};
   var vision=new VisionSystem(new DeploymentNavigation(new List<Rect>()),new VisionSettings(),2);
   int starts=0; c.ReloadStarted=i=>starts++;
   c.RequestReload(0); Check(!c.Reloading(0)&&c.SpareMagazines(0)==3,"Full magazine reloaded");
   for(int mag=0;mag<3;mag++)
   {
    fire.Invoke(c,new object[]{0,1,0f,10f,50});
    Check(c.Magazine(0)==w.magazineSize-1,"Shot consumption");
    Check(!c.ShouldReload(0,false),"Wasteful top-off: "+id);
    c.RequestReload(0); c.RequestReload(0);
    Check(c.Magazine(0)==0&&c.SpareMagazines(0)==2-mag&&starts==mag+1,"Discard/double consumption");
    int shots=c.Shots;
    c.Tick(w.reloadSeconds/2,p,f,teams,ready,aim,vision);
    Check(c.Reloading(0)&&c.Magazine(0)==0&&c.Shots==shots,"Early reload/fire");
    c.Tick(w.reloadSeconds,p,f,teams,ready,aim,vision);
    Check(!c.Reloading(0)&&c.Magazine(0)==w.magazineSize,"Full replacement failed");
   }
   for(int n=0;n<w.magazineSize;n++) fire.Invoke(c,new object[]{0,1,0f,10f,50});
   int before=c.Shots;
   for(int n=0;n<20;n++) c.Tick(.1f,p,f,teams,new[]{true,true},aim,vision);
   Check(c.Magazine(0)==0&&c.SpareMagazines(0)==0&&!c.Reloading(0)&&c.Shots==before,"Exhaustion guard");
   c.Reset(1); Check(c.Magazine(0)==w.magazineSize&&c.SpareMagazines(0)==3,"Round restock");
   for(int n=0;n<w.magazineSize-1;n++) fire.Invoke(c,new object[]{0,1,0f,10f,50});
   Check(c.ShouldReload(0,false)&&!c.ShouldReload(0,true),"Threat-aware reload");
   fire.Invoke(c,new object[]{0,1,0f,10f,50});
   Check(c.ShouldReload(0,true),"Empty emergency reload");
   c.RequestReload(0); c.Reset(2);
   Check(!c.Reloading(0)&&c.Magazine(0)==w.magazineSize&&c.SpareMagazines(0)==3,"Pending reload survived reset");
  }
  var unarmed=new CombatSystem(null,WeaponCatalog.Equipped(new string[0]),2);
  Check(unarmed.Magazine(0)==0&&unarmed.SpareMagazines(0)==0,"Unarmed reserves");
  Debug.Log("MAGAZINE_ALL_OK: 18 guns, discard, duplicate request, timing, exhaustion, AI, reset");
 }
}