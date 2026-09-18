using System;using System.Reflection;using UnityEngine;using UnityEditor.SceneManagement;using FpsManager;
public static class AiCoachChecks {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static void Run(){
 int[,] expected={{1,0,-1},{0,-1,1},{-1,1,0}};for(int d=0;d<3;d++)for(int a=0;a<3;a++)Check(TacticMatchup.ForDefense((DefenseTactic)d,(AttackTactic)a)==expected[d,a],"matchup matrix");
 var coach=new AiCoach();Check(!coach.Prepare(true,0,0,0,123),"initial plan spent timeout");var initial=coach.Defense;for(int n=0;n<5;n++)Check(!coach.Prepare(true,5,10,1,n),"no-info gear change invented counter");
 coach.Observe(true,new[]{0,0},0,0,0,10);coach.EndRound();Check(coach.Evidence(1,0)==1&&coach.Evidence(1,1)==1&&coach.Evidence(1,2)==1,"empty information changed belief");
 for(int n=0;n<5;n++){coach.Observe(true,new[]{0,0},3,0,3,12);coach.EndRound();}
 coach.Prepare(true,5,6,0,321,true);Check(coach.Defense==DefenseTactic.Forward,"mid play not countered by forward");Check(coach.TimeoutsRemaining>=1,"more than one timeout spent changing plan");
 Check(coach.Score(true,1,5,6,0)>coach.Score(true,1,0,0,0),"rifle strength ignored");Check(coach.Score(false,1,5,6,0)>coach.Score(false,1,5,0,0),"rush utility ignored");
 Check(coach.TimeoutsRemaining==2,"shared timeout consumed own request");
 Check(AiCoach.TimeoutChance(2,3,4)==0,"normal score triggers timeout");Check(AiCoach.TimeoutChance(3,3,4)>0,"three losses not eligible");Check(AiCoach.TimeoutChance(0,8,6)>0,"map point not eligible");
 int requests=0;for(int seed=0;seed<100;seed++){var c=new AiCoach();if(c.TryTimeout(3,4,3,seed))requests++;}Check(requests>0&&requests<100,"timeout is not probabilistic");
 for(int seed=0;seed<100;seed++)coach.TryTimeout(5,8,2,seed);Check(coach.TimeoutsRemaining==0,"two requests not consumed");Check(!coach.TryTimeout(6,8,2,1),"third timeout allowed");
 for(int r=0;r<10;r++){coach.Observe(true,new[]{4,0},0,0,4,12);coach.EndRound();}
 coach.Prepare(true,5,8,0,42,true);Check(coach.Defense==DefenseTactic.Stack&&coach.TimeoutsRemaining==0,"exhausted coach cannot change in other team's timeout");
 var repeated=new AiCoach();var other=new AiCoach();repeated.Prepare(false,4,5,1,55);other.Prepare(false,4,5,1,55);Check(repeated.Defense==other.Defense&&repeated.Attack==other.Attack,"coach seed not deterministic");
 EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var g=new GameObject("AI coach integration").AddComponent<Prototype>();g.Initialize();g.SwapPreviewSides();g.StartMatch();g.AdvanceFrame(Prototype.BuySeconds+.01f);
 var flags=BindingFlags.Instance|BindingFlags.NonPublic;var enemy=(AiCoach)typeof(Prototype).GetField("enemyCoach",flags).GetValue(g);Check(g.Director.DefensePlan==enemy.Defense&&g.TimeoutsRemaining==2&&g.OpponentTimeoutsRemaining==2,"enemy plan integration or budget");
 if(enemy.Defense==DefenseTactic.Stack){int zone=-1;for(int i=0;i<10;i++)if(g.TeamIndexOf(i)==g.CounterTerroristTeam){if(zone<0)zone=g.AssignedZone(i);Check(zone==g.AssignedZone(i),"AI stack not in actual formation");}}
 typeof(Prototype).GetField("coachLossStreak",flags).SetValue(g,3);
 var wins=(int[])typeof(Prototype).GetField("roundWins",flags).GetValue(g);wins[0]=8;wins[1]=2;
 var prepare=typeof(Prototype).GetMethod("PrepareCoach",flags);bool requested=false;
 for(int seed=0;seed<100&&!requested;seed++){typeof(Prototype).GetField("coachPrepared",flags).SetValue(g,false);typeof(Prototype).GetField("roundSeed",flags).SetValue(g,seed);requested=(bool)prepare.Invoke(g,new object[]{false});}
 Check(requested&&g.Stage==MatchStage.Timeout&&g.OpponentTimeout&&g.OpponentTimeoutsRemaining==1&&g.TimeoutsRemaining==2,"AI timeout did not pause both sides independently");
 Check(!g.ChangeAttack(AttackTactic.MidPlay)&&!g.ChangeDefense(DefenseTactic.Forward),"expired buy window must lock tactics");Check(g.ChooseTimeoutTalk(TimeoutTalk.Aim),"shared timeout talk unavailable");
 g.AdvanceFrame(29.9f);Check(g.Stage==MatchStage.Timeout,"AI timeout ended early");g.AdvanceFrame(.11f);Check(g.Stage==MatchStage.Buying&&!g.OpponentTimeout,"shared timeout did not resume buy");
 int remaining=g.OpponentTimeoutsRemaining;Check(g.RequestTimeout()&&g.TimeoutsRemaining==1&&g.OpponentTimeoutsRemaining==remaining,"user timeout consumed AI request");
 Debug.Log("AI_COACH_ALL_OK nine matchups, no hidden plan input, sparse info, counter selection, own gear, deterministic seed, two timeouts, actual opposing formation");
 }
}
