using System;
using System.Reflection;
using System.Collections.Generic;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class WeaponDropChecks
{
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 static object Call(Prototype g,string name,params object[] args){return typeof(Prototype).GetMethod(name,Flags).Invoke(g,args);}
 static void Give(Prototype g,int i,string gun,int rounds,int spares){g.MatchState(i).equipment=new[]{"glock_18",gun};g.Combat.EquipSaved(i,gun,new WeaponAmmo{rounds=rounds,spares=spares});}
 public static void Run()
 {
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var g=new GameObject("Drop checks").AddComponent<Prototype>();g.Initialize();g.StartMatch();g.AdvanceFrame(Prototype.BuySeconds+.01f);
  var actors=(List<GameObject>)typeof(Prototype).GetField("actors",Flags).GetValue(g);
  actors[1].transform.position=actors[0].transform.position;
  Give(g,0,"ak_47",7,1);Give(g,1,"m4a1_s",3,0);
  Check(g.DropWeapon(0)&&g.GroundWeaponCount==1,"manual drop");var drop=g.GroundWeaponAt(0);
  Check(drop.weapon=="ak_47"&&drop.ammo.rounds==7&&drop.ammo.spares==1&&Array.IndexOf(g.MatchState(0).equipment,"ak_47")<0,"drop snapshot and inventory removal");
  Check(g.TryPickUpWeapon(1,drop)&&g.Combat.WeaponFor(1).id=="ak_47"&&g.Combat.Magazine(1)==7&&g.Combat.SpareMagazines(1)==1,"pickup transfers exact ammo");
  Check(g.GroundWeaponCount==1&&g.GroundWeaponAt(0).weapon=="m4a1_s"&&g.GroundWeaponAt(0).ammo.rounds==3&&!g.TryPickUpWeapon(0,drop),"exchange drops old weapon and single claim");
  Check(WeaponDropRules.Wants("glock_18",20,3,"ak_47")&&!WeaponDropRules.Wants("ak_47",30,3,"glock_18")&&WeaponDropRules.Wants("ak_47",2,0,"m4a1_s"),"strength and own ammo rules");
  // Empty gun is still taken: the decision is never allowed to inspect its hidden ammo.
  Call(g,"ClearGroundWeapons");Give(g,0,"awp",0,0);g.DropWeapon(0);drop=g.GroundWeaponAt(0);Give(g,1,"glock_18",20,3);
  Check((bool)Call(g,"CollectNearbyWeapon",1,new PlayerObjective{valid=true,task=PlayerTask.DefendSite},.1f)&&g.Combat.WeaponFor(1).id=="awp"&&g.Combat.Magazine(1)==0&&g.Combat.SpareMagazines(1)==0,"AI rejected hidden empty ammo or refilled it");
  Call(g,"ClearGroundWeapons");Give(g,0,"ak_47",1,2);g.Combat.RequestReload(0);g.DropWeapon(0);drop=g.GroundWeaponAt(0);
  Check(drop.ammo.rounds==0&&drop.ammo.spares==1&&drop.ammo.reloadRemaining>0,"reload committed magazine missing");
  Check(g.TryPickUpWeapon(1,drop)&&g.Combat.Reloading(1)&&g.Combat.SpareMagazines(1)==1,"reload snapshot pickup");
  Call(g,"ClearGroundWeapons");Give(g,0,"awp",5,3);Give(g,2,"ak_47",11,2);
  typeof(CombatSystem).GetMethod("ApplyHit",Flags).Invoke(g.Combat,new object[]{0,2,HitRegion.Head});
  Check(!g.Combat.Alive(2)&&g.GroundWeaponCount==1&&g.GroundWeaponAt(0).ammo.rounds==11&&Array.IndexOf(g.MatchState(2).equipment,"ak_47")<0,"real death drops strongest once");
  Check(!g.DropWeapon(2),"dead player can manually duplicate drop");
  g.PrepareNextRound();Check(g.GroundWeaponCount==0,"round reset left ground loot");g.BeginRound();
  // Danger is based on shared known information, not true enemy positions.
  Give(g,0,"ak_47",10,1);g.DropWeapon(0);drop=g.GroundWeaponAt(0);actors[1].transform.position=actors[0].transform.position;
  var autonomy=(PlayerAutonomy)typeof(Prototype).GetField("autonomy",Flags).GetValue(g);autonomy.Blinded[1]=true;
  Check(!g.TryPickUpWeapon(1,drop),"blinded pickup allowed");autonomy.Blinded[1]=false;
  actors[1].transform.position+=Vector3.up*3;Check(!g.TryPickUpWeapon(1,drop),"pickup through floor");
  // Force/Eco support picks the current match's strongest result, keeps the donor's own gun,
  // and reserves the donor's next full buy budget in an eco.
  g.PrepareNextRound();typeof(Prototype).GetProperty("CompletedRounds").SetValue(g,3,null);
  var plans=(BuyPlan[])typeof(Prototype).GetField("buyPlans",Flags).GetValue(g);plans[0]=BuyPlan.Eco;plans[1]=BuyPlan.Full;
  for(int i=0;i<10;i++){g.MatchState(i).credits=1900;g.MatchState(i).equipment=new[]{"glock_18"};g.Combat.Equip(i,WeaponCatalog.Find("glock_18"));g.Statistics.Result(i).rounds=3;}
  g.Statistics.Result(1).kills=12;g.Statistics.Result(1).damage=1000;g.MatchState(0).credits=6000;Give(g,0,"ak_47",30,3);
  int donorBefore=g.MatchState(0).credits;Call(g,"PlanAceSupport");Check(g.GroundWeaponCount==1&&g.GroundWeaponAt(0).receiver==1&&g.GroundWeaponAt(0).owner==0,"ace selection");
  Check(g.MatchState(0).credits==donorBefore-2900&&Array.IndexOf(g.MatchState(0).equipment,"ak_47")>=0,"support charge or donor gun changed");
  typeof(Prototype).GetProperty("Stage").SetValue(g,MatchStage.Buying,null);Call(g,"DeliverSupport",1f);
  Check(g.Combat.WeaponFor(1).id=="m4a1_s"&&g.Combat.Magazine(1)==20,"support did not use drop pickup");
  Call(g,"ClearGroundWeapons");g.MatchState(1).equipment=new[]{"glock_18"};g.MatchState(0).equipment=new[]{"glock_18"};g.MatchState(0).credits=4000;
  Call(g,"PlanAceSupport");Check(g.GroundWeaponCount==0,"eco support consumed next full buy reserve");
  Debug.Log("WEAPON_DROP_ALL_OK manual/death, exact ammo, reload, empty unknown gun, exchange, single claim, safety, floor, round reset, ace support and eco reserve");
 }
}
