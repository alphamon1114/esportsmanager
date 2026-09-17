using System;
using System.Collections.Generic;
namespace FpsManager
{
 public enum BuyPlan { Pistol, Full, Force, Eco }
 public static class MatchEconomy
 {
  public static int UtilityCount(PlayerMatchState s,string id=null){int n=0;foreach(var item in s.equipment??new string[0])if(id==null?(item=="smoke"||item=="flash"):item==id)n++;return n;}
  static bool Own(PlayerMatchState s,string id){return Array.IndexOf(s.equipment??new string[0],id)>=0;}
  static bool Pistol(string id){int n=Array.IndexOf(WeaponCatalog.Ids,id);return n>=0&&n<6;}
  public static int Price(string id){switch(id){case "desert_eagle":return 700;case "dual_berettas":return 300;case "tec_9":case "five_seven":return 500;case "awp":return 4500;case "ak_47":return 2700;case "m4a1_s":return 2900;case "smoke":return 300;case "flash":return 200;default:return 0;}}
  public static int Skill(PlayerData p,string id){if(p.weapons!=null)foreach(var w in p.weapons)if(w.weapon==id)return w.stars;return 0;}
  public static string FavoritePistol(PlayerData p,bool ct)
  {
   string best=ct?"five_seven":"tec_9";
   foreach(var id in new[]{"dual_berettas","desert_eagle"})if(Skill(p,id)>Skill(p,best))best=id;
   return best;
  }
  static bool HasRifle(PlayerMatchState s){int i=Array.IndexOf(WeaponCatalog.Ids,WeaponCatalog.Equipped(s.equipment).id);return i>=11;}
  static string Rifle(PlayerData p,bool ct){return p.weaponPosition=="awper"?"awp":ct?"m4a1_s":"ak_47";}
  static int UtilityBudget(PlayerMatchState s)
  {
   int count=UtilityCount(s),smoke=UtilityCount(s,"smoke"),flash=UtilityCount(s,"flash"),cost=0;
   while(count<3){if(smoke==0){smoke++;cost+=300;}else if(flash<2){flash++;cost+=200;}else {smoke++;cost+=300;}count++;}return cost;
  }
  public static int CoreCost(PlayerMatchState s,PlayerData p,bool ct){return (HasRifle(s)?0:Price(Rifle(p,ct)))+ArmorRules.SuitPrice(s);}
  public static int FullCost(PlayerMatchState s,PlayerData p,bool ct){return CoreCost(s,p,ct)+UtilityBudget(s);}
  public static int FullBuyFunds(PlayerMatchState s,PlayerData p,bool ct){return Math.Min(4001,FullCost(s,p,ct));}
  public static bool CanFullBuy(PlayerMatchState s,PlayerData p,bool ct){return s.credits>=FullBuyFunds(s,p,ct);}
  public static int ForceCost(PlayerMatchState s,PlayerData p,bool ct)
  {
   string gun=FavoritePistol(p,ct);return (HasRifle(s)||Own(s,gun)?0:Price(gun))+(ct?(s.armor<100?650:0):ArmorRules.SuitPrice(s))+(UtilityCount(s)>0?0:200);
  }
  public static BuyPlan Choose(PlayerMatchState[] states,PlayerData[] players,int[] teams,int team,bool ct,bool opening,bool enemyMapPoint)
  {
   if(opening)return BuyPlan.Pistol;
   int count=0,full=0,next=0,force=0;
   for(int i=0;i<states.Length;i++)if(teams[i]==team){count++;int cost=FullBuyFunds(states[i],players[i],ct);if(CanFullBuy(states[i],players[i],ct))full++;if(states[i].credits+2400>=cost)next++;if(states[i].credits>=ForceCost(states[i],players[i],ct))force++;}
   if(count>0&&full==count)return BuyPlan.Full;
   if(enemyMapPoint||(force>=3&&next<3))return BuyPlan.Force;
   return BuyPlan.Eco;
  }
  public static bool[] EcoBuyers(PlayerMatchState[] states,PlayerData[] players,int[] teams,int team,int round,bool ct=false)
  {
   var candidates=new List<int>();var chosen=new bool[states.Length];
   for(int i=0;i<states.Length;i++)if(teams[i]==team&&!HasRifle(states[i])&&(Own(states[i],"desert_eagle")||(states[i].credits>=700&&states[i].credits-700+2400>=FullBuyFunds(states[i],players[i],ct))))candidates.Add(i);
   candidates.Sort((a,b)=>{int skill=Skill(players[b],"desert_eagle").CompareTo(Skill(players[a],"desert_eagle"));return skill!=0?skill:a.CompareTo(b);});
   int target=2+(round%2);for(int n=0;n<Math.Min(target,candidates.Count);n++)chosen[candidates[n]]=true;return chosen;
  }
  public static bool BuyUtility(PlayerMatchState s,string id)
  {
   if(id!="flash"&&id!="smoke")return false;
   if(UtilityCount(s)>=3||UtilityCount(s,id)>=2||s.credits<Price(id))return false;
   var items=new List<string>(s.equipment??new string[0]);items.Add(id);s.equipment=items.ToArray();s.credits-=Price(id);return true;
  }
  static void BuyGun(PlayerMatchState s,string id)
  {
   if(Own(s,id)||s.credits<Price(id))return;
   var items=new List<string>(s.equipment??new string[0]);bool pistol=Pistol(id);
   items.RemoveAll(w=>WeaponCatalog.Find(w)!=null&&Pistol(w)==pistol);items.Add(id);s.equipment=items.ToArray();s.credits-=Price(id);
  }
  // Compatibility entry point for isolated economy callers; automatic matches plan per team.
  public static void Buy(PlayerMatchState s,PlayerData p,bool ct){Buy(s,p,ct,CanFullBuy(s,p,ct)?BuyPlan.Full:BuyPlan.Force,false);}
  public static void Buy(PlayerMatchState s,PlayerData p,bool ct,BuyPlan plan,bool ecoDeagle,bool buyKit=false)
  {
   if(plan==BuyPlan.Force&&s.credits>4000)plan=BuyPlan.Full;
   if(plan==BuyPlan.Eco){if(ecoDeagle&&!HasRifle(s)&&s.credits-700+2400>=FullBuyFunds(s,p,ct))BuyGun(s,"desert_eagle");return;}
   if(plan==BuyPlan.Pistol){ArmorRules.Buy(s,false);return;}
   if(plan==BuyPlan.Full)
   {
    // Above 4000, secure rifle + armor first; utilities use the remaining budget.
    if(!CanFullBuy(s,p,ct))return;
    string rifle=Rifle(p,ct);
    if(!HasRifle(s)&&s.credits<Price(rifle)+ArmorRules.SuitPrice(s))rifle=ct?"m4a1_s":"ak_47";
    ArmorRules.Buy(s,true);if(!HasRifle(s))BuyGun(s,rifle);if(ct&&buyKit)DefuseKitRules.Buy(s);
    while(UtilityCount(s)<3)if(!BuyUtility(s,UtilityCount(s,"smoke")==0?"smoke":UtilityCount(s,"flash")<2?"flash":"smoke"))break;
    return;
   }
   // Force buy is best effort on a match point; CT never purchases a new helmet here.
   ArmorRules.Buy(s,!ct);
   if(!HasRifle(s))BuyGun(s,FavoritePistol(p,ct));
   if(UtilityCount(s)==0)BuyUtility(s,"flash");
  }
 }
}
