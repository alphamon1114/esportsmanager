using System;using System.Reflection;using FpsManager;using UnityEngine;
public static class CtOpeningChecks {
 static BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool b,string message){if(!b)throw new Exception(message);}
 public static void Run(){
  Check(PlayerCollision.Blocked(new Vector3(0,1,0),new Vector3(.1f,1,0),new Vector3(.5f,2.27f,0)),"standing bodies overlap undetected");
  Check(!PlayerCollision.Blocked(new Vector3(0,1,0),new Vector3(.1f,1,0),new Vector3(.5f,2.27f,0),1.2f,1.8f),"crouched vertical clearance rejected");
  Check(PlayerCollision.Blocked(new Vector3(.6f,2.21f,0),new Vector3(.65f,2.19f,0),new Vector3(0,1,0),1.2f,1.2f),"vertical entry accepted as existing overlap separation");
  foreach(int fps in new[]{60,144}){
   var g=new GameObject("Nuke CT opening").AddComponent<Prototype>();g.Initialize();typeof(Prototype).GetMethod("ActivateMatchMap",F).Invoke(g,new object[]{"de_nuke"});g.ChangeDefense(DefenseTactic.Forward);g.StartMatch();g.AdvanceFrame(7.01f);
   var spawn=new Vector2[5];for(int i=0;i<5;i++)spawn[i]=g.MapPosition(i);
   for(int frame=0;frame<fps*22;frame++){
    g.AdvanceFrame(1f/fps);
    if(frame==fps)for(int i=0;i<5;i++)Check(g.Combat.KnifeOut(i),"CT opening did not draw knife");
   }
   for(int i=0;i<5;i++)Check(Vector2.Distance(spawn[i],g.MapPosition(i))>35,"CT opening jam fps="+fps+" player="+i);
   Check(g.Director.Objective(2).destination!=g.Director.Objective(4).destination,"lower site forward pair share one mouth");
   // No arbitrary 35-second expiry in a quiet scenario.
   g.Vision.Reset();typeof(RoundDirector).GetProperty("Clock").SetValue(g.Director,40f,null);
   var positions=new Vector2[10];var anchors=new Vector2[10];var teams=new int[10];var composure=new int[10];for(int i=0;i<10;i++){positions[i]=g.MapPosition(i);anchors[i]=g.HomeAnchor(i);teams[i]=g.TeamIndexOf(i);composure[i]=80;}
   g.Director.Tick(.01f,positions,teams,g.CounterTerroristTeam,anchors,composure,g.Vision,g.Combat);
   Check(g.Director.Objective(0).forwardAdvance,"forward expired at 35 seconds");
   var done=(bool[])typeof(Prototype).GetField("ctOpeningDone",F).GetValue(g);done[0]=false;
   typeof(Prototype).GetMethod("RegisterHurt",F).Invoke(g,new object[]{5,0});
   Check(!(bool)typeof(Prototype).GetMethod("StepCtOpening",F).Invoke(g,new object[]{0,.1f})&&!g.Combat.KnifeOut(0),"opening ignored incoming fire");
   Debug.Log("CT_OPENING_CASE_OK fps="+fps+" knife departure, Nuke traversal, distinct approaches, no timed expiry, damage interrupt");
  }
  Debug.Log("CT_OPENING_ALL_OK");
 }
}
