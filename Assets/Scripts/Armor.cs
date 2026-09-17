using System;
namespace FpsManager
{
 public static class ArmorRules
 {
  public const int VestCost=650, SuitCost=1000, HelmetCost=350;
  public static int SuitPrice(PlayerMatchState state) { return state.helmet?(state.armor<100?VestCost:0):(state.armor>=100?HelmetCost:SuitCost); }
  public static bool Buy(PlayerMatchState state,bool helmet)
  {
   int cost=helmet?SuitPrice(state):(state.armor<100?VestCost:0);
   if(cost==0||state.credits<cost)return false;
   state.credits-=cost;state.armor=100;if(helmet)state.helmet=true;return true;
  }
  public static float Damage(PlayerMatchState state,WeaponProfile gun,HitRegion region)
  {
   if(region==HitRegion.Miss)return 0;
   float raw=region==HitRegion.Head?gun.HeadDamage:gun.damage;
   if(state.armor<=0||(region==HitRegion.Head&&!state.helmet))return raw;
   float health=raw*Math.Max(0,Math.Min(1,gun.armorRatio*.5f));
   float spent=(raw-health)*.5f;
   if(spent>state.armor){health=raw-state.armor*2;spent=state.armor;}
   state.armor=Math.Max(0,state.armor-spent);
   return health;
  }
 }
 public sealed partial class CombatSystem
 {
  readonly PlayerMatchState[] protection=new PlayerMatchState[10];
  public void BindProtection(int i,PlayerMatchState state){protection[i]=state;}
  public float Armor(int i){return protection[i]==null?0:protection[i].armor;}
  public bool Helmet(int i){return protection[i]!=null&&protection[i].helmet;}
  float ProtectedDamage(int i,WeaponProfile gun,HitRegion region)
  {
   return protection[i]==null?(region==HitRegion.Head?gun.HeadDamage:gun.damage):ArmorRules.Damage(protection[i],gun,region);
  }
 }
}
