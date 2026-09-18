using System;using System.Reflection;using System.Collections.Generic;using FpsManager;using UnityEngine;
public static class SafeReloadChecks {
 static BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;static object Field(object g,string n){return g.GetType().GetField(n,F).GetValue(g);}static object Call(object g,string n,params object[] a){return g.GetType().GetMethod(n,F).Invoke(g,a);}static void Check(bool b,string m){if(!b)throw new Exception(m);}
 public static void Run(){
  var g=new GameObject("reload cover").AddComponent<Prototype>();g.Initialize();g.StartMatch();g.AdvanceFrame(7.01f);
  var nav=new DeploymentNavigation(new List<Rect>{new Rect(15,5,2,8)});typeof(Prototype).GetField("navigation",F).SetValue(g,nav);
  var vision=new VisionSystem(nav,new VisionSettings(),10);typeof(Prototype).GetField("vision",F).SetValue(g,vision);
  var actors=(List<GameObject>)Field(g,"actors");for(int i=0;i<10;i++)actors[i].transform.position=new Vector3(80+i*2,101,0);actors[0].transform.position=new Vector3(10,1,85);
  vision.ReportSound(g.TeamIndexOf(0),5,0,new Vector2(20,15));g.Combat.EquipSaved(0,"usp_s",new WeaponAmmo{rounds=0,spares=2});g.Combat.SetKnife(0,false);
  Check(!(bool)Call(g,"CanReloadSafely",0),"exposed reload marked safe");g.Combat.RequestReload(0);Check(!g.Combat.Reloading(0)&&g.Combat.SpareMagazines(0)==2,"exposed request consumed magazine");
  var start=g.MapPosition(0);
  for(int n=0;n<150&&!g.Combat.Reloading(0);n++){
   typeof(RoundDirector).GetProperty("Clock").SetValue(g.Director,n*.05f,null);Call(g,"BeginPlayerMovement");Call(g,"StepSafeReload",0,.05f);Call(g,"ResolvePlayerMovement",.05f);
  }
  Check(g.Combat.Reloading(0),"never reloaded behind cover");Check(Vector2.Distance(start,g.MapPosition(0))>1,"reloaded in place under fire");Check((bool)Call(g,"CanReloadSafely",0),"reload began before line of sight was blocked");Check(g.Combat.SpareMagazines(0)==1,"replacement magazine consumed more than once");
  var post=g.MapPosition(0);Call(g,"StepSafeReload",0,.1f);Check(Vector2.Distance(post,g.MapPosition(0))<.01f,"left cover during reload");
  g.Combat.EquipSaved(0,"usp_s",new WeaponAmmo{rounds=1,spares=2});vision.Reset();g.Combat.RequestReload(0);Check(g.Combat.Reloading(0),"quiet reload blocked");
  Call(g,"ResetSafeReload");foreach(bool valid in (bool[])Field(g,"reloadCoverValid"))Check(!valid,"cover state leaked across round");
  Debug.Log("SAFE_RELOAD_ALL_OK exposed request denied, move behind real blocker, hold while loading, magazine preserved, quiet reload, reset");
 }
}
