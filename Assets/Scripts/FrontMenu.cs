using System;
using System.Collections.Generic;
using UnityEngine;
namespace FpsManager {
 public partial class Prototype {
  int menuPage; // 0 match, 1 home, 2 options, 3 team selection
  bool korean;string menuTeam="spirit",menuMessage="";
#if UNITY_5_3_OR_NEWER
  Font menuFont;
#endif
  public void OpenMainMenu(){
   menuPage=1;menuMessage="";LoadResolution();
#if UNITY_5_3_OR_NEWER
   SetLanguage(PlayerPrefs.GetInt("fps.language",1)==1);
#endif
  }
  public void OpenTeamSelection(){tournamentRoster=TournamentRoster.Load();menuTeam=AlliedTeamId;menuPage=3;menuMessage="";}
  public void SetLanguage(bool value){korean=value;
#if UNITY_5_3_OR_NEWER
   if(menuFont==null)menuFont=Resources.Load<Font>("Fonts/NanumGothic-Regular");
#if !UNITY_WEBGL || UNITY_EDITOR
   if(menuFont==null)menuFont=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR","Arial Unicode MS"},18);
#endif
   PlayerPrefs.SetInt("fps.language",value?1:0);PlayerPrefs.Save();
#endif
  }
  public static float PlayerOverall(PlayerData p){return(p.stats.aim+p.stats.utility+p.stats.movement+p.stats.mental+p.stats.composure)/5f;}
  public static float TeamOverall(Database db,string team){float sum=0;int count=0;foreach(var p in db.players)if(p.teamId==team){sum+=PlayerOverall(p);count++;}return count==0?0:sum/count;}
  static readonly Dictionary<string,string> translations=new Dictionary<string,string>{
   {"1F","1층"},{"2F","2층"},{"RESOLUTION","해상도"},{"Editor: Game view size is set separately.","에디터에서는 Game 창 크기를 별도로 설정합니다."},{"PAUSED","일시정지"},{"RESUME GAME","게임 계속하기"},{"QUIT GAME","게임 종료"},{"ESC: resume / back","ESC: 계속하기 / 뒤로"},{"Quit the game?","게임을 종료할까요?"},{"Completed tournament maps are saved.","완료한 대회 맵 결과는 저장됩니다."},{"Progress in the current map is not saved.","진행 중인 맵의 플레이는 저장되지 않습니다."},
   {"Watch or skip this match.","경기를 관전하거나 건너뛸 수 있습니다."},{"SKIP MATCH","경기 건너뛰기"},{"SKIP ROUND","라운드 건너뛰기"},{"RETURN TO LIVE","관전으로 돌아가기"},{"QUICK SIM / DEFAULT TACTICS","빠른 계산 / 디폴트 전술"},{"SIMULATING...","경기 계산 중..."},{"Choose tactics, then start the map.","전략을 정한 뒤 경기를 시작하세요."},{"Skip paused for timeout.","타임아웃으로 건너뛰기를 멈췄습니다."},{"Simulation stopped.","경기 계산을 멈췄습니다."},{"Simulation limit reached. Continue watching or skip again.","계산 한도에 도달했습니다. 관전하거나 다시 건너뛸 수 있습니다."},{"SELECT TEAM","팀 선택"},{"OPTIONS","옵션"},{"BACK","뒤로"},{"LANGUAGE","언어"},{"LOAD TOURNAMENT","대회 이어하기"},{"NEW TOURNAMENT","새 대회"},{"PLAY NEXT MAP","다음 맵 시작"},{"TOURNAMENT BRACKET","대진표"},{"START MATCH","경기 시작"},{"START NEW MAP","새 맵 시작"},
   {"RANDOM DRAW / START","무작위 대진 생성"},{"CHOOSE YOUR TEAM","운영할 팀 선택"},{"ROSTER","선수 명단"},{"OVERALL","종합점수"},{"AIM / UTIL / MOVE / MENT / COMP","에임 / 유틸 / 무빙 / 멘탈 / 침착함"},{"Overall = average of five attributes, averaged across five players.","종합점수 = 선수별 5개 능력치 평균을 팀원 5명에 대해 평균 (100점 만점)"},
   {"Your team. Other matches: AI vs AI. Saved after each map.","선택한 팀을 운영합니다. 나머지 경기는 AI끼리 진행하며 맵마다 저장합니다."},
   {"TIMEOUT QUEUED","타임아웃 예약됨"},{"AI TIMEOUT","AI 타임아웃"},{"OPPONENT TIMEOUT","상대 타임아웃"},{"RESUME / BUY","재개 / 구매"},{"FORWARD","전진"},{"DEFAULT","디폴트"},{"STACK","스택"},{"RUSH","러쉬"},{"MID PLAY","미드플레이"},{"MAP FLOOR: LOWER","지도: 아래층"},{"MAP FLOOR: UPPER","지도: 위층"},{"MAP BAN / BO3","맵 밴 / BO3"},{"Ban one map. Opponent bans one. Remaining maps play in random order.","각 팀 1개 밴 후 남은 맵은 무작위 순서로 진행합니다."},{"MAP: TEAM INTEL","지도: 아군 정보"},{"MAP: ALL PLAYERS","지도: 전체 선수"},{"DEBUG: ON","디버그: 켜짐"},{"DEBUG: OFF","디버그: 꺼짐"},{"BOTH","전체"},
   {"COACH DESK","감독석"},{"MATCH CONTROL","경기 조작"},{"CT / DEFENSE","CT / 수비 전술"},{"T / ATTACK","T / 공격 전술"},{"OBSERVER","관전"},{"Strategy changes available","전술 변경 가능"},{"Locked until next buy phase","다음 구매시간에 변경 가능"},{"Timeout begins between rounds.","타임아웃은 라운드 사이에 시작됩니다."},{"Opponent coach is changing tactics.","상대 감독이 전술을 변경 중입니다."},{"Click a player card to change POV","선수 카드를 누르면 해당 선수 시점으로 전환됩니다."},
   {"No saved tournament found.","불러올 수 있는 대회 저장이 없습니다."},{"Creates a new tournament and replaces the previous tournament save.","새 대회를 만들면 이전 대회 저장을 교체합니다."}
  };
  public string Ui(string text){if(!korean)return text;string translated;if(translations.TryGetValue(text,out translated))return translated;if(text.StartsWith("TIMEOUT  ("))return "타임아웃  ("+text.Substring(10);return text;}
  void DrawFrontMenu(){
   HudText(new Rect(80,55,1400,60),"ESPORTS MANAGER",42,HudWhite,true);
   HudText(new Rect(82,122,1300,30),"MAJOR PLAYOFFS / 8 TEAMS",18,HudMuted);
   if(menuPage==1){
    if(HudButton(new Rect(80,290,430,64),"SELECT TEAM"))OpenTeamSelection();
    if(HudButton(new Rect(80,374,430,64),"OPTIONS"))menuPage=2;
    if(HudButton(new Rect(80,458,430,64),"LOAD TOURNAMENT")&&!LoadTournament())menuMessage="No saved tournament found.";
    HudText(new Rect(80,560,1200,30),menuMessage,18,HudGold);return;
   }
   if(menuPage==2){
    HudText(new Rect(80,245,900,40),"LANGUAGE",26,HudWhite,true);
    if(HudButton(new Rect(80,315,310,64),"한국어",true,korean))SetLanguage(true);
    if(HudButton(new Rect(410,315,310,64),"English",true,!korean))SetLanguage(false);
    DrawResolutionOptions(80,440,640);
   }else{
    HudText(new Rect(80,190,1300,40),"CHOOSE YOUR TEAM",26,HudWhite,true);
    for(int i=0;i<8;i++){var t=tournamentRoster.teams[i];if(HudButton(new Rect(80,250+i*61,350,51),t.name,true,menuTeam==t.id))menuTeam=t.id;}
    HudPanel(new Rect(475,248,1030,480),new Color(.08f,.12f,.18f));
    HudText(new Rect(500,265,750,42),TeamName(menuTeam),28,HudWhite,true);
    HudText(new Rect(1180,272,310,34),Ui("OVERALL")+"  "+TeamOverall(tournamentRoster,menuTeam).ToString("F1"),23,HudGold,true);
    HudText(new Rect(500,330,310,30),"ROSTER",17,HudMuted);
    HudText(new Rect(820,330,680,30),"AIM / UTIL / MOVE / MENT / COMP",17,HudMuted);
    int row=0;foreach(var p in tournamentRoster.players)if(p.teamId==menuTeam){var s=p.stats;float y=383+row++*58;HudText(new Rect(500,y,300,34),p.handle,23,HudWhite,true);HudText(new Rect(825,y,660,34),s.aim+" / "+s.utility+" / "+s.movement+" / "+s.mental+" / "+s.composure+"     | "+PlayerOverall(p).ToString("F1"),21,HudWhite);}
    HudText(new Rect(480,745,1040,28),"Overall = average of five attributes, averaged across five players.",15,HudMuted);
    HudText(new Rect(480,780,1040,28),"Creates a new tournament and replaces the previous tournament save.",15,HudMuted);
    if(HudButton(new Rect(1150,830,355,48),"RANDOM DRAW / START"))StartTournament(menuTeam,Guid.NewGuid().GetHashCode(),true);
   }
   if(HudButton(new Rect(80,830,250,48),"BACK"))menuPage=1;
  }
 }
}



