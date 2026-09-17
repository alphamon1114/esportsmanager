using System;
using System.Collections.Generic;
using System.Reflection;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class BombEquipmentChecks
{
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,F).Invoke(o,args);}
 public static void Run()
 {
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var g=new GameObject("Bomb kit checks").AddComponent<Prototype>();g.Initialize();g.StartMatch();g.AdvanceFrame(3.01f);
  var teams=new int[10];var pos=new Vector2[10];var actors=(List<GameObject>)typeof(Prototype).GetField("actors",F).GetValue(g);
  for(int i=0;i<10;i++){teams[i]=g.TeamIndexOf(i);pos[i]=g.MapPosition(i);}
  var d=g.Director;int ct=g.CounterTerroristTeam;var chosen=new HashSet<int>();
  for(int seed=0;seed<60;seed++){d.RandomizeCarrier(seed,teams,ct);Check(teams[d.Carrier]!=ct,"CT assigned C4");chosen.Add(d.Carrier);}Check(chosen.Count==5,"carrier not randomized across T roster");
  int carrier=d.Carrier,other=-1,defender=-1;for(int i=0;i<10;i++){if(i!=carrier&&teams[i]!=ct)other=i;if(teams[i]==ct)defender=i;}
  actors[other].transform.position=actors[carrier].transform.position;
  Check(g.PassC4(carrier,other)&&d.Carrier==other&&!g.PassC4(other,defender),"C4 handoff/side restriction");
  Check(g.DropC4(other)&&d.BombDropped&&d.Carrier<0,"voluntary C4 drop");
  for(int i=0;i<10;i++)pos[i]=new Vector2(5,5);pos[other]=d.BombPosition;pos[defender]=d.BombPosition;
  Call(d,"UpdateBombCarrier",pos,teams,ct,g.Combat);Check(d.BombDropped,"dropper instantly reclaims or CT steals C4");
  pos[carrier]=d.BombPosition;actors[carrier].transform.position=actors[other].transform.position;Call(d,"UpdateBombCarrier",pos,teams,ct,g.Combat);Check(d.Carrier==carrier&&!d.BombDropped,"T cannot recover C4");
  actors[carrier].transform.position=new Vector3(45,2.2f,42);Check(g.DropC4(carrier),"raised-mid drop");
  actors[other].transform.position=actors[carrier].transform.position;pos[other]=new Vector2(45,58);pos[carrier]=new Vector2(5,5);pos[defender]=new Vector2(5,5);
  Call(d,"UpdateBombCarrier",pos,teams,ct,g.Combat);Check(d.Carrier==other,"raised-mid C4 unrecoverable");carrier=other;
  g.MatchState(defender).defuseKit=true;
  Call(g,"DropBombEquipmentOnDeath",defender);Check(!g.MatchState(defender).defuseKit&&g.GroundKitCount==1,"kit death drop missing");
  // Simulate living CT collecting a teammate's death drop through the regular proximity path.
  Call(g,"TickBombEquipment",.1f);Check(g.MatchState(defender).defuseKit&&g.GroundKitCount==0,"CT cannot pick up kit");
  Call(g,"DropBombEquipmentOnDeath",carrier);Check(d.BombDropped&&d.Carrier<0,"carrier death did not drop bomb");
  g.PrepareNextRound();Check(g.GroundKitCount==0&&!d.BombDropped,"round ground cleanup");
  typeof(Prototype).GetProperty("CompletedRounds").SetValue(g,1,null);
  for(int i=0;i<10;i++){g.MatchState(i).credits=5000;g.MatchState(i).defuseKit=false;g.MatchState(i).armor=0;g.MatchState(i).helmet=false;g.MatchState(i).equipment=new[]{"glock_18"};g.Data.players[i].weaponPosition="rifler";}
  Call(g,"StartBuying");g.AdvanceFrame(3.01f);int kits=0;
  for(int i=0;i<10;i++){if(g.MatchState(i).defuseKit){Check(teams[i]==ct,"T purchased kit");kits++;}Check(g.MatchState(i).credits>=0,"kit overspent");}Check(kits==3,"full CT did not buy three kits");
  var poor=new PlayerMatchState{credits=399};Check(!DefuseKitRules.Buy(poor)&&poor.credits==399,"unaffordable kit charge");poor.credits=400;Check(DefuseKitRules.Buy(poor)&&poor.credits==0&&!DefuseKitRules.Buy(poor),"kit cost/duplicate");
  // Actual planted-bomb handler, both timings and interrupted player changes.
  d=g.Director;typeof(RoundDirector).GetProperty("PlantedSite").SetValue(d,0,null);typeof(RoundDirector).GetProperty("BombPosition").SetValue(d,g.Layout.Sites[0],null);
  int ctPlayer=-1,ctOther=-1;for(int i=0;i<10;i++){pos[i]=new Vector2(5,5);if(teams[i]==ct){ctOther=ctPlayer;ctPlayer=i;}g.MatchState(i).defuseKit=false;}
  d.InteractionAllowed=null;pos[ctPlayer]=d.BombPosition;
  foreach(bool kit in new[]{false,true})
  {
   typeof(RoundDirector).GetProperty("Phase").SetValue(d,RoundPhase.PostPlant,null);typeof(RoundDirector).GetProperty("BombTimer").SetValue(d,40f,null);typeof(RoundDirector).GetProperty("DefuseProgress").SetValue(d,0f,null);g.MatchState(ctPlayer).defuseKit=kit;
   Call(d,"UpdatePlantedBomb",4.9f,pos,teams,ct,g.Combat);Check(d.Phase!=RoundPhase.Ended&&Math.Abs(d.DefuseDuration-(kit?5:10))<.01f,"defuse duration");
   Call(d,"UpdatePlantedBomb",.11f,pos,teams,ct,g.Combat);Check((d.Phase==RoundPhase.Ended)==kit,"kit completion timing");
   if(!kit){Call(d,"UpdatePlantedBomb",5f,pos,teams,ct,g.Combat);Check(d.Outcome==RoundOutcome.BombDefused,"10 second defuse");}
  }
  typeof(RoundDirector).GetProperty("Phase").SetValue(d,RoundPhase.PostPlant,null);typeof(RoundDirector).GetProperty("BombTimer").SetValue(d,40f,null);typeof(RoundDirector).GetProperty("DefuseProgress").SetValue(d,3f,null);
  pos[ctPlayer]=new Vector2(5,5);pos[ctOther]=d.BombPosition;Call(d,"UpdatePlantedBomb",.1f,pos,teams,ct,g.Combat);Check(d.Defuser==ctOther&&d.DefuseProgress<.2f,"defuser change inherited progress");
  Debug.Log("BOMB_EQUIPMENT_ALL_OK random T carrier, give/drop/recover, CT restriction, kit death/pickup, full-buy three kits, price, 5/10s timing, interruption and reset");
 }
}
