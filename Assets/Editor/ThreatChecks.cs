using System;using System.Collections.Generic;using FpsManager;using UnityEngine;
public static class ThreatChecks {
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 public static void Run(){
 Check(!ThreatRules.CanReach(new Vector2(50,0),2,new Vector2(0,0)),"recent distant enemy incorrectly reaches rear");
 Check(ThreatRules.CanReach(new Vector2(50,0),10,new Vector2(0,0)),"old info excludes reachable rear");
 Check(ThreatRules.Priority(false,true,15,1)>ThreatRules.Priority(false,false,40,0),"remote info overrides local danger");
 Check(ThreatRules.Priority(true,false,40,0)>ThreatRules.Priority(false,true,15,0),"heard cue overrides direct fight");
 var nav=new DeploymentNavigation(new List<Rect>());var search=new ClutchSearch();Vector2 target;
 search.Step(.1f,new Vector2(30,30),new Vector2(-1,0),new Vector2(1,0),nav,out target,new Vector2(45,30));
 Check(target==new Vector2(30,30),"clutch moved while facing away");
  var g=new GameObject("threat fixture").AddComponent<Prototype>();g.Initialize();g.StartMatch();g.AdvanceFrame(Prototype.BuySeconds+.01f);
 var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
 var type=typeof(Prototype);var vision=(VisionSystem)type.GetField("vision",flags).GetValue(g);
 var contacts=(ContactInfo[])typeof(VisionSystem).GetField("knowledge",flags).GetValue(vision);
 var teams=(int[])type.GetField("teamIndex",flags).GetValue(g);
 int enemy=5;var point=g.MapPosition(0)+new Vector2(10,0);
 contacts[teams[0]*10+enemy]=new ContactInfo{known=true,anonymous=true,age=0,lastKnownPosition=point};
 var kill=new KillEvent{killer=enemy,victim=0};type.GetMethod("RegisterTrade",flags).Invoke(g,new object[]{kill});
 var until=(float[])type.GetField("tradeUntil",flags).GetValue(g);foreach(var t in until)Check(t==0,"unseen killer leaked into trade");
 contacts[teams[0]*10+enemy]=new ContactInfo{known=true,anonymous=false,age=.2f,lastKnownPosition=point};
 type.GetMethod("RegisterTrade",flags).Invoke(g,new object[]{kill});int responders=0;int chosen=-1;for(int i=0;i<10;i++)if(until[i]>0){responders++;chosen=i;}
 Check(responders==1&&chosen!=0&&teams[chosen]==teams[0],"trade did not select one nearby teammate");
 var points=(Vector2[])type.GetField("tradePoint",flags).GetValue(g);Check(points[chosen]==point,"trade did not preserve observed position");
  var memory=(Vector2[,])type.GetField("remembered",flags).GetValue(g);var times=(float[,])type.GetField("rememberedAt",flags).GetValue(g);
 for(int e=0;e<10;e++)if(teams[e]!=teams[0]){memory[teams[0],e]=g.MapPosition(0)+new Vector2(40,0);times[teams[0],e]=g.Director.Clock;}
 object[] cueArgs={0,new Vector2()};var cueMethod=type.GetMethod("ClutchKnownCue",flags);
 Check((bool)cueMethod.Invoke(g,cueArgs),"complete recent info did not constrain search");
 times[teams[0],enemy]=g.Director.Clock-10;
 Check(!(bool)cueMethod.Invoke(g,new object[]{0,new Vector2()}),"reachable opponent still excluded unknown angles");
 times[teams[0],enemy]=-100;
 Check(!(bool)cueMethod.Invoke(g,new object[]{0,new Vector2()}),"missing opponent info suppressed rear checks");
 type.GetMethod("ResetThreats",flags).Invoke(g,null);foreach(var t in until)Check(t==0,"round retained trade cue");
 typeof(RoundDirector).GetProperty("Defuser").SetValue(g.Director,0,null);
 typeof(RoundDirector).GetProperty("DefuseProgress").SetValue(g.Director,2f,null);
 type.GetMethod("RegisterHurt",flags).Invoke(g,new object[]{enemy,0});
 Check(g.Director.Defuser<0&&g.Director.DefuseProgress==0,"damage did not immediately cancel defuse");
 Check(!(bool)type.GetMethod("SafeToDefuse",flags).Invoke(g,new object[]{0}),"recently hit player immediately restarted defuse");
 var hurt=(float[])type.GetField("hurtUntil",flags).GetValue(g);
 Check(hurt[0]>=g.Director.Clock+2.4f,"damage bearing expires before rear turn");
 Check((bool)type.GetMethod("StepThreatResponse",flags).Invoke(g,new object[]{0,.1f}),"unengaged victim ignored incoming damage");
 Debug.Log("THREAT_ALL_OK local priority, direct fight, reachable uncertainty, face-before-expose");
 }
}


