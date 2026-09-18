using System;using System.Reflection;using UnityEngine;using UnityEditor.SceneManagement;using FpsManager;
public static class SideTacticChecks {
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 static Prototype Game(bool attack){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var g=new GameObject("side tactic").AddComponent<Prototype>();g.Initialize();if(attack)g.SwapPreviewSides();return g;}
 static void Tick(Prototype g){var p=new Vector2[10];var a=new Vector2[10];var t=new int[10];var c=new int[10];for(int i=0;i<10;i++){p[i]=g.MapPosition(i);a[i]=g.HomeAnchor(i);t[i]=g.TeamIndexOf(i);c[i]=80;}g.Director.Tick(.1f,p,t,g.CounterTerroristTeam,a,c,g.Vision,g.Combat);}
 public static void Run(){
  foreach(DefenseTactic plan in Enum.GetValues(typeof(DefenseTactic))){var g=Game(false);g.ChangeDefense(plan);g.StartMatch();g.AdvanceFrame(Prototype.BuySeconds+.01f);Tick(g);int[] zones=new int[3];for(int i=0;i<5;i++)zones[g.AssignedZone(i)]++;
   Check(g.Director.DefensePlan==plan,"CT plan not applied");if(plan==DefenseTactic.Stack)Check(zones[0]==5||zones[2]==5,"stack not five at one site");else Check(zones[0]==2&&zones[1]==1&&zones[2]==2,"CT default distribution");Check(!g.ChangeDefense(DefenseTactic.Default),"live CT edit allowed");}
  foreach(AttackTactic plan in Enum.GetValues(typeof(AttackTactic))){var g=Game(true);g.ChangeAttack(plan);g.StartMatch();g.AdvanceFrame(Prototype.BuySeconds+.01f);Tick(g);int[] zones=new int[3];for(int i=0;i<5;i++)zones[g.AssignedZone(i)]++;
   Check(g.Director.AttackPlan==plan,"T plan not applied");if(plan==AttackTactic.Rush){Check(zones[0]==5||zones[2]==5,"rush split formation");Check(g.Director.Lurker<0,"rush retained lurker");for(int i=0;i<5;i++)Check(g.Director.Objective(i).plannedExecute,"rush missing utility preparation");}
   if(plan==AttackTactic.MidPlay){Check(zones[1]==3&&zones[0]==1&&zones[2]==1,"mid formation");int[] teams=new int[10];for(int i=0;i<10;i++)teams[i]=g.TeamIndexOf(i);g.Director.PlanKill(0,5,new Vector2(45,38),teams,g.CounterTerroristTeam);Check(g.Director.MidPhase==1,"mid win did not release site probes");g.Director.PlanKill(1,6,g.Director.Layout.Sites[1],teams,g.CounterTerroristTeam);Check(g.Director.MidPhase==2&&g.Director.TargetSite==1,"site kill not committed");}
   Check(!g.ChangeAttack(AttackTactic.Default),"live T edit allowed");}
  var saved=Game(false);Check(saved.ChangeDefense(DefenseTactic.Stack)&&!saved.ChangeAttack(AttackTactic.Rush),"CT can edit T plan");saved.SwapPreviewSides();Check(saved.ChangeAttack(AttackTactic.Rush)&&!saved.ChangeDefense(DefenseTactic.Forward),"T can edit CT plan");Check(saved.DefensePlan==DefenseTactic.Stack&&saved.AttackPlan==AttackTactic.Rush,"side plans overwrite each other");
  var failed=Game(true);failed.ChangeAttack(AttackTactic.MidPlay);failed.StartMatch();failed.AdvanceFrame(Prototype.BuySeconds+.01f);var tm=new int[10];for(int i=0;i<10;i++)tm[i]=failed.TeamIndexOf(i);failed.Director.PlanKill(5,0,new Vector2(45,38),tm,failed.CounterTerroristTeam);Check(failed.Director.MidPhase==3,"mid loss did not fallback");Tick(failed);Check(failed.Director.Lurker>=0,"fallback did not restore default lurker");
  Debug.Log("SIDE_TACTIC_ALL_OK six plans, formations, utility preparation, no rush lurker, mid win/site commit/loss fallback, independent saved plans, live lock");
 }
}
