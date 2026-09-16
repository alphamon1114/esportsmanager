using System;
namespace FpsManager
{
 public static class WeaponCatalog
 {
  // Prototype unarmoured values, not a verified CS2 damage table.
  public static readonly string[] Ids={"glock_18","usp_s","dual_berettas","tec_9","five_seven","desert_eagle","mac_10","mp9","mp7","pp_bizon","ump_45","galil_ar","famas","ak_47","m4a1_s","aug","sg_553","awp"};
  static readonly float[] Body={30,35,38,33,32,53,29,26,29,27,35,30,30,36,33,28,30,115};
  static readonly float[] Interval={.15f,.17f,.12f,.12f,.15f,.23f,.075f,.07f,.08f,.08f,.09f,.09f,.09f,.1f,.1f,.09f,.09f,1.45f};
  static readonly int[] Magazine={20,12,30,18,20,7,30,30,30,64,25,35,25,30,20,30,30,5};
  public static WeaponProfile Find(string id)
  {
   int i=Array.IndexOf(Ids,id); if(i<0) return null;
   return new WeaponProfile{id=id,recoilPerShot=id=="awp"?1f:id=="desert_eagle"?.8f:i<6?.3f:i<11?.12f:.18f,damage=Body[i],headMultiplier=4,fireInterval=Interval[i],magazineSize=Magazine[i],reloadSeconds=id=="awp"?3.5f:2.2f,baseSpreadDegrees=id=="awp"?.65f:3};
  }
  public static WeaponProfile Equipped(string[] inventory)
  {
   WeaponProfile selected=null; int best=-1;
   if(inventory!=null) foreach(string id in inventory)
   {
    int index=Array.IndexOf(Ids,id); if(index<0) continue;
    int priority=index>=6?2:1;
    if(priority<=best) continue; selected=Find(id); best=priority;
   }
   return selected??new WeaponProfile{id="unarmed",damage=0,magazineSize=0};
  }
 }
}