using System;
using System.Collections.Generic;
using System.Reflection;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class EngagementFocusChecks
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static void Run()
 {
  var c=new CombatSystem(new CombatSettings(),new WeaponProfile{damage=0},3){UnifiedAim=true};
  var v=new VisionSystem(new DeploymentNavigation(new List<Rect>()),new VisionSettings(),3);
  var p=new[]{new Vector2(20,20),new Vector2(40,20),new Vector2(40.2f,23)};
  var f=new[]{new Vector2(1,0),new Vector2(-1,0),new Vector2(-1,0)};var teams=new[]{0,1,1};
  v.Tick(.5f,p,f,teams);c.UpdateEngagementFocus(.05f,p,f,teams,v);Check(c.FocusTarget(0)==1,"initial target unexpected");
  int switches=0,previous=1;
  for(int n=0;n<80;n++)
  {
   p[1]=new Vector2(n%2==0?40.8f:39.8f,20);p[2]=new Vector2(n%2==0?39.8f:40.8f,21);
   v.Tick(.05f,p,f,teams);c.UpdateEngagementFocus(.05f,p,f,teams,v);
   if(c.FocusTarget(0)!=previous)switches++;previous=c.FocusTarget(0);
   var before=f[0];c.Tick(.05f,p,f,teams,new[]{true,false,false},new[]{85,85,85},v);
   Check(f[0]==before,"combat independently rotated shared aim");
  }
  Check(switches==0&&c.Shots>0,"crossing targets switched repeatedly or prevented shots");
  var remembered=c.FocusPoint(0);int shots=c.Shots;
  v.PairGeometry=(observer,target,a,b)=>observer!=0||target!=1;
  for(int n=0;n<4;n++)
  {
   p[1]=new Vector2(45,35+n);v.Tick(.05f,p,f,teams);c.UpdateEngagementFocus(.05f,p,f,teams,v);
   c.Tick(.05f,p,f,teams,new[]{true,false,false},new[]{85,85,85},v);
   Check(c.FocusTarget(0)==1&&c.FocusPoint(0)==remembered,"short occlusion changed target or tracked hidden position");
  }
  Check(c.Shots==shots,"held invisible target was shot");
  v.PairGeometry=null;p[1]=new Vector2(40,20);v.Tick(.5f,p,f,teams);c.UpdateEngagementFocus(.01f,p,f,teams,v);
  Check(c.FocusTarget(0)==1,"brief cover exit failed to retain target");
  v.PairGeometry=(observer,target,a,b)=>observer!=0||target!=1;
  for(int n=0;n<6;n++){v.Tick(.05f,p,f,teams);c.UpdateEngagementFocus(.05f,p,f,teams,v);}
  Check(c.FocusTarget(0)==2,"long occlusion failed to release target");
  c.Equip(1,new WeaponProfile{damage=1000});typeof(CombatSystem).GetMethod("ApplyHit",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{1,2,HitRegion.Head});
  v.PairGeometry=null;v.Tick(.5f,p,f,teams);c.UpdateEngagementFocus(.05f,p,f,teams,v);Check(c.FocusTarget(0)==1,"dead target retained");
  c.Reset(7);Check(c.FocusTarget(0)==-1,"focus survived round reset");
  p[1]=new Vector2(50,20);p[2]=new Vector2(40,20);v.Tick(.5f,p,f,teams);c.UpdateEngagementFocus(.05f,p,f,teams,v);
  Check(c.FocusTarget(0)==2,"urgent fixture did not start far target");
  p[1]=new Vector2(24,20);v.Tick(.5f,p,f,teams);c.UpdateEngagementFocus(.05f,p,f,teams,v);Check(c.FocusTarget(0)==1,"point-blank threat could not interrupt");
  // The live scene's movement and peek requests share one yaw update, even if they request opposite angles.
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var g=new GameObject("Shared aim check").AddComponent<Prototype>();g.Initialize();g.StartMatch();g.AdvanceFrame(Prototype.BuySeconds+.01f);
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;var actors=(List<GameObject>)typeof(Prototype).GetField("actors",flags).GetValue(g);
  var positions=new Vector2[10];var facing=new Vector2[10];var team=new int[10];
  for(int i=0;i<10;i++){positions[i]=new Vector2(90,90);facing[i]=new Vector2(1,0);team[i]=g.TeamIndexOf(i);}
  actors[0].transform.position=new Vector3(30,1,12);positions[0]=new Vector2(30,88);positions[5]=new Vector2(40,88);positions[6]=new Vector2(41,87);
  var vision=new VisionSystem(new DeploymentNavigation(new List<Rect>()),new VisionSettings(),10);vision.Tick(.5f,positions,facing,team);
  typeof(Prototype).GetField("vision",flags).SetValue(g,vision);g.Combat.UpdateEngagementFocus(.1f,positions,facing,team,vision);
  Check(g.Combat.FocusTarget(0)==5,"scene target not acquired");
  actors[0].transform.rotation=Quaternion.LookRotation(new Vector3(0,0,-1));var initial=g.MapFacing(0);
  var turn=typeof(Prototype).GetMethod("FaceWatch",flags);turn.Invoke(g,new object[]{0,new Vector2(-1,0),.1f});var first=g.MapFacing(0);
  turn.Invoke(g,new object[]{0,new Vector2(0,-1),.1f});Check(Vector2.Distance(first,g.MapFacing(0))<.0001f,"second aim writer changed same-tick yaw");
  Check(Vector2.Distance(initial,first)<.0001f,"human acquisition delay was bypassed");
  // A new contact now has perception latency; verify bounded convergence after that delay.
  var applied=(bool[])typeof(Prototype).GetField("aimApplied",flags).GetValue(g);
  for(int n=0;n<12;n++){applied[0]=false;var before=g.MapFacing(0);turn.Invoke(g,new object[]{0,new Vector2(-1,0),.05f});Check(Mathf.Abs(CombatSystem.SignedAngle(before,g.MapFacing(0)))<=10.01f,"human turn exceeded bound");}
  Check(g.MapFacing(0).x>.5f&&g.Combat.FocusTarget(0)==5,"delayed yaw ignored stable focus");
  Debug.Log("ENGAGEMENT_FOCUS_ALL_OK crossing switches="+switches+", continued fire, short occlusion, frozen memory/no hidden shot, release, death, urgent close threat, reset, single bounded yaw writer");
 }
}
