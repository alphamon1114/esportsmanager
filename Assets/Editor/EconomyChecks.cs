using System;
using FpsManager;
using UnityEngine;
public static class EconomyChecks
{
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 static PlayerMatchState State(int money){return new PlayerMatchState{credits=money,equipment=new[]{"glock_18"}};}
 public static void Run()
 {
  var p=new PlayerData{weaponPosition="rifler",weapons=new[]{new Proficiency{weapon="desert_eagle",stars=5},new Proficiency{weapon="tec_9",stars=3}}};
  var s=State(4400);Check(MatchEconomy.FullCost(s,p,false)==4400,"full quote");MatchEconomy.Buy(s,p,false,BuyPlan.Full,false);
  Check(s.credits==0&&s.armor==100&&s.helmet&&WeaponCatalog.Equipped(s.equipment).id=="ak_47"&&MatchEconomy.UtilityCount(s)==3&&MatchEconomy.UtilityCount(s,"flash")==2,"full kit");
  s.credits=1000;MatchEconomy.Buy(s,p,false,BuyPlan.Full,false);Check(s.credits==1000,"duplicate full purchase");
  Check(!MatchEconomy.BuyUtility(s,"flash")&&!MatchEconomy.BuyUtility(s,"smoke"),"utility capacity");
  s=State(4399);MatchEconomy.Buy(s,p,false,BuyPlan.Full,false);Check(s.credits==199&&s.armor==100&&s.helmet&&WeaponCatalog.Equipped(s.equipment).id=="ak_47"&&MatchEconomy.UtilityCount(s)==2,"partial utility full buy");
  s=State(1900);MatchEconomy.Buy(s,p,false,BuyPlan.Force,false);Check(s.credits==0&&s.helmet&&s.armor==100&&WeaponCatalog.Equipped(s.equipment).id=="desert_eagle"&&MatchEconomy.UtilityCount(s)==1,"T force");
  s=State(1550);MatchEconomy.Buy(s,p,true,BuyPlan.Force,false);Check(s.credits==0&&!s.helmet&&s.armor==100&&MatchEconomy.UtilityCount(s)==1,"CT force no helmet");
  s=State(1000);MatchEconomy.Buy(s,p,false,BuyPlan.Eco,false);Check(s.credits==1000&&s.armor==0&&MatchEconomy.UtilityCount(s)==0,"eco spent");
  MatchEconomy.Buy(s,p,false,BuyPlan.Eco,true);Check(s.credits==1000,"eco purchase protects next round");
  s=State(2400);MatchEconomy.Buy(s,p,false,BuyPlan.Eco,true);Check(s.credits==1700&&WeaponCatalog.Equipped(s.equipment).id=="desert_eagle"&&s.armor==0,"eco deagle only");
  s=State(2000);Check(MatchEconomy.BuyUtility(s,"smoke")&&MatchEconomy.BuyUtility(s,"smoke")&&!MatchEconomy.BuyUtility(s,"smoke")&&MatchEconomy.BuyUtility(s,"flash"),"duplicate max 2");
  var items=new System.Collections.Generic.List<string>(s.equipment);items.Remove("smoke");s.equipment=items.ToArray();Check(MatchEconomy.UtilityCount(s,"smoke")==1&&MatchEconomy.BuyUtility(s,"flash"),"single consume/replenish");
  var states=new PlayerMatchState[10];var players=new PlayerData[10];var teams=new int[10];
  for(int i=0;i<10;i++){states[i]=State(5000);players[i]=p;teams[i]=i/5;}
  Check(MatchEconomy.Choose(states,players,teams,0,false,false,false)==BuyPlan.Full,"team full");
  for(int i=0;i<5;i++)states[i].credits=2400;
  Check(MatchEconomy.Choose(states,players,teams,0,false,false,false)==BuyPlan.Eco,"save toward next full");
  Check(MatchEconomy.Choose(states,players,teams,0,false,false,true)==BuyPlan.Force,"match point force");
  for(int i=0;i<5;i++)states[i].credits=1900;
  Check(MatchEconomy.Choose(states,players,teams,0,false,false,false)==BuyPlan.Eco,"save toward 4001 next round");
  for(int i=0;i<5;i++)states[i].credits=2400;
  for(int round=2;round<=3;round++){var picks=MatchEconomy.EcoBuyers(states,players,teams,0,round);int n=0;for(int i=0;i<10;i++)if(picks[i]){Check(teams[i]==0,"opponent eco selection");n++;}Check(n==2+round%2,"eco 2 or 3");}
  for(int i=0;i<5;i++)states[i].credits=699;var none=MatchEconomy.EcoBuyers(states,players,teams,0,1);Check(Array.IndexOf(none,true)<0,"unaffordable deagles");
  Check(MatchEconomy.Choose(states,players,teams,0,false,true,false)==BuyPlan.Pistol,"opening exception");
  var sniper=new PlayerData{weaponPosition="awper"};s=State(6200);MatchEconomy.Buy(s,sniper,true,BuyPlan.Full,false);Check(WeaponCatalog.Equipped(s.equipment).id=="awp"&&s.credits==0,"awper full budget");
  foreach(bool ct in new[]{false,true})foreach(var plan in new[]{BuyPlan.Force})
  {
   s=State(4001);MatchEconomy.Buy(s,p,ct,plan,false);
   Check(s.armor==100&&s.helmet&&WeaponCatalog.Equipped(s.equipment).id==(ct?"m4a1_s":"ak_47")&&s.credits>=0,"rich player overrides team plan");
   if(ct)Check(s.credits==101&&MatchEconomy.UtilityCount(s)==0,"CT rifle armor without utilities");
  }
  s=State(4000);Check(!MatchEconomy.CanFullBuy(s,p,true),"strict greater than 4000 boundary");
  s=State(4001);MatchEconomy.Buy(s,p,true);Check(WeaponCatalog.Equipped(s.equipment).id=="m4a1_s","compatibility buy threshold");
  s=State(4001);MatchEconomy.Buy(s,sniper,true,BuyPlan.Force,false);Check(WeaponCatalog.Equipped(s.equipment).id=="m4a1_s"&&s.helmet&&s.armor==100,"AWP budget fallback rifle");
  s=State(5500);MatchEconomy.Buy(s,sniper,true,BuyPlan.Full,false);Check(WeaponCatalog.Equipped(s.equipment).id=="awp"&&s.credits==0&&MatchEconomy.UtilityCount(s)==0,"AWP without utilities");
  for(int i=0;i<5;i++){states[i]=State(4001);players[i]=i==0?sniper:p;}
  Check(MatchEconomy.Choose(states,players,teams,0,true,false,true)==BuyPlan.Full,"4001 full team including awper on enemy match point");
  s=State(9000);MatchEconomy.Buy(s,p,true,BuyPlan.Eco,false);Check(s.credits==9000&&s.armor==0&&WeaponCatalog.Equipped(s.equipment).id=="glock_18","rich player respects team eco");
  for(int i=0;i<5;i++)states[i]=State(1900);
  states[0].credits=9000;
  Check(MatchEconomy.Choose(states,players,teams,0,true,false,false)==BuyPlan.Eco,"mixed wealth saves together");
  var savers=MatchEconomy.EcoBuyers(states,players,teams,0,2,true);
  Check(savers[0],"rich eco player can afford deagle");
  for(int i=1;i<5;i++)Check(!savers[i],"deagle cannot delay next full buy");
  MatchEconomy.Buy(states[0],players[0],true,BuyPlan.Eco,savers[0]);Check(states[0].credits==8300&&states[0].armor==0&&WeaponCatalog.Equipped(states[0].equipment).id=="desert_eagle","rich eco buys only planned deagle");
  for(int i=0;i<5;i++)states[i].credits+=2400;
  Check(MatchEconomy.Choose(states,players,teams,0,true,false,false)==BuyPlan.Full,"full team ready after eco loss");
  Debug.Log("ECONOMY_ALL_OK team plans, full/force/eco, side helmet rules, favorite pistol, 2-3 deagles, inventory reuse, utility limits, budget boundaries");
 }
}
