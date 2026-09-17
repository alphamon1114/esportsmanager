using System;
using UnityEngine;
namespace FpsManager
{
 public partial class Prototype
 {
  static readonly Color HudWhite=new Color(.92f,.95f,1),HudMuted=new Color(.55f,.64f,.72f);
  static readonly Color HudBlue=new Color(.21f,.68f,1),HudGold=new Color(1,.69f,.25f);
#if UNITY_5_3_OR_NEWER
  readonly System.Collections.Generic.Dictionary<int,GUIStyle> hudStyles=new System.Collections.Generic.Dictionary<int,GUIStyle>();
#endif
  void HudPanel(Rect rect,Color color)
  {
   var previous=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture,ScaleMode.StretchToFill);GUI.color=previous;
  }
  void HudText(Rect rect,string text,int size,Color color,bool bold=false)
  {
#if UNITY_5_3_OR_NEWER
   int key=size*2+(bold?1:0);GUIStyle style;
   if(!hudStyles.TryGetValue(key,out style))
   {style=new GUIStyle(GUI.skin.label){fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,wordWrap=false};hudStyles.Add(key,style);}
   style.normal.textColor=color;GUI.Label(rect,text,style);
#else
   GUI.Label(rect,text);
#endif
  }
  bool HudClick(Rect area)
  {
#if UNITY_5_3_OR_NEWER
   return GUI.Button(area,"",GUIStyle.none);
#else
   return GUI.Button(area,"");
#endif
  }
  bool HudButton(Rect area,string title,bool enabled=true,bool active=false)
  {
   HudPanel(area,active?new Color(.16f,.38f,.51f,.95f):new Color(.13f,.18f,.24f,enabled?.95f:.5f));
   HudText(new Rect(area.x+12,area.y+9,area.width-16,28),title,15,enabled?HudWhite:HudMuted,true);
   return enabled&&HudClick(area);
  }
  string WeaponLabel(int player) { return combat.WeaponFor(player).id.Replace('_',' ').ToUpperInvariant(); }
  string AmmoLabel(int player)
  {
   return combat.Magazine(player)+"/"+combat.WeaponFor(player).magazineSize+"   MAGS "+combat.SpareMagazines(player)+(combat.Reloading(player)?"  RELOAD":"");
  }
  string PhaseLabel()
  {
   if(!AutomaticMatch)return "READY TO START";
   if(Stage==MatchStage.Live)return director.PlantedSite>=0?"BOMB  "+director.BombTimer.ToString("F0")+"s":"LIVE  "+Mathf.Max(0,roundSettings.roundSeconds-director.Clock).ToString("F0")+"s";
   if(Stage==MatchStage.Result)return Data.teams[LastRoundWinner].name.ToUpperInvariant()+" WINS  /  "+StageSeconds.ToString("F1")+"s";
   if(Stage==MatchStage.Finished)return "MAP WINNER: "+Data.teams[LastRoundWinner].name.ToUpperInvariant();
   return Stage.ToString().ToUpperInvariant()+"  "+StageSeconds.ToString("F1")+"s";
  }
  void DrawBroadcastHud()
  {
   var matrix=GUI.matrix;
   GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1600f,Screen.height/900f,1));
   HudPanel(new Rect(0,0,1600,900),new Color(.025f,.035f,.052f));
   if(Stage==MatchStage.Finished){DrawMatchResults();GUI.matrix=matrix;return;}
   var viewport=new Rect(16,80,1316,740);
   GUI.DrawTexture(viewport,eyeTexture,ScaleMode.StretchToFill);DrawUtilityPov(viewport);
   HudText(new Rect(660,435,24,32),"+",24,HudWhite);
   HudText(new Rect(22,15,350,30),"ESPORTS MANAGER",20,HudWhite,true);
   HudText(new Rect(22,44,350,22),"INFERNO PROTOTYPE  /  LIVE OBSERVER",12,HudMuted);
   HudPanel(new Rect(430,10,505,59),new Color(.09f,.13f,.19f));
   HudText(new Rect(449,17,180,25),Data.teams[ctTeam].name,17,HudBlue,true);
   HudText(new Rect(636,12,125,45),roundWins[ctTeam]+" : "+roundWins[1-ctTeam],30,HudWhite,true);
   HudText(new Rect(753,17,180,25),Data.teams[1-ctTeam].name,17,HudGold,true);
   HudText(new Rect(560,44,380,22),"CT     ROUND "+(Stage==MatchStage.Result||Stage==MatchStage.Finished?CompletedRounds:CompletedRounds+1)+"  /  FIRST TO 9, WIN BY 2     T",11,HudMuted);
   HudPanel(new Rect(450,82,468,36),new Color(.025f,.04f,.06f,.9f));
   HudText(new Rect(465,89,445,28),PhaseLabel(),16,HudWhite,true);
   DrawBroadcastMap(new Rect(30,94,270,270));
   DrawTeamCards(ctTeam,30,HudBlue);DrawTeamCards(1-ctTeam,1092,HudGold);
   for(int row=0;row<killFeed.Count;row++)
   {
    var entry=killFeed[killFeed.Count-1-row];var area=new Rect(923,126+row*29,390,26);
    HudPanel(area,new Color(.025f,.04f,.06f,.8f));
    HudText(new Rect(area.x+8,area.y+3,380,24),Data.players[entry.killer].handle+"  ["+entry.weapon.Replace('_',' ').ToUpperInvariant()+"]  "+(entry.region==HitRegion.Head?"HS ":"")+Data.players[entry.victim].handle,12,HudWhite);
   }
   var accent=teamIndex[selected]==ctTeam?HudBlue:HudGold;
   HudPanel(new Rect(430,744,498,62),new Color(.03f,.05f,.075f,.9f));
   HudPanel(new Rect(430,744,4,62),accent);
   HudText(new Rect(444,752,245,26),Data.players[selected].handle.ToUpperInvariant(),21,accent,true);
   HudText(new Rect(702,752,210,25),"HP "+Mathf.RoundToInt(combat.Health(selected))+"  |  $"+matchState[selected].credits,17,HudWhite);
   HudText(new Rect(444,781,480,24),WeaponLabel(selected)+"   "+AmmoLabel(selected)+"   ARMOR "+Mathf.RoundToInt(matchState[selected].armor)+(matchState[selected].helmet?" + HELMET":""),13,HudWhite);
   HudText(new Rect(290,827,1040,24),autonomy!=null&&autonomy.Radio!=null?"RADIO  "+autonomy.Radio:"Click a player card to change POV",14,HudMuted);
   string history="ROUND HISTORY   ";for(int i=Math.Max(0,roundWinners.Count-16);i<roundWinners.Count;i++)history+=(i+1)+":"+(roundWinners[i]==AlliedTeamIndex?"W":"L")+"  ";
   HudText(new Rect(30,863,1280,23),history,13,HudMuted);
   DrawCoachPanel();
   if(showDebugInfo)HudText(new Rect(290,852,1010,22),"DEBUG  seed "+roundSeed+" / "+director.Objective(selected).task+" / target "+director.SiteName(director.TargetSite),12,HudGold);
   GUI.matrix=matrix;
  }
  void DrawTeamCards(int team,float x,Color accent)
  {
   HudPanel(new Rect(x,371,224,29),new Color(.025f,.04f,.06f,.9f));
   HudText(new Rect(x+10,377,210,25),Data.teams[team].name.ToUpperInvariant()+" / "+(team==ctTeam?"CT":"T"),13,accent,true);
   int row=0;
   for(int i=0;i<10;i++)if(teamIndex[i]==team)
   {
    bool alive=combat.Alive(i);float y=406+row++*78;var area=new Rect(x,y,224,72);
    HudPanel(area,new Color(.035f,.055f,.085f,alive?.83f:.6f));
    if(i==selected)HudPanel(new Rect(x,y,4,72),accent);
    string captain=Data.teams[team].iglPlayerId==Data.players[i].id?" *":"";
    HudText(new Rect(x+10,y+5,162,22),Data.players[i].handle+captain,16,alive?HudWhite:HudMuted,true);
    HudText(new Rect(x+173,y+5,48,22),alive?Mathf.RoundToInt(combat.Health(i)).ToString():"OUT",15,alive?accent:HudMuted,true);
    HudText(new Rect(x+10,y+28,211,19),WeaponLabel(i)+"   $"+matchState[i].credits,11,HudWhite);
    bool flash=Array.IndexOf(matchState[i].equipment,"flash")>=0,smoke=Array.IndexOf(matchState[i].equipment,"smoke")>=0;
    HudText(new Rect(x+10,y+48,211,20),"AR "+Mathf.RoundToInt(matchState[i].armor)+(matchState[i].helmet?" H ":" ")+AmmoLabel(i)+" "+(flash?"F"+MatchEconomy.UtilityCount(matchState[i],"flash")+" ":"")+(smoke?"S"+MatchEconomy.UtilityCount(matchState[i],"smoke"):""),10,HudMuted);
    HudPanel(new Rect(x,y+69,224*Mathf.Clamp01(combat.Health(i)/100),3),alive?accent:HudMuted);
    if(HudClick(area))Select(i);
   }
  }
  void DrawBroadcastMap(Rect map)
  {
   HudPanel(new Rect(map.x-3,map.y-3,map.width+6,map.height+6),new Color(.04f,.07f,.1f,.9f));
   GUI.DrawTexture(map,mapTexture,ScaleMode.StretchToFill);DrawUtilityMap(map);
   for(int i=0;i<10;i++)
   {
    bool own=teamIndex[i]==AlliedTeamIndex;var contact=vision.Knowledge(AlliedTeamIndex,i);
    if(!combat.Alive(i)||(!own&&fogOfWar&&!contact.known))continue;
    Vector2 shown=own||!fogOfWar?MapPosition(i):contact.lastKnownPosition;
    var point=mapCamera.WorldToViewportPoint(World(shown));
    var area=new Rect(map.x+point.x*map.width-9,map.y+(1-point.y)*map.height-9,18,18);
    HudPanel(area,teamIndex[i]==ctTeam?HudBlue:HudGold);
    HudText(new Rect(area.x+3,area.y,20,20),(i+1).ToString(),11,new Color(.02f,.03f,.04f),true);
    if(!fogOfWar&&PlayerHeight(i)>.4f)HudText(new Rect(area.x+17,area.y-6,55,18),"+"+PlayerHeight(i).ToString("F1"),10,HudWhite,true);
    if(HudClick(area))Select(i);
   }
  }
  void DrawCoachPanel()
  {
   HudPanel(new Rect(1350,16,234,868),new Color(.055f,.08f,.115f));
   HudText(new Rect(1368,36,210,28),"COACH DESK",20,HudWhite,true);
   HudText(new Rect(1368,70,210,26),"TEAM SPIRIT",14,HudBlue,true);
   HudText(new Rect(1368,110,210,25),"MATCH CONTROL",12,HudMuted);
   if(!AutomaticMatch||Stage==MatchStage.Finished)
    if(HudButton(new Rect(1366,143,202,46),Stage==MatchStage.Finished?"START NEW MAP":"START MATCH"))StartMatch();
   if(AutomaticMatch&&Stage!=MatchStage.Finished)
   {
    if(HudButton(new Rect(1366,143,202,46),TimeoutPending?"TIMEOUT QUEUED":"TIMEOUT  ("+TimeoutsRemaining+"/2)",TimeoutsRemaining>0&&!TimeoutPending&&Stage!=MatchStage.Timeout))RequestTimeout();
    if(Stage==MatchStage.Timeout&&HudButton(new Rect(1366,199,202,42),"RESUME / BUY"))ResumeTimeout();
   }
   HudText(new Rect(1368,255,210,25),"OUR TEAM STRATEGY",12,HudMuted);
   if(HudButton(new Rect(1366,289,202,44),"AGGRESSIVE",CanChangeStrategy,Strategy==TeamStrategy.Aggressive))ChangeStrategy(TeamStrategy.Aggressive);
   if(HudButton(new Rect(1366,343,202,44),"BALANCED",CanChangeStrategy,Strategy==TeamStrategy.Balanced))ChangeStrategy(TeamStrategy.Balanced);
   if(HudButton(new Rect(1366,397,202,44),"DEFENSIVE",CanChangeStrategy,Strategy==TeamStrategy.Defensive))ChangeStrategy(TeamStrategy.Defensive);
   HudText(new Rect(1368,455,212,23),CanChangeStrategy?"Strategy changes available":"Locked / call a timeout",12,CanChangeStrategy?HudBlue:HudMuted);
   HudText(new Rect(1368,492,215,22),"Timeout begins between rounds.",11,HudMuted);
   HudText(new Rect(1368,513,215,22),"BUY PLAN: "+TeamBuyPlan(AlliedTeamIndex).ToString().ToUpperInvariant(),11,HudMuted);
   HudText(new Rect(1368,554,210,24),"OBSERVER",12,HudMuted);
   if(HudButton(new Rect(1366,589,202,44),fogOfWar?"MAP: TEAM INTEL":"MAP: ALL PLAYERS"))fogOfWar=!fogOfWar;
   if(HudButton(new Rect(1366,643,202,44),showDebugInfo?"DEBUG: ON":"DEBUG: OFF"))showDebugInfo=!showDebugInfo;
   HudText(new Rect(1368,724,210,22),"F = FLASH  /  S = SMOKE",11,HudMuted);
   HudText(new Rect(1368,746,210,22),"MAGS = SPARE MAGAZINES",11,HudMuted);
   HudText(new Rect(1368,768,210,22),"AR = ARMOR / H = HELMET",11,HudMuted);
   if(preparationError!=null)HudText(new Rect(1368,810,210,55),preparationError,11,HudGold);
  }
 }
}
