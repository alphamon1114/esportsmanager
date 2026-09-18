using System;
using System.Reflection;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class ArmorChecks
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Near(float a,float b,string message){Check(Math.Abs(a-b)<.01f,message);}
 public static void Run()
 {
  var ak=WeaponCatalog.Find("ak_47");var m4=WeaponCatalog.Find("m4a1_s");
  var state=new PlayerMatchState{armor=100};
  Near(ArmorRules.Damage(state,ak,HitRegion.Body),27.9f,"AK body");Near(state.armor,95.95f,"armor wear");
  Near(ArmorRules.Damage(state,ak,HitRegion.Head),144,"unprotected head");
  state.helmet=true;Check(ArmorRules.Damage(state,ak,HitRegion.Head)>100,"AK helmet one tap");
  Check(ArmorRules.Damage(state,m4,HitRegion.Head)<100,"M4 helmet survival");
  state.armor=1;Near(ArmorRules.Damage(state,ak,HitRegion.Body),34,"exhaustion overflow");Near(state.armor,0,"depleted armor");
  Near(ArmorRules.Damage(state,ak,HitRegion.Head),144,"empty armor helmet");
  state.armor=100;Near(ArmorRules.Damage(state,WeaponCatalog.Find("sg_553"),HitRegion.Body),30,"full penetration");Near(state.armor,100,"no absorbed damage");
  state=new PlayerMatchState{credits=649};Check(!ArmorRules.Buy(state,false)&&state.credits==649,"insufficient money");
  state.credits=1000;Check(ArmorRules.Buy(state,false)&&state.credits==350&&state.armor==100&&!state.helmet,"vest purchase");
  Check(ArmorRules.Buy(state,true)&&state.credits==0&&state.helmet,"helmet upgrade");
  state.armor=80;state.credits=650;Check(ArmorRules.Buy(state,true)&&state.armor==100&&state.credits==0,"helmet retained on repair");
  state.helmet=false;state.armor=99;state.credits=999;Check(!ArmorRules.Buy(state,true),"damaged vest upgrade costs 1000");
  var combat=new CombatSystem(new CombatSettings(),ak,2);state.armor=100;state.helmet=true;combat.BindProtection(1,state);
  typeof(CombatSystem).GetMethod("ApplyHit",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(combat,new object[]{0,1,HitRegion.Body});
  Near(combat.Health(1),72.1f,"actual combat armor path");Near(state.armor,95.95f,"persistent wear");combat.Reset(1);Near(state.armor,95.95f,"reset free armor refill");
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var game=new GameObject("Armor integration").AddComponent<Prototype>();game.Initialize();game.StartMatch();game.AdvanceFrame(Prototype.BuySeconds+.01f);
  Check(game.MatchState(0).armor==100&&!game.MatchState(0).helmet&&game.MatchState(0).credits==150,"pistol round buys vest");
  ArmorRules.Damage(game.MatchState(0),ak,HitRegion.Body);game.PrepareNextRound();Near(game.MatchState(0).armor,95.95f,"round retains worn armor");
  foreach(string id in WeaponCatalog.Ids)Check(WeaponCatalog.Find(id).armorRatio>0&&WeaponCatalog.Find(id).armorRatio<=2,"weapon ratio "+id);
  Debug.Log("ARMOR_ALL_OK damage, helmet, penetration, exhaustion, economy, combat, persistence, opening buy");
 }
}
