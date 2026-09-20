using System;using System.Collections.Generic;using FpsManager;using UnityEngine;
public static class StatDistributionChecks {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static StatBlock Stats(int aim=80){return new StatBlock{aim=aim,movement=80,composure=80,utility=80};}
 // Exact old shot-selection rule, retained only as a measurement baseline.
 static void Old(CombatSystem c,int[] teams,DeterministicRandom rng){
  var a=new List<int>();var b=new List<int>();
  for(int n=0;n<512;n++){
   a.Clear();b.Clear();for(int i=0;i<10;i++)if(c.Alive(i)){if(teams[i]==0)a.Add(i);else b.Add(i);}
   if(a.Count==0||b.Count==0)return;rng.Next01();
   int x=a[(int)(rng.NextUInt()%(uint)a.Count)],y=b[(int)(rng.NextUInt()%(uint)b.Count)];
   float sx=Power(c,x),sy=Power(c,y);int s=rng.Next01()<sx/(sx+sy)?x:y,t=s==x?y:x;
   c.StatShot(s,t,rng.Next01()<(c.WeaponFor(s).id=="awp"?.03f:.124f)?HitRegion.Head:HitRegion.Body);
  }
 }
 static float Power(CombatSystem c,int i){return 6400*(1+Math.Min(2,MatchEconomy.Price(c.WeaponFor(i).id)/2500f))*(.5f+c.Health(i)/200)*(i<5?1.04f:1);}
 static string Trial(int seed,bool old,int awpSlot,int[] total,int aceAim=80){
  var teams=new[]{0,0,0,0,0,1,1,1,1,1};var c=new CombatSystem(new CombatSettings(),new WeaponProfile(),10){AmmoEnabled=true};
  for(int i=0;i<10;i++){c.Equip(i,WeaponCatalog.Find(i%5==awpSlot?"awp":"ak_47"));c.BindProtection(i,new PlayerMatchState{armor=100,helmet=true});}
  c.Killed=e=>total[e.killer]++;var rng=new DeterministicRandom(seed);
  if(old)Old(c,teams,rng);else{
   var exchange=new StatExchange(c,teams,i=>Stats(i==0?aceAim:80),rng);
   for(int n=0;n<512;n++){int s,t;float dt;if(!exchange.Next(out s,out t,out dt))break;exchange.Fire(s,t);}
  }
  string state=c.Shots+"/"+c.Kills;for(int i=0;i<10;i++)state+="/"+c.Health(i)+":"+c.Magazine(i);return state;
 }
 public static void Run(){
  int[] before=new int[10],after=new int[10],rifles=new int[10],strong=new int[10],weak=new int[10];
  const int samples=4000;int aces=0;
  for(int seed=0;seed<samples;seed++){Trial(seed,true,0,before);var round=new int[10];Trial(seed,false,0,round);for(int i=0;i<10;i++){after[i]+=round[i];if(round[i]==5)aces++;}Trial(seed,false,-1,rifles);Trial(seed,false,-1,strong,98);Trial(seed,false,-1,weak,35);}
  int oldTotal=0,newTotal=0,rifleTotal=0;for(int i=0;i<10;i++){oldTotal+=before[i];newTotal+=after[i];rifleTotal+=rifles[i];}
  float oldShare=(before[0]+before[5])/(float)oldTotal,newShare=(after[0]+after[5])/(float)newTotal;
  Debug.Log("STAT_DISTRIBUTION mixed AWP kill share before="+oldShare.ToString("P1")+" after="+newShare.ToString("P1")+" rounds="+samples);
  Check(newShare<oldShare-.1f&&newShare<.4f,"AWP still harvests a disproportionate share of team kills");
  for(int i=0;i<10;i++)Check(rifles[i]/(float)rifleTotal>.085f&&rifles[i]/(float)rifleTotal<.115f,"equal players biased by roster index");
  Check(strong[0]>weak[0]*1.15f,"skill no longer matters");
  Check(aces>0,"natural five-kill rounds were capped");
  Check(Trial(901,false,0,new int[10])==Trial(901,false,0,new int[10]),"skip replay changed");
  // Cadence, persistent targets, misses and finite ammunition are mechanical,
  // not statistical kill caps. No player can shoot after exhausting reserves.
  var c=new CombatSystem(new CombatSettings{health=10000},new WeaponProfile(),4){AmmoEnabled=true};
  for(int i=0;i<4;i++)c.Equip(i,new WeaponProfile{id=i==0?"awp":"ak_47",damage=1,fireInterval=i==0?1.45f:.1f,magazineSize=2,reserveMagazines=1,reloadSeconds=2});
  var e2=new StatExchange(c,new[]{0,0,1,1},i=>Stats(),new DeterministicRandom(42));var last=new float[]{-100,-100,-100,-100};var target=new[]{-1,-1,-1,-1};var shots=new int[4];float clock=0;
  for(int n=0;n<100;n++){int s,t;float dt;if(!e2.Next(out s,out t,out dt))break;clock+=dt;
   Check(clock-last[s]+.0001f>=c.WeaponFor(s).fireInterval,"weapon cooldown ignored");
   if(shots[s]==2)Check(clock-last[s]>=2,"reload was instantaneous");
   Check(target[s]<0||target[s]==t,"target changed before death");target[s]=t;last[s]=clock;shots[s]++;e2.Fire(s,t);
  }
  for(int i=0;i<4;i++)Check(shots[i]==4&&c.Magazine(i)==0&&c.SpareMagazines(i)==0,"ammunition fabricated or participant starved");
  Check(c.Hits<c.Shots&&c.Kills==0,"misses absent or fake kills generated");
  // A reload already started in live play has consumed its spare magazine.
  // Carry both its remaining delay and that single reservation into skip.
  c.Reset(1);c.StatShot(0,2,HitRegion.Miss);c.RequestReload(0);
  Check(c.Reloading(0)&&c.SpareMagazines(0)==0,"partial reload fixture invalid");
  e2=new StatExchange(c,new[]{0,0,1,1},i=>Stats(),new DeterministicRandom(8));clock=0;bool resumed=false;
  for(int n=0;n<30;n++){int s,t;float dt;if(!e2.Next(out s,out t,out dt))break;clock+=dt;e2.Fire(s,t);if(s==0){Check(clock>=2&&c.Magazine(0)==1&&c.SpareMagazines(0)==0,"in-progress reload lost delay or consumed reserve twice");resumed=true;break;}}
  Check(resumed,"reloading player never resumed");
  Debug.Log("STAT_DISTRIBUTION_ALL_OK deterministic, equal-slot fairness, skill effect, cadence, reload, finite ammo, persistent targets, uncapped aces="+aces);
 }
}
