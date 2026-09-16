using System;
using System.Reflection;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class HitZoneChecks
{
 public static void Run()
 {
  if(CombatSystem.ResolveRegion(0,.8f,.6f)!=HitRegion.Head||CombatSystem.ResolveRegion(.4f,0,.6f)!=HitRegion.Body||CombatSystem.ResolveRegion(.4f,.8f,.6f)!=HitRegion.Miss||CombatSystem.ResolveRegion(0,1.2f,.6f)!=HitRegion.Miss) throw new Exception("Hit region geometry failed");
  var fire=typeof(CombatSystem).GetMethod("Fire",BindingFlags.NonPublic|BindingFlags.Instance);
  foreach(string id in WeaponCatalog.Ids)
  {
   var profile=WeaponCatalog.Find(id); profile.baseSpreadDegrees=0; profile.recoilPerShot=0; // Isolate damage from recoil.
   var combat=new CombatSystem(new CombatSettings{health=1000000},new WeaponProfile(),2);
   combat.Equip(0,profile); combat.Reset(25);
   if(combat.Magazine(0)!=profile.magazineSize) throw new Exception("Wrong magazine: "+id);
   for(int shot=0;shot<40;shot++)
   {
    float before=combat.Health(1); fire.Invoke(combat,new object[]{0,1,0f,10f,100});
    var region=combat.LastHit(0);
    if(region==HitRegion.Miss) throw new Exception("Perfect aim missed: "+id);
    float expected=region==HitRegion.Head?profile.HeadDamage:profile.damage;
    if(Mathf.Abs((before-combat.Health(1))-expected)>.1f) throw new Exception("Damage mismatch: "+id);
   }
   if(combat.Headshots==0||combat.Bodyshots==0||combat.Hits!=combat.Headshots+combat.Bodyshots) throw new Exception("Missing head/body outcomes: "+id);
   combat.Reset(25); if(combat.Headshots!=0||combat.Bodyshots!=0||combat.LastHit(0)!=HitRegion.Miss) throw new Exception("Hit state survived reset");
  }
  var awp=WeaponCatalog.Find("awp"); awp.baseSpreadDegrees=0;
  var lethal=new CombatSystem(new CombatSettings(),new WeaponProfile(),2); lethal.Equip(0,awp);
  fire.Invoke(lethal,new object[]{0,1,0f,10f,0});
  if(lethal.Alive(1)||lethal.Health(1)!=0||lethal.KilledBy(1)!=0||lethal.Kills!=1) throw new Exception("Lethal shot did not resolve");
  if(WeaponCatalog.Equipped(new[]{"flash","desert_eagle","awp"}).id!="awp"||WeaponCatalog.Equipped(new[]{"smoke"}).id!="unarmed") throw new Exception("Equipment selection failed");
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var game=new GameObject("weapon integration").AddComponent<Prototype>(); game.Initialize();
  game.MatchState(0).equipment=new[]{"awp","flash"}; game.BeginRound();
  if(game.Combat.WeaponFor(0).id!="awp"||game.Combat.Magazine(0)!=5) throw new Exception("Round did not use equipped gun");
  game.PrepareNextRound(); game.BeginRound();
  if(game.Combat.WeaponFor(0).id!="awp") throw new Exception("Weapon lost after next round");
  Debug.Log("HITZONE_ALL_OK geometry, 18 weapon head/body damage, ammo, lethal hit, reset, inventory integration");
 }
}