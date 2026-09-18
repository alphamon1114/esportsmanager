using System;
using UnityEngine;
namespace FpsManager
{
 public partial class Prototype
 {
  readonly float[,] finishingDamage=new float[10,10],finishingAge=new float[10,10];
  readonly string[] finishingPrimary=new string[10];
  void ResetFinishingCombat(){Array.Clear(finishingDamage,0,finishingDamage.Length);Array.Clear(finishingAge,0,finishingAge.Length);Array.Clear(finishingPrimary,0,10);}
  void RecordCombatDamage(int shooter,int target,float damage)
  {
   Statistics.Damage(shooter,target,damage);if(AutomaticMatch&&teamIndex[shooter]!=teamIndex[target])RegisterHurt(shooter,target);
   if(teamIndex[shooter]!=teamIndex[target]&&vision.Sees(shooter,target))
   {finishingDamage[shooter,target]+=damage;finishingAge[shooter,target]=0;}
  }
  string ReadyPistol(int i)
  {
   string best=null;int strength=0;
   foreach(var id in matchState[i].equipment)
   {
    int slot=Array.IndexOf(WeaponCatalog.Ids,id);if(slot<0||slot>=6)continue;
    WeaponAmmo ammo;if(holsteredAmmo[i]==null||!holsteredAmmo[i].TryGetValue(id,out ammo))ammo=WeaponDropRules.Fresh(id);
    if(ammo.rounds<=0||ammo.reloadRemaining>0)continue;
    if(WeaponDropRules.Strength(id)>strength){best=id;strength=WeaponDropRules.Strength(id);}
   }
   return best;
  }
  bool TimeForFinisher(int i)
  {
   if(director.PlantedSite>=0)return teamIndex[i]==ctTeam&&director.BombTimer<=director.DefuseDuration+6;
   return teamIndex[i]!=ctTeam&&director.Settings.roundSeconds-director.Clock<=10;
  }
  void UpdateFinishingCombat(int i,float dt)
  {
   if(!AutomaticMatch)return;
   bool contact=false,wounded=false;
   for(int enemy=0;enemy<10;enemy++)
   {
    finishingAge[i,enemy]+=dt;if(finishingAge[i,enemy]>5)finishingDamage[i,enemy]=0;
    if(teamIndex[i]==teamIndex[enemy]||!combat.Alive(enemy)||!vision.Sees(i,enemy))continue;
    var known=vision.Knowledge(teamIndex[i],enemy);
    if(!known.known||Vector2.Distance(MapPosition(i),known.lastKnownPosition)>25)continue;
    contact=true;if(finishingDamage[i,enemy]>=50)wounded=true;
   }
   string held=combat.WeaponFor(i).id;
   if(finishingPrimary[i]!=null)
   {
    string primary=finishingPrimary[i];
    if(Array.IndexOf(matchState[i].equipment,primary)<0||Array.IndexOf(WeaponCatalog.Ids,held)>=6){finishingPrimary[i]=null;return;}
    if(!contact||combat.Magazine(i)==0){SwitchAwperWeapon(i,primary);finishingPrimary[i]=null;}
    return;
   }
   if(Array.IndexOf(WeaponCatalog.Ids,held)<6||combat.Magazine(i)>0||!contact||(!wounded&&!TimeForFinisher(i)))return;
   string pistol=ReadyPistol(i);if(pistol==null)return;
   finishingPrimary[i]=held;SwitchAwperWeapon(i,pistol);awpSidearm[i]=false;
   if(awperMovement[i]!=null)awperMovement[i].Cancel();peeking[i].Cancel();
  }
 }
}

