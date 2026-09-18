using System;
using System.Reflection;
using System.Collections.Generic;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class EquipmentExchangeChecks
{
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool value,string text){if(!value)throw new Exception(text);}
 static object Call(Prototype g,string name,params object[] args){return typeof(Prototype).GetMethod(name,F).Invoke(g,args);}
 static Prototype Game(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var g=new GameObject("Exchange checks").AddComponent<Prototype>();g.Initialize();g.StartMatch();g.AdvanceFrame(Prototype.BuySeconds+.01f);return g;}
 static VisionSystem Sight(Prototype g,float distance=10)
 {
  var v=new VisionSystem(new DeploymentNavigation(new List<Rect>()),new VisionSettings(),10);
  typeof(Prototype).GetField("vision",F).SetValue(g,v);
  var p=new Vector2[10];var f=new Vector2[10];var t=new int[10];
  for(int i=0;i<10;i++){p[i]=new Vector2(90,90);f[i]=new Vector2(1,0);t[i]=g.TeamIndexOf(i);}
  p[0]=g.MapPosition(0);p[5]=p[0]+new Vector2(distance,0);f[5]=new Vector2(-1,0);v.Tick(.5f,p,f,t);return v;
 }
 public static void Run()
 {
  var g=Game();var sight=Sight(g);g.MatchState(0).equipment=new[]{"ak_47","desert_eagle","flash","smoke"};
  g.Combat.EquipSaved(0,"ak_47",new WeaponAmmo{rounds=0,spares=2});
  g.Combat.DamageDealt(0,5,49);Call(g,"UpdateFinishingCombat",0,.1f);
  Check(g.Combat.WeaponFor(0).id=="ak_47","weak damage triggered pistol finishing");
  g.Combat.DamageDealt(0,5,1);Call(g,"UpdateFinishingCombat",0,.1f);
  Check(g.Combat.WeaponFor(0).id=="desert_eagle","confirmed 50 damage did not trigger pistol finishing");
  g.Combat.EquipSaved(0,"desert_eagle",new WeaponAmmo{rounds=4,spares=1});sight.Reset();Call(g,"UpdateFinishingCombat",0,.1f);
  Check(g.Combat.WeaponFor(0).id=="ak_47"&&g.Combat.Magazine(0)==0&&g.Combat.SpareMagazines(0)==2,"primary return refilled ammo");
  Call(g,"ResetFinishingCombat");sight=Sight(g);g.Combat.DamageDealt(0,5,60);Call(g,"UpdateFinishingCombat",0,5.1f);
  Check(g.Combat.WeaponFor(0).id=="ak_47","stale damage triggered finishing");
  // Attacking player sees a target with no damage information and little time to plant.
  var actors=(List<GameObject>)typeof(Prototype).GetField("actors",F).GetValue(g);
  actors[5].transform.position=actors[0].transform.position+new Vector3(10,0,0);
  g.MatchState(5).equipment=new[]{"ak_47","glock_18"};g.Combat.EquipSaved(5,"ak_47",new WeaponAmmo{rounds=0,spares=1});
  typeof(RoundDirector).GetProperty("Clock").SetValue(g.Director,g.Director.Settings.roundSeconds-8,null);
  Call(g,"UpdateFinishingCombat",5,.1f);Check(g.Combat.WeaponFor(5).id=="glock_18","late attack did not choose sidearm");
  // No usable pistol: no free weapon, no forced swap.
  Call(g,"ResetFinishingCombat");g.MatchState(5).equipment=new[]{"ak_47"};g.Combat.EquipSaved(5,"ak_47",new WeaponAmmo{rounds=0,spares=1});
  Call(g,"UpdateFinishingCombat",5,.1f);Check(g.Combat.WeaponFor(5).id=="ak_47","finisher invented pistol");
  // Picking up a primary while holding a Deagle replaces only the primary slot.
  g=Game();actors=(List<GameObject>)typeof(Prototype).GetField("actors",F).GetValue(g);actors[1].transform.position=actors[0].transform.position;
  g.MatchState(0).equipment=new[]{"desert_eagle","smoke","flash"};g.MatchState(0).defuseKit=true;g.Combat.EquipSaved(0,"desert_eagle",new WeaponAmmo{rounds=4,spares=1});
  g.MatchState(1).equipment=new[]{"usp_s","ak_47"};g.Combat.EquipSaved(1,"ak_47",new WeaponAmmo{rounds=9,spares=2});g.DropWeapon(1);
  Check(g.TryPickUpWeapon(0,g.GroundWeaponAt(0)),"primary pickup failed");
  Check(Array.IndexOf(g.MatchState(0).equipment,"desert_eagle")>=0&&MatchEconomy.UtilityCount(g.MatchState(0))==2&&g.MatchState(0).defuseKit,"pickup removed unrelated equipment");
  Call(g,"SwitchAwperWeapon",0,"desert_eagle");Check(g.Combat.Magazine(0)==4&&g.Combat.SpareMagazines(0)==1,"pickup refilled preserved pistol");
  // Recover AWP in preference to a nearby favourite rifle and deliver next spawn.
  g=Game();actors=(List<GameObject>)typeof(Prototype).GetField("actors",F).GetValue(g);
  for(int i=0;i<10;i++)actors[i].transform.position=new Vector3(75,1,12);
  actors[0].transform.position=new Vector3(30,1,12);actors[1].transform.position=new Vector3(40,1,12);
  g.Data.players[0].weaponPosition="rifler";g.Data.players[1].weaponPosition="awper";
  g.Data.players[0].weapons=new[]{new Proficiency{weapon="ak_47",stars=5},new Proficiency{weapon="m4a1_s",stars=5}};
  g.MatchState(0).equipment=new[]{"desert_eagle","famas"};g.Combat.EquipSaved(0,"famas",new WeaponAmmo{rounds=11,spares=1});g.MatchState(1).equipment=new[]{"usp_s"};
  var drops=(List<GroundWeapon>)typeof(Prototype).GetField("groundWeapons",F).GetValue(g);
  drops.Add(new GroundWeapon{weapon="ak_47",ammo=new WeaponAmmo{rounds=8,spares=1},owner=5,position=new Vector3(30.5f,0,12)});
  drops.Add(new GroundWeapon{weapon="awp",ammo=new WeaponAmmo{rounds=2,spares=1},owner=5,position=new Vector3(31,0,12)});
  typeof(Prototype).GetProperty("Stage").SetValue(g,MatchStage.Result,null);Call(g,"BeginRecovery");Call(g,"TickRoundRecovery",.1f);
  Check(g.Combat.WeaponFor(0).id=="awp"&&g.Combat.Magazine(0)==2,"recovery did not prioritize AWP or preserved ammo");
  Check(Array.IndexOf(g.MatchState(0).equipment,"desert_eagle")>=0,"recovery removed Deagle");
  g.PrepareNextRound();g.MatchState(0).credits=1000;g.MatchState(1).credits=6000;Call(g,"StartBuying");
  for(int n=0;n<20;n++)Call(g,"DeliverSupport",.1f);
  Check(Array.IndexOf(g.MatchState(1).equipment,"awp")>=0&&Array.IndexOf(g.MatchState(0).equipment,"m4a1_s")>=0,"AWP/rifle exchange not delivered");
  Check(g.MatchState(1).credits==3100&&g.MatchState(0).credits==1000,"return purchase charged wrong player or wrong amount");
  Check(Array.IndexOf(g.MatchState(0).equipment,"desert_eagle")>=0,"return rifle removed donor sidearm");
  var debts=(int[])typeof(Prototype).GetField("repaymentDonor",F).GetValue(g);debts[1]=0;g.MatchState(1).credits=1000;
  int count=drops.Count;Call(g,"RepayRecoveredAwp",1);Check(drops.Count==count&&g.MatchState(1).credits==1000,"equal funds bought return rifle");
  Check(!(bool)Call(g,"PendingRecoveryDelivery"),"completed recovery delivery stalled buying");
  // A favourite primary is recovered even when its hidden magazine is empty.
  g=Game();actors=(List<GameObject>)typeof(Prototype).GetField("actors",F).GetValue(g);
  for(int i=0;i<10;i++)actors[i].transform.position=new Vector3(75,1,12);
  actors[0].transform.position=new Vector3(30,1,12);actors[1].transform.position=new Vector3(32,1,12);
  g.Data.players[0].weaponPosition="rifler";g.Data.players[1].weaponPosition="awper";
  g.Data.players[0].weapons=new[]{new Proficiency{weapon="ak_47",stars=5},new Proficiency{weapon="famas",stars=2}};
  g.MatchState(0).equipment=new[]{"desert_eagle","famas"};g.Combat.Equip(0,WeaponCatalog.Find("famas"));g.MatchState(1).equipment=new[]{"usp_s"};
  drops=(List<GroundWeapon>)typeof(Prototype).GetField("groundWeapons",F).GetValue(g);
  drops.Add(new GroundWeapon{weapon="ak_47",ammo=new WeaponAmmo{rounds=0,spares=0},owner=5,position=new Vector3(31,0,12)});
  typeof(Prototype).GetProperty("Stage").SetValue(g,MatchStage.Result,null);Call(g,"BeginRecovery");Call(g,"TickRoundRecovery",.1f);
  Check(g.Combat.WeaponFor(0).id=="ak_47"&&g.Combat.Magazine(0)==0,"favourite pickup inspected hidden ammo or did not replace lower proficiency");
  g.MatchState(0).equipment=new[]{"desert_eagle","awp"};g.Combat.EquipSaved(0,"awp",new WeaponAmmo{rounds=2,spares=1});
  Call(g,"SendRecoveredAwp",0,1);Check((bool)Call(g,"PendingRecoveryDelivery"),"late throw was not in flight");
  Call(g,"PreserveRecoveryDeliveries");g.PrepareNextRound();g.MatchState(0).credits=3000;g.MatchState(1).credits=3500;Call(g,"StartBuying");
  // Return purchases ignore proficiency: CT always buys M4 (2900).
  for(int n=0;n<20;n++)Call(g,"DeliverSupport",.1f);
  Check(Array.IndexOf(g.MatchState(1).equipment,"awp")>=0,"round boundary deleted in-flight recovered AWP");
  Check(g.MatchState(1).credits==600,"return purchase duplicated or skipped after carried delivery");
  debts=(int[])typeof(Prototype).GetField("repaymentDonor",F).GetValue(g);debts[1]=0;g.MatchState(0).credits=100;g.MatchState(1).credits=200;
  count=drops.Count;Call(g,"RepayRecoveredAwp",1);Check(g.MatchState(1).credits==200&&drops.Count==count,"richer but insufficient payer overspent");
  // T return purchase is always AK, even when the donor favours M4.
  typeof(Prototype).GetField("ctTeam",F).SetValue(g,1-g.TeamIndexOf(0));
  g.MatchState(0).credits=1000;g.MatchState(1).credits=6000;debts[1]=0;
  g.Data.players[0].weapons=new[]{new Proficiency{weapon="m4a1_s",stars=5}};
  Call(g,"RepayRecoveredAwp",1);
  GroundWeapon gift=null;foreach(var item in drops)if(item.receiver==0&&item.recoveryTrade)gift=item;
  Check(gift!=null&&gift.weapon=="ak_47"&&g.MatchState(1).credits==3300,"T return purchase did not use AK/2700");
  Call(g,"BeginRecovery");drops.Clear();actors[0].transform.position=new Vector3(30,1,12);
  Vector2 beforeRecovery=g.MapPosition(0);Call(g,"TickRoundRecovery",.1f);
  Check(Vector2.Distance(beforeRecovery,g.MapPosition(0))>.001f,"post-round survivor remained frozen without loot");
  Debug.Log("EQUIPMENT_EXCHANGE_ALL_OK visible recent damage, urgency, no invented pistol, slot and ammo preservation, AWP priority, next-spawn exchange, payer funds, no duplicate delivery");
 }
}
