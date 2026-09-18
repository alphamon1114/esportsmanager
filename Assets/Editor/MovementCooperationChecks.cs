using System;using System.Reflection;using System.Collections.Generic;using FpsManager;using UnityEngine;
public static class MovementCooperationChecks {
 static BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(object g,string n){return g.GetType().GetField(n,F).GetValue(g);}
 static object Call(object g,string n,params object[] a){return g.GetType().GetMethod(n,F).Invoke(g,a);}
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static Prototype Game(string map=null){var g=new GameObject("cooperation").AddComponent<Prototype>();g.Initialize();if(map!=null)Call(g,"ActivateMatchMap",map);g.ChangeDefense(DefenseTactic.Stack);g.StartMatch();g.AdvanceFrame(Prototype.BuySeconds+.01f);return g;}
 public static void Run(){
  Check(Prototype.InFiringLane(Vector2.zero,new Vector2(10,0),new Vector2(2,.3f)),"front ally not recognized");
  Check(!Prototype.InFiringLane(Vector2.zero,new Vector2(10,0),new Vector2(-2,0)),"rear ally yielded");
  Check(!Prototype.InFiringLane(Vector2.zero,new Vector2(10,0),new Vector2(2,2)),"side ally yielded");
  foreach(int fps in new[]{60,144}){
   var g=Game();var actors=(List<GameObject>)Field(g,"actors");
   // Open geometry, stationary friendly obstruction, no scripted combat.
   typeof(Prototype).GetField("navigation",F).SetValue(g,new DeploymentNavigation(new List<Rect>()));
   for(int i=0;i<10;i++)actors[i].transform.position=new Vector3(i*3,101,0);
   actors[0].transform.position=new Vector3(20,1,20);actors[1].transform.position=new Vector3(21.05f,1,20);
   for(int frame=0;frame<fps*6;frame++){
    Call(g,"BeginPlayerMovement");actors[0].transform.position=Vector3.MoveTowards(actors[0].transform.position,new Vector3(27,1,20),5f/fps);Call(g,"ResolvePlayerMovement",1f/fps);
    Check((actors[0].transform.position-actors[1].transform.position).magnitude>=.995f,"yield overlapped bodies");
   }
   Check(actors[0].transform.position.x>26,"stationary obstacle deadlock "+fps);
   // Seed an actual ongoing engagement, then move an ally across its lane.
   var states=(CombatState[])Field(g.Combat,"states");var state=states[0];state.target=5;states[0]=state;
   var focuses=(EngagementFocus[])Field(g.Combat,"engagement");focuses[0]=new EngagementFocus();typeof(EngagementFocus).GetProperty("Target").SetValue(focuses[0],5,null);typeof(EngagementFocus).GetProperty("Point").SetValue(focuses[0],new Vector2(40,80),null);
   actors[0].transform.position=new Vector3(20,1,20);actors[1].transform.position=new Vector3(22,1,20);
   Call(g,"ClearFriendlyFiringLanes");Check(g.IsCrouched(1),"blocking teammate did not crouch");
   Call(g,"ResetTraversal");Check(!g.IsCrouched(1),"crouch leaked into next round");
   typeof(RoundDirector).GetProperty("Defuser").SetValue(g.Director,1,null);typeof(RoundDirector).GetProperty("DefuseProgress").SetValue(g.Director,2f,null);
   Call(g,"ClearFriendlyFiringLanes");Check(!g.IsCrouched(1)&&((float[])Field(g,"yieldFor"))[0]>0,"defuser displaced instead of rear shooter");
   Debug.Log("COOPERATION_TRAFFIC_OK fps="+fps);
  }
  foreach(string id in new[]{"de_inferno","de_dust2","de_mirage","de_nuke","de_vertigo"}){
   var g=Game(id);var layout=g.Director.Layout;var arena=SourceArena.Load(id);
   for(int s=0;s<2;s++){
    int hidden=0;for(int i=0;i<5;i++){
     for(int j=0;j<i;j++)Check(Vector2.Distance(layout.StackPost[s][i],layout.StackPost[s][j])>=2,"duplicate stack positions "+id);
     var post=SourceArena.At(layout.StackPost[s][i],arena.Floor(layout.StackPost[s][i],arena.Sites[s].y));bool allHidden=true;
     foreach(var a in layout.Approaches[s])if(arena.Sight(post+Vector3.up*1.1f,SourceArena.At(a,arena.Floor(a,arena.Sites[s].y)+1.65f)))allHidden=false;
     if(i>=2&&allHidden)hidden++;
    }
    Debug.Log("STACK_COVER "+id+" site="+s+" hidden="+hidden+"/3");
    Check(hidden==3,"stack cover not hidden "+id+" site="+s);
   }
   var positions=new Vector2[10];var anchors=new Vector2[10];var teams=new int[10];var composure=new int[10];for(int i=0;i<10;i++){positions[i]=g.MapPosition(i);anchors[i]=g.HomeAnchor(i);teams[i]=g.TeamIndexOf(i);composure[i]=80;}
   g.Director.Tick(.1f,positions,teams,g.CounterTerroristTeam,anchors,composure,g.Vision,g.Combat);
   int traders=0,guards=0;for(int i=0;i<5;i++){var o=g.Director.Objective(i);if(o.stackHold){if(o.stackHidden)traders++;else guards++;}}
   Check(traders==3&&guards==2,"stack role count");
  }
  foreach(int fps in new[]{60,144}){
   var g=Game("de_vertigo");g.AdvanceFrame(.02f);var actors=(List<GameObject>)Field(g,"actors");var orders=new PlayerObjective[5];for(int i=0;i<5;i++)orders[i]=g.Director.Objective(i);
   for(int i=5;i<10;i++)actors[i].transform.position=new Vector3(0,101+i,0);
   var delay=(float[])Field(g,"repathDelay");
   for(int frame=0;frame<fps*35;frame++){
    Call(g,"BeginPlayerMovement");for(int i=0;i<5;i++){delay[i]=Math.Max(0,delay[i]-1f/fps);Call(g,"StepStackPost",i,orders[i],1f/fps);}Call(g,"ResolvePlayerMovement",1f/fps);
    if(frame%12==0)for(int i=0;i<5;i++)for(int j=0;j<i;j++)Check(Vector2.Distance(g.MapPosition(i),g.MapPosition(j))>=.995f,"stack route body overlap");
   }
   for(int i=0;i<5;i++)Check(Vector2.Distance(g.MapPosition(i),orders[i].destination)<.4f,"stack mid-route jam fps="+fps+" player="+i);
   Debug.Log("STACK_TRAVEL_OK fps="+fps+" all five reach distinct posts");
  }
  var jumper=Game("de_vertigo");var ja=(List<GameObject>)Field(jumper,"actors");var ar=SourceArena.Load("de_vertigo");bool jumped=false;
  foreach(var area in ar.Areas.Values){if(area.hull!=0)continue;foreach(int id in area.connections){var b=ar.Areas[id];var a=SourceArena.Project(area,b.Center);var dest=SourceArena.Project(b,a);float rise=dest.y-a.y;if(rise<.38f||rise>1.15f||Vector2.Distance(SourceArena.Flat(a),SourceArena.Flat(dest))>2.4f)continue;
   for(int j=0;j<10;j++)ja[j].transform.position=new Vector3(j*3,101,0);ja[0].transform.position=a+Vector3.up;
   ((List<Vector3>[])Field(jumper,"sourceRoutes"))[0]=new List<Vector3>{dest};((int[])Field(jumper,"routeSteps"))[0]=0;
   if(!(bool)Call(jumper,"PrepareSourceTraversal",0,1f/60))continue;
   Check(jumper.IsJumping(0)&&jumper.IsCrouched(0),"source jump did not crouch");float peak=jumper.PlayerHeight(0);
   for(int n=0;n<60&&jumper.IsJumping(0);n++){Call(jumper,"StepSourceJump",0,1f/60);peak=Math.Max(peak,jumper.PlayerHeight(0));}
   Check(!jumper.IsJumping(0)&&(ja[0].transform.position-(dest+Vector3.up)).magnitude<.02f&&peak>dest.y+.05f,"source jump failed to land");jumped=true;break;
  }if(jumped)break;}Check(jumped,"no executable source jump");
  var start=new Vector3(0,0,0);var end=new Vector3(1,.8f,0);Check((Prototype.JumpArc(start,end,0)-start).magnitude<.001f&&(Prototype.JumpArc(start,end,1)-end).magnitude<.001f,"jump endpoints");Check(Prototype.JumpArc(start,end,.5f).y>.9f,"jump arc missing");
  Debug.Log("MOVEMENT_COOPERATION_ALL_OK traffic, friendly firing lanes, crouch reset, five-map cover formations, jump arc");
 }
}
