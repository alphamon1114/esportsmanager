using System;using System.Reflection;using FpsManager;using UnityEngine;
public static class ConditionChecks {
 static BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
 static void Set(object o,string n,object v){o.GetType().GetProperty(n).SetValue(o,v,null);}
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 public static void Run(){
  var raw=new StatBlock{aim=80,utility=80,movement=80,mental=0,composure=80};
  Check(ConditionRules.Apply(raw,DailyCondition.Low,0,0).aim==72,"low not -10%");raw.mental=100;Check(ConditionRules.Apply(raw,DailyCondition.Low,0,0).aim==76,"mental mitigation wrong");
  Check(ConditionRules.Apply(raw,DailyCondition.Normal,0,0).aim==80&&ConditionRules.Apply(raw,DailyCondition.High,0,0).aim==88,"normal/high multiplier");
  Check(ConditionRules.Apply(raw,DailyCondition.Normal,.05f,.05f).aim==84&&raw.aim==80&&raw.mental==100,"bonus mutated base");
  Check(ConditionRules.TournamentDay(0)==ConditionRules.TournamentDay(3)&&ConditionRules.TournamentDay(4)=="DAY 2"&&ConditionRules.TournamentDay(6)=="DAY 3","tournament day boundaries");
  var outcomes=new System.Collections.Generic.HashSet<DailyCondition>();for(int seed=0;seed<100;seed++){var roll=ConditionRules.Roll("DAY 1","player",seed);Check(roll==ConditionRules.Roll("DAY 1","player",seed),"condition rerolled");outcomes.Add(roll);}Check(outcomes.Count==3,"condition not varied");
  Check(JsonUtility.FromJson<StatBlock>(ConditionRules.MigrateJson("{\"charisma\":73}")).mental==73,"legacy roster migration");
  var g=new GameObject("condition checks").AddComponent<Prototype>();g.Initialize();for(int i=0;i<10;i++)g.Data.players[i].stats=new StatBlock{aim=80,utility=80,movement=80,mental=50,composure=80};g.StartMatch();
  Check(g.StageSeconds==7&&g.ChangeDefense(DefenseTactic.Stack)&&!g.ChangeAttack(AttackTactic.Rush),"buy strategy ownership");
  g.AdvanceFrame(6.99f);Check(g.Stage==MatchStage.Buying&&g.ChangeDefense(DefenseTactic.Forward)&&g.Combat.Shots==0,"buy window ended early");g.AdvanceFrame(.02f);Check(g.Stage==MatchStage.Live&&!g.ChangeDefense(DefenseTactic.Stack)&&g.Director.DefensePlan==DefenseTactic.Forward,"deadline/strategy application");
  Set(g,"Stage",MatchStage.Buying);Set(g,"StageSeconds",7f);Check(g.RequestTimeout(),"timeout failed");
  int ally=g.AlliedTeamIndex,player=ally*5;float before=g.EffectiveStats(player).aim;int credits=g.MatchState(player).credits;
  Check(g.ChooseTimeoutTalk(TimeoutTalk.Aim)&&!g.ChooseTimeoutTalk(TimeoutTalk.Composure),"talk not once per timeout");Check(g.EffectiveStats(player).aim==before+4&&g.Data.players[player].stats.aim==80&&g.MatchState(player).credits==credits,"aim bonus/base/currency");
  Check(((int[])Field(g,"aimStats"))[player]==g.EffectiveStats(player).aim,"combat aim not refreshed");
  g.ResumeTimeout();Check(!g.CanChooseTimeoutTalk&&g.CanChangeStrategy&&g.StageSeconds==7,"resume lost buy window");Check(g.RequestTimeout(),"second timeout failed");
  var daily=(DailyCondition[])Field(g,"dailyCondition");for(int i=0;i<10;i++)daily[i]=DailyCondition.Low;
  Check(g.ChooseTimeoutTalk(TimeoutTalk.Confidence)&&g.PlayerCondition(player)==DailyCondition.Normal,"confidence did not lift one tier");
  Call(g,"BeginTimeoutTalks");Check(g.ChooseTimeoutTalk(TimeoutTalk.Confidence)&&g.PlayerCondition(player)==DailyCondition.High,"confidence did not cap at high");Call(g,"BeginTimeoutTalks");Check(!g.ChooseTimeoutTalk(TimeoutTalk.Confidence)&&g.ChooseTimeoutTalk(TimeoutTalk.Composure),"high condition choice not disabled");Check(g.EffectiveStats(player).composure==92,"composure bonus missing");
  var baseCondition=ConditionRules.Roll(g.ConditionDay,g.Data.players[player].id,0);Call(g,"ResetMapCondition");Check(g.PlayerCondition(player)==baseCondition&&((float[])Field(g,"talkAim"))[ally]==0,"new map retained buffs");
  var db=TournamentRoster.Load();foreach(var p in db.players)Check(p.stats.mental>0,"roster missing mental");
  Debug.Log("CONDITION_ALL_OK daily deterministic tiers, tournament days, mental mitigation, legacy migration, seven-second own-team strategy window, battle lock, shared timeout choices, base isolation, live stat refresh, cap and map reset");
 }
}
