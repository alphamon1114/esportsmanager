using UnityEngine;
namespace FpsManager {
 public partial class Prototype {
  public bool ShowVictoryCelebration {get{return Tournament!=null&&!Tournament.victoryCelebrationSeen&&!string.IsNullOrEmpty(Tournament.Champion)&&Tournament.Champion==Tournament.controlledTeam&&Tournament.Champion==AlliedTeamId;}}
  public void DismissVictoryCelebration(){
   if(!ShowVictoryCelebration)return;
   Tournament.victoryCelebrationSeen=true;SaveTournament();OpenMainMenu();
  }
  void VictoryText(Rect area,string text,int size,Color color,bool bold=false){
#if UNITY_5_3_OR_NEWER
   var style=new GUIStyle(GUI.skin.label){fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,alignment=TextAnchor.MiddleCenter,wordWrap=true};
   style.normal.textColor=color;if(menuFont!=null)style.font=menuFont;
   GUI.Label(area,text,style);
#else
   GUI.Label(area,text);
#endif
  }
  void DrawVictoryCelebration(){
   var background=new Color(.025f,.035f,.052f);var card=new Color(.055f,.075f,.105f);
   var gold=new Color(1,.76f,.32f);var shadow=new Color(.69f,.40f,.12f);
   HudPanel(new Rect(0,0,1600,900),background);
   HudPanel(new Rect(300,100,1000,700),card);
   HudPanel(new Rect(300,100,1000,3),gold);
   VictoryText(new Rect(350,135,900,32),"MAJOR CHAMPIONS",18,HudMuted,true);
   // A simple vector-like cup made from UI shapes: no font-dependent emoji
   // or external texture, so it stays sharp at every supported resolution.
   HudPanel(new Rect(688,218,224,76),gold);
   HudPanel(new Rect(701,230,198,48),card);
   HudPanel(new Rect(780,299,40,68),shadow);
   HudPanel(new Rect(789,299,17,68),gold);
   HudPanel(new Rect(751,361,98,14),gold);
   HudPanel(new Rect(731,375,138,19),shadow);
   for(int row=0;row<24;row++){
    float t=row/23f;float width=156-68*t*t;
    HudPanel(new Rect(800-width/2,207+row*4,width,4),gold);
    HudPanel(new Rect(800+width*.22f,207+row*4,width*.28f,4),shadow);
   }
   HudPanel(new Rect(716,201,168,12),new Color(1,.86f,.52f));
   HudPanel(new Rect(737,217,9,48),new Color(1,.88f,.59f));
   VictoryText(new Rect(350,437,900,76),"CONGRATULATIONS!",54,gold,true);
   VictoryText(new Rect(370,535,860,70),TeamName(Tournament.Champion),38,HudWhite,true);
   VictoryText(new Rect(370,617,860,38),korean?"축하합니다! 당신의 팀이 우승했습니다.":"Your team has won the championship.",21,HudMuted);
   var button=new Rect(650,699,300,55);HudPanel(button,gold);
   VictoryText(button,korean?"메인으로":"MAIN MENU",19,background,true);
   if(HudClick(button))DismissVictoryCelebration();
  }
 }
}

