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
  s=State(4399);MatchEconomy.Buy(s,p,false,BuyPlan.Full,false);Check(s.credits==4399&&s.armor==0,"atomic full buy");
  s=State(1900);MatchEconomy.Buy(s,p,false,BuyPlan.Force,false);Check(s.credits==0&&s.helmet&&s.armor==100&&WeaponCatalog.Equipped(s.equipment).id=="desert_eagle"&&MatchEconomy.UtilityCount(s)==1,"T force");
  s=State(1550);MatchEconomy.Buy(s,p,true,BuyPlan.Force,false);Check(s.credits==0&&!s.helmet&&s.armor==100&&MatchEconomy.UtilityCount(s)==1,"CT force no helmet");
  s=State(1000);MatchEconomy.Buy(s,p,false,BuyPlan.Eco,false);Check(s.credits==1000&&s.armor==0&&MatchEconomy.UtilityCount(s)==0,"eco spent");
  MatchEconomy.Buy(s,p,false,BuyPlan.Eco,true);Check(s.credits==300&&WeaponCatalog.Equipped(s.equipment).id=="desert_eagle"&&s.armor==0,"eco deagle only");
  s=State(2000);Check(MatchEconomy.BuyUtility(s,"smoke")&&MatchEconomy.BuyUtility(s,"smoke")&&!MatchEconomy.BuyUtility(s,"smoke")&&MatchEconomy.BuyUtility(s,"flash"),"duplicate max 2");
  var items=new System.Collections.Generic.List<string>(s.equipment);items.Remove("smoke");s.equipment=items.ToArray();Check(MatchEconomy.UtilityCount(s,"smoke")==1&&MatchEconomy.BuyUtility(s,"flash"),"single consume/replenish");
  var states=new PlayerMatchState[10];var players=new PlayerData[10];var teams=new int[10];
  for(int i=0;i<10;i++){states[i]=State(5000);players[i]=p;teams[i]=i/5;}
  Check(MatchEconomy.Choose(states,players,teams,0,false,false,false)==BuyPlan.Full,"team full");
  for(int i=0;i<5;i++)states[i].credits=2400;
  Check(MatchEconomy.Choose(states,players,teams,0,false,false,false)==BuyPlan.Eco,"save toward next full");
  Check(MatchEconomy.Choose(states,players,teams,0,false,false,true)==BuyPlan.Force,"match point force");
  for(int i=0;i<5;i++)states[i].credits=1900;
  Check(MatchEconomy.Choose(states,players,teams,0,false,false,false)==BuyPlan.Force,"affordable force before long save");
  for(int round=2;round<=3;round++){var picks=MatchEconomy.EcoBuyers(states,players,teams,0,round);int n=0;for(int i=0;i<10;i++)if(picks[i]){Check(teams[i]==0,"opponent eco selection");n++;}Check(n==2+round%2,"eco 2 or 3");}
  for(int i=0;i<5;i++)states[i].credits=699;var none=MatchEconomy.EcoBuyers(states,players,teams,0,1);Check(Array.IndexOf(none,true)<0,"unaffordable deagles");
  Check(MatchEconomy.Choose(states,players,teams,0,false,true,false)==BuyPlan.Pistol,"opening exception");
  var sniper=new PlayerData{weaponPosition="awper"};s=State(6200);MatchEconomy.Buy(s,sniper,true,BuyPlan.Full,false);Check(WeaponCatalog.Equipped(s.equipment).id=="awp"&&s.credits==0,"awper full budget");
  Debug.Log("ECONOMY_ALL_OK team plans, full/force/eco, side helmet rules, favorite pistol, 2-3 deagles, inventory reuse, utility limits, budget boundaries");
 }
}
