using System;
using System.Collections.Generic;
using FpsManager;
using UnityEngine;
public static class RecoilChecks
{
 public static void Run()
 {
  var nav=new DeploymentNavigation(new List<Rect>());
  int aimSprays=0,moveSprays=0;
  for(int seed=1;seed<=100;seed++) { if(FirstChoice(nav,seed,100,0)==FollowupStyle.Spray) aimSprays++; if(FirstChoice(nav,seed,0,100)==FollowupStyle.Spray) moveSprays++; }
  if(aimSprays<70||moveSprays>30) throw new Exception("Aim/movement followup weighting failed");
  var combat=new CombatSystem(new CombatSettings(),new WeaponProfile{damage=0,baseSpreadDegrees=0},2);
  var vision=new VisionSystem(nav,new VisionSettings(),2);
  var positions=new[]{new Vector2(20,20),new Vector2(30,20)}; var facing=new[]{new Vector2(1,0),new Vector2(-1,0)};
  var teams=new[]{0,1}; var alive=new[]{true,true}; var ready=new[]{true,false}; var aim=new[]{50,50};
  for(int tick=0;tick<80;tick++) { vision.Tick(.05f,positions,facing,teams,alive); combat.Tick(.05f,positions,facing,teams,ready,aim,vision); }
  if(combat.Recoil(0)<1||combat.Recoil(0)>3.001f) throw new Exception("Recoil did not accumulate/cap");
  ready[0]=false;
  for(int tick=0;tick<50;tick++) combat.Tick(.05f,positions,facing,teams,ready,aim,vision);
  if(combat.Recoil(0)!=0) throw new Exception("Recoil did not recover while resting");
  combat.Reset(1); if(combat.Recoil(0)!=0||combat.SprayChoices!=0||combat.EvadeChoices!=0) throw new Exception("Recoil state survived round reset");
  // A lethal first shot must not request either nonlethal followup.
  combat=new CombatSystem(new CombatSettings(),new WeaponProfile{damage=1000,baseSpreadDegrees=0},2); ready[0]=true;
  int choices=0; combat.FollowupChosen=(i,s)=>choices++;
  for(int tick=0;tick<30&&combat.Alive(1);tick++) { vision.Tick(.05f,positions,facing,teams,alive); combat.Tick(.05f,positions,facing,teams,ready,aim,vision); }
  if(combat.Alive(1)||choices!=0) throw new Exception("Lethal shot requested followup");
  var fire=typeof(CombatSystem).GetMethod("Fire",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
  int[] hitCounts=new int[3];
  for(int condition=0;condition<3;condition++)
  {
   var spray=new CombatSystem(new CombatSettings(),new WeaponProfile{damage=0,baseSpreadDegrees=0,recoilPerShot=condition==0?0:.18f},2);
   for(int shot=0;shot<40;shot++) fire.Invoke(spray,new object[]{0,1,0f,50f,condition==2?100:0});
   hitCounts[condition]=spray.Hits;
  }
  if(hitCounts[0]<=hitCounts[1]||hitCounts[2]<=hitCounts[1]) throw new Exception("Recoil/aim compensation did not affect actual bullet impacts");
  Debug.Log("RECOIL_IMPACT_OK noRecoil="+hitCounts[0]+" lowAim="+hitCounts[1]+" highAim="+hitCounts[2]);
  Debug.Log("RECOIL_ALL_OK accumulation, recovery, reset, lethal exclusion, aimSprays="+aimSprays+" movementSprays="+moveSprays);
 }
 static FollowupStyle FirstChoice(DeploymentNavigation nav,int seed,int aimValue,int moveValue)
 {
  var combat=new CombatSystem(new CombatSettings(),new WeaponProfile{damage=0,baseSpreadDegrees=0},2); combat.Reset(seed); combat.SetMovementSkill(0,moveValue);
  var vision=new VisionSystem(nav,new VisionSettings(),2); var p=new[]{new Vector2(20,20),new Vector2(30,20)}; var f=new[]{new Vector2(1,0),new Vector2(-1,0)};
  var teams=new[]{0,1}; var alive=new[]{true,true}; var ready=new[]{true,false}; var aim=new[]{aimValue,50};
  bool chosen=false; FollowupStyle first=FollowupStyle.Spray;
  combat.FollowupChosen=(i,s)=>{ if(!chosen) { chosen=true; first=s; } };
  for(int tick=0;tick<30&&!chosen;tick++){ vision.Tick(.05f,p,f,teams,alive); combat.Tick(.05f,p,f,teams,ready,aim,vision); }
  if(!chosen) throw new Exception("Nonlethal first shot did not choose followup");
  if(first==FollowupStyle.EvadeAndTap)
  {
   int shots=combat.Shots;
   for(int tick=0;tick<8;tick++) combat.Tick(.05f,p,f,teams,ready,aim,vision);
   if(combat.Shots!=shots) throw new Exception("Evade choice kept spraying during recovery");
  }
  return first;
 }
}