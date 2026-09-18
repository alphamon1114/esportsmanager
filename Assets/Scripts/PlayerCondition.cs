using System;
using UnityEngine;
namespace FpsManager {
 public enum DailyCondition { Low, Normal, High }
 public enum TimeoutTalk { Aim, Confidence, Composure }
 public static class ConditionRules {
  public static string TournamentDay(int series){return "DAY "+(series<4?1:series<6?2:3);}
  public static string MigrateJson(string json){return json.Replace("\"charisma\"","\"mental\"");}
  public static DailyCondition Roll(string day,string player,int seed){unchecked{uint h=2166136261u^(uint)seed;foreach(char c in day+"/"+player){h^=c;h*=16777619;}return(DailyCondition)(new DeterministicRandom((int)h).NextUInt()%3);}}
  public static float Multiplier(DailyCondition state,int mental){return state==DailyCondition.High?1.1f:state==DailyCondition.Low?.9f+.05f*Mathf.Clamp01(mental/100f):1;}
  public static StatBlock Apply(StatBlock raw,DailyCondition state,float aimBonus,float composureBonus){float factor=Multiplier(state,raw.mental);return new StatBlock{aim=Value(raw.aim,factor+aimBonus),utility=Value(raw.utility,factor),movement=Value(raw.movement,factor),mental=raw.mental,composure=Value(raw.composure,factor+composureBonus)};}
  static int Value(int value,float scale){return Math.Max(1,Math.Min(100,Mathf.RoundToInt(value*scale)));}
 }
 public partial class Prototype {
  readonly DailyCondition[] dailyCondition=new DailyCondition[10];readonly int[] conditionLift=new int[10];
  readonly float[] talkAim=new float[2],talkComposure=new float[2];readonly bool[] talkUsed=new bool[2];readonly TimeoutTalk[] lastTalk=new TimeoutTalk[2];
  readonly PlayerData[] conditionPlayers=new PlayerData[10];bool conditionReady;
  public string ConditionDay {get;private set;}
  public DailyCondition PlayerCondition(int i){return conditionReady?(DailyCondition)Math.Min(2,(int)dailyCondition[i]+conditionLift[i]):DailyCondition.Normal;}
  public StatBlock EffectiveStats(int i){return conditionReady?ConditionRules.Apply(Data.players[i].stats,PlayerCondition(i),talkAim[teamIndex[i]],talkComposure[teamIndex[i]]):Data.players[i].stats;}
  PlayerData MatchPlayer(int i){if(!conditionReady)return Data.players[i];return conditionPlayers[i];}
  PlayerData[] MatchPlayers(){return conditionReady?conditionPlayers:Data.players;}
  void ResetMapCondition(){
   ConditionDay=Tournament!=null?ConditionRules.TournamentDay(playingSeries):DateTime.Today.ToString("yyyy-MM-dd");
   for(int i=0;i<10;i++)dailyCondition[i]=ConditionRules.Roll(ConditionDay,string.IsNullOrEmpty(Data.players[i].id)?Data.players[i].teamId+"/"+Data.players[i].handle:Data.players[i].id,Tournament!=null?Tournament.seed:0);
   Array.Clear(conditionLift,0,10);Array.Clear(talkAim,0,2);Array.Clear(talkComposure,0,2);Array.Clear(talkUsed,0,2);conditionReady=true;RefreshConditionStats();
  }
  void RefreshConditionStats(){if(Data==null)return;for(int i=0;i<10;i++){var p=Data.players[i];var s=EffectiveStats(i);conditionPlayers[i]=new PlayerData{id=p.id,teamId=p.teamId,handle=p.handle,weaponPosition=p.weaponPosition,riflerRole=p.riflerRole,weapons=p.weapons,stats=s};aimStats[i]=s.aim;composureStats[i]=s.composure;if(combat!=null)combat.SetMovementSkill(i,s.movement);}}
  public bool CanChooseTimeoutTalk {get{return Stage==MatchStage.Timeout&&!FastForwarding&&!ComputerOnlyMatch&&!talkUsed[AlliedTeamIndex];}}
  bool CanLiftTeam(int team){for(int i=0;i<10;i++)if(teamIndex[i]==team&&PlayerCondition(i)!=DailyCondition.High)return true;return false;}
  public bool ChooseTimeoutTalk(TimeoutTalk choice){if(!CanChooseTimeoutTalk||!Enum.IsDefined(typeof(TimeoutTalk),choice)||choice==TimeoutTalk.Confidence&&!CanLiftTeam(AlliedTeamIndex))return false;ApplyTimeoutTalk(AlliedTeamIndex,choice);return true;}
  void ApplyTimeoutTalk(int team,TimeoutTalk choice){if(talkUsed[team])return;talkUsed[team]=true;lastTalk[team]=choice;if(choice==TimeoutTalk.Aim)talkAim[team]+=.05f;else if(choice==TimeoutTalk.Composure)talkComposure[team]+=.05f;else for(int i=0;i<10;i++)if(teamIndex[i]==team&&PlayerCondition(i)!=DailyCondition.High)conditionLift[i]++;RefreshConditionStats();}
  void BeginTimeoutTalks(){Array.Clear(talkUsed,0,2);for(int t=0;t<2;t++)if(ComputerOnlyMatch||t!=AlliedTeamIndex){var choice=CanLiftTeam(t)?TimeoutTalk.Confidence:(CompletedRounds%2==0?TimeoutTalk.Aim:TimeoutTalk.Composure);ApplyTimeoutTalk(t,choice);}}
  string ConditionLabel(int i){var state=PlayerCondition(i);return korean?(state==DailyCondition.High?"상":state==DailyCondition.Low?"하":"중"):state.ToString().ToUpperInvariant();}
  void DrawTimeoutTalk(){
   if(Stage!=MatchStage.Timeout||ComputerOnlyMatch)return;
   HudPanel(new Rect(330,235,690,355),new Color(.025f,.045f,.065f,.98f));
   HudText(new Rect(350,250,650,30),korean?"타임아웃 격려 — 한 가지 선택":"TIMEOUT TALK — CHOOSE ONE",22,HudGold,true);
   if(talkUsed[AlliedTeamIndex]){HudText(new Rect(355,320,635,35),korean?"격려를 전달했습니다. 효과는 이번 맵 동안 유지됩니다.":"Talk delivered. Effects last for this map.",18,HudWhite);HudText(new Rect(355,365,635,30),lastTalk[AlliedTeamIndex]==TimeoutTalk.Aim?(korean?"에임 +5%":"AIM +5%"):lastTalk[AlliedTeamIndex]==TimeoutTalk.Composure?(korean?"침착함 +5%":"COMPOSURE +5%"): (korean?"컨디션 한 단계 상승":"CONDITION +1 TIER"),18,HudBlue);return;}
   HudText(new Rect(355,553,635,22),korean?"이번 맵 동안 유지 · 기본 능력치 기준 · 최대 100":"This map only / bonuses use base stats / stats capped at 100",13,HudMuted);
   string[] ko={"지금까지 잘 해왔잖아. 해온 것처럼만 하면 돼.","너희들이 훨씬 잘해. 충분히 이길 수 있어.","괜찮아. 하고 싶은 플레이를 모두 시도해 봐."};
   string[] en={"Keep doing what you have practiced. You have done well.","You are better than them. You can win this.","It is okay. Try the plays you want to make."};
   string[] effects=korean?new[]{"에임 +5%","컨디션 한 단계 상승 (최대 상)","침착함 +5%"}:new[]{"AIM +5%","CONDITION +1 TIER (MAX HIGH)","COMPOSURE +5%"};
   for(int n=0;n<3;n++){float y=300+n*87;bool enabled=n!=1||CanLiftTeam(AlliedTeamIndex);if(HudButton(new Rect(350,y,650,42),(korean?ko:en)[n],enabled))ChooseTimeoutTalk((TimeoutTalk)n);HudText(new Rect(365,y+44,620,22),effects[n],14,HudBlue);}
  }
 }
}
