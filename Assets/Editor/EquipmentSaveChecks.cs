using System;using System.Reflection;using System.Collections.Generic;using FpsManager;using UnityEngine;
public static class EquipmentSaveChecks {
 static BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 public static void Run(){
  Check(EquipmentSaveRules.ShouldSave(BuyPlan.Full,true,2,5,60,true,true,true,false),"poor CT 2v5 did not save");
  foreach(var plan in new[]{BuyPlan.Pistol,BuyPlan.Force,BuyPlan.Eco})Check(!EquipmentSaveRules.ShouldSave(plan,true,1,5,60,true,true,true,false),"cheap buy saved");
  Check(!EquipmentSaveRules.ShouldSave(BuyPlan.Full,true,3,5,60,true,true,true,false),"small deficit saved");
  Check(!EquipmentSaveRules.ShouldSave(BuyPlan.Full,true,1,5,60,true,false,true,false),"rich team saved");
  Check(!EquipmentSaveRules.ShouldSave(BuyPlan.Full,true,1,5,60,true,true,false,false),"pistol saved");
  Check(!EquipmentSaveRules.ShouldSave(BuyPlan.Full,true,1,5,60,true,true,true,true),"match point saved");
  Check(EquipmentSaveRules.ShouldSave(BuyPlan.Full,false,1,5,12,false,true,true,false),"early T wipe ignored");
  Check(!EquipmentSaveRules.ShouldSave(BuyPlan.Full,false,1,5,40,false,true,true,false)&&!EquipmentSaveRules.ShouldSave(BuyPlan.Full,false,1,5,12,true,true,true,false),"T abandoned late attack/planted bomb");
  var g=new GameObject("equipment save").AddComponent<Prototype>();g.Initialize();g.StartMatch();g.AdvanceFrame(Prototype.BuySeconds+.01f);
  var teams=(int[])Field(g,"teamIndex");var states=(PlayerMatchState[])Field(g,"matchState");var plans=(BuyPlan[])Field(g,"buyPlans");var actors=(List<GameObject>)Field(g,"actors");var ct=new List<int>();int enemy=-1;
  for(int i=0;i<10;i++){states[i].credits=0;states[i].equipment=new[]{"m4a1_s","usp_s"};if(teams[i]==g.CounterTerroristTeam)ct.Add(i);else enemy=i;}
  plans[g.CounterTerroristTeam]=BuyPlan.Full;g.Combat.Equip(enemy,new WeaponProfile{damage=1000});for(int k=2;k<5;k++)Call(g.Combat,"ApplyHit",enemy,ct[k],HitRegion.Head);
  Set(g.Director,"Clock",15f);Call(g,"DecideEquipmentSave");Check(g.IsSavingEquipment(ct[0])&&g.IsSavingEquipment(ct[1]),"live decision not connected");
  int saver=ct[0];var threats=new List<Vector2>();Check((bool)Call(g,"FindSaveCover",saver,threats),"no valid cover in Inferno");
  var goals=(Vector2[])Field(g,"saveGoal");var goal=goals[saver];actors[saver].transform.position=new Vector3(goal.x,1,100-goal.y);Call(g,"GroundActor",saver);
  Call(g,"StepEquipmentSave",saver,.1f);Check(g.IsSaveCrouched(saver),"arrived saver did not crouch");
  float floor=g.PlayerHeight(saver);Check(Math.Abs((float)Call(g,"StanceHeight",saver)-(floor-.55f))<.01f,"crouch did not lower sight/hit height");
  // Hidden enemy coordinates cannot affect the hiding decision.
  var before=goals[saver];for(int i=0;i<10;i++)if(teams[i]!=teams[saver])actors[i].transform.position=new Vector3(90+i,1,5);
  Call(g,"FindSaveCover",saver,threats);Check(goals[saver]==before,"hidden positions changed cover");
  Set(g.Director,"Phase",RoundPhase.PostPlant);Set(g.Director,"BombPosition",goal);Set(g.Director,"PlantedSite",0);Set(g.Director,"BombTimer",20f);
  Check(!(bool)Call(g,"SafeSaveSpot",goal,threats,floor),"save inside bomb radius");
  var positions=new Vector2[10];for(int i=0;i<10;i++)positions[i]=g.MapPosition(i);positions[saver]=goal;
  g.Director.InteractionAllowed=null;g.Director.DefuseSafe=i=>true;g.Director.PreferredDefuser=null;
  for(int i=0;i<10;i++)if(i!=saver)positions[i]=goal+new Vector2(40,0);
  Call(g.Director,"UpdatePlantedBomb",.1f,positions,teams,g.CounterTerroristTeam,g.Combat);Check(g.Director.Defuser!=saver,"saving player started defuse");
  plans[g.CounterTerroristTeam]=BuyPlan.Eco;Set(g.Director,"Clock",16f);Call(g,"DecideEquipmentSave");Check(!g.IsSavingEquipment(saver),"eco kept saving rifle");
  plans[g.CounterTerroristTeam]=BuyPlan.Full;Set(g.Director,"Clock",17f);Call(g,"DecideEquipmentSave");Check(g.IsSavingEquipment(saver),"save did not resume");
  Set(g.Director,"Phase",RoundPhase.Ended);Set(g.Director,"Outcome",RoundOutcome.BombExploded);Call(g,"CompleteRoundOnce");
  Check(states[saver].credits==2400&&Array.IndexOf(states[saver].equipment,"m4a1_s")>=0,"survivor lost equipment/reward");
  g.PrepareNextRound();Check(!g.IsSavingEquipment(saver)&&!g.IsSaveCrouched(saver)&&Array.IndexOf(states[saver].equipment,"m4a1_s")>=0,"save state carried over or gun lost");
  foreach(var map in new[]{"de_inferno","de_dust2","de_mirage","de_nuke","de_vertigo"}){
   var m=new GameObject("source save "+map).AddComponent<Prototype>();m.Initialize();Call(m,"ActivateMatchMap",map);m.StartMatch();m.AdvanceFrame(Prototype.BuySeconds+.01f);int player=0;while(m.TeamIndexOf(player)!=m.CounterTerroristTeam)player++;
   typeof(Prototype).GetField("sourceNavHeight",F).SetValue(m,m.PlayerHeight(player));
   Check((bool)Call(m,"FindSaveCover",player,new List<Vector2>()),"source save cover missing: "+map);
   var chosenGoals=(Vector2[])Field(m,"saveGoal");var start=m.MapPosition(player);var chosenGoal=chosenGoals[player];
   ((bool[])Field(m,"savingEquipment"))[player]=true;
   for(int frame=0;frame<1500&&Vector2.Distance(m.MapPosition(player),chosenGoal)>.8f;frame++)Call(m,"StepEquipmentSave",player,.1f);
   Check(Vector2.Distance(m.MapPosition(player),chosenGoal)<=.8f,"source save path stuck: "+map);
   Call(m,"StepEquipmentSave",player,.1f);Check(m.IsSaveCrouched(player),"source save did not crouch: "+map);
   Debug.Log("SOURCE_SAVE_OK "+map+" travel="+Vector2.Distance(start,chosenGoal));
  }
  Debug.Log("EQUIPMENT_SAVE_ALL_OK economy, full-only, deficit, CT/T timing, match point, cover, hidden-info isolation, crouch height, bomb exclusion, survival and reset");
 }
}
