using System;using System.Reflection;using System.Collections.Generic;using FpsManager;using UnityEngine;
public static class MidTrafficChecks {
 static BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(object g,string n){return g.GetType().GetField(n,F).GetValue(g);}
 static object Call(object g,string n,params object[] a){return g.GetType().GetMethod(n,F).Invoke(g,a);}
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static Prototype Game(string map){var g=new GameObject("mid traffic").AddComponent<Prototype>();g.Initialize();Call(g,"ActivateMatchMap",map);g.StartMatch();g.AdvanceFrame(7.01f);return g;}
 public static void Run(){
  foreach(string map in new[]{"de_inferno","de_dust2","de_mirage","de_nuke","de_vertigo"}){
   var g=Game(map);var actors=(List<GameObject>)Field(g,"actors");var arena=(SourceArena)Field(g,"sourceArena");var gates=(Vector3[])Field(g,"midEntry");var directions=(Vector2[])Field(g,"midEntryDirection");
   for(int side=0;side<2;side++){
    int i=side*5;
    var gate=gates[side];var inward=new Vector3(directions[side].x,0,-directions[side].y);
    actors[i].transform.position=gate-inward*.5f+Vector3.up;
    Check(!(bool)Call(g,"RequiresMidGun",i),"gun boundary before entrance "+map);
    actors[i].transform.position=gate+inward*.5f+Vector3.up;
    Check((bool)Call(g,"RequiresMidGun",i),"gun boundary after entrance "+map);
    actors[i].transform.position=arena.Mid+Vector3.up;
    Check((bool)Call(g,"RequiresMidGun",i),"mid centre unarmed "+map+" side="+side);
    Call(g,"UpdateTravelWeapon",i,new PlayerObjective{valid=true,task=PlayerTask.MoveToLane,destination=g.MapPosition(i)+new Vector2(30,0)});
    Check(!g.Combat.KnifeOut(i),"mid travel redraws knife");
    actors[i].transform.position=(side==0?arena.CT:arena.T)+Vector3.up;
    Check(!(bool)Call(g,"RequiresMidGun",i),"spawn blocked knife "+map);
   }
   Debug.Log("MID_GUN_OK "+map);
  }
  RunMirageQueues();RunInfernoMidQueues();
  Debug.Log("MID_TRAFFIC_ALL_OK");
 }
 public static void RunInfernoMidQueues(){
  foreach(int fps in new[]{60,144}){
   var g=Game("de_inferno");var actors=(List<GameObject>)Field(g,"actors");var delay=(float[])Field(g,"repathDelay");var posts=g.Director.Layout.MidPosts;
   for(int i=0;i<5;i++)actors[i].transform.position=new Vector3(i*3,101,0);
   for(int i=0;i<5;i++)for(int j=0;j<i;j++)Check(Vector2.Distance(posts[i],posts[j])>=1.79f,"Inferno mid posts overlap");
   for(int frame=0;frame<fps*45;frame++){
    Call(g,"BeginPlayerMovement");for(int i=5;i<10;i++){delay[i]=Math.Max(0,delay[i]-1f/fps);Call(g,"SourceMoveTo",i,posts[i-5]);Call(g,"SourceStepRoute",i,1f/fps);}Call(g,"ResolvePlayerMovement",1f/fps);
    if(frame%12==0)for(int i=5;i<10;i++)for(int j=5;j<i;j++)Check(g.PlayerHeight(i)+(g.IsCrouched(i)?1.2f:1.8f)<=g.PlayerHeight(j)||g.PlayerHeight(j)+(g.IsCrouched(j)?1.2f:1.8f)<=g.PlayerHeight(i)||Vector2.Distance(g.MapPosition(i),g.MapPosition(j))>=.995f,"Inferno mid queue overlap");
   }
   for(int i=5;i<10;i++)Check(Vector2.Distance(g.MapPosition(i),posts[i-5])<1.1f,"Inferno mid jam fps="+fps+" i="+i+" at="+g.MapPosition(i)+" goal="+posts[i-5]);
   Debug.Log("INFERNO_MID_QUEUE_OK fps="+fps);
  }
 }
 public static void RunMirageQueues(){
  foreach(int fps in new[]{60,144}){
   var g=Game("de_mirage");var actors=(List<GameObject>)Field(g,"actors");var delay=(float[])Field(g,"repathDelay");
   for(int i=0;i<5;i++)actors[i].transform.position=new Vector3(i*3,101,0);
   for(int frame=0;frame<fps*60;frame++){
    Call(g,"BeginPlayerMovement");for(int i=5;i<10;i++){delay[i]=Math.Max(0,delay[i]-1f/fps);Call(g,"SourceMoveTo",i,g.Director.Layout.HoldRing[0][i-5]);Call(g,"SourceStepRoute",i,1f/fps);}Call(g,"ResolvePlayerMovement",1f/fps);
    if(frame==fps*30)for(int i=5;i<10;i++)Check(Vector2.Distance(g.MapPosition(i),new Vector2(97.2f,50))>6,"Mirage narrow entrance still occupied after 30s");
    if(frame%12==0)for(int i=5;i<10;i++)for(int j=5;j<i;j++)Check(g.PlayerHeight(i)+(g.IsCrouched(i)?1.2f:1.8f)<=g.PlayerHeight(j)||g.PlayerHeight(j)+(g.IsCrouched(j)?1.2f:1.8f)<=g.PlayerHeight(i)||Vector2.Distance(g.MapPosition(i),g.MapPosition(j))>=.995f,"Mirage queue overlap");
   }
   for(int i=5;i<10;i++)Check(Vector2.Distance(g.MapPosition(i),g.Director.Layout.HoldRing[0][i-5])<1.5f,"Mirage A queue jam fps="+fps+" i="+i+" at="+g.MapPosition(i));
   Debug.Log("MIRAGE_QUEUE_OK fps="+fps);
  }
  Debug.Log("MIRAGE_TRAFFIC_ALL_OK");
 }
}
