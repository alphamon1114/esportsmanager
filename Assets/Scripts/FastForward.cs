using System;
using System.Diagnostics;
namespace FpsManager {
 public partial class Prototype {
  public bool AwaitingMapStart {get;private set;}
  public bool FastForwarding {get{return skipMode!=0;}}
  int skipMode,skipRound,skipSeries=-1,skipSteps; // 1 round, 2 other team's series
  public string SkipMessage {get;private set;}
  public bool ConfirmMapStart(){if(!AwaitingMapStart)return false;AwaitingMapStart=false;StartBuying();return true;}
  public bool SkipRound(){
   if(FastForwarding||AwaitingMapStart||!AutomaticMatch||ComputerOnlyMatch||Stage!=MatchStage.Live&&Stage!=MatchStage.Buying&&Stage!=MatchStage.Result)return false;
   skipRound=CompletedRounds+(Stage==MatchStage.Result?0:1);skipMode=1;skipSteps=0;SkipMessage=null;return true;
  }
  public bool SkipOtherMatch(){
   if(FastForwarding||Tournament==null)return false;
   if(tournamentBoard){int next=Tournament.Next;if(next<0)return false;var s=Tournament.series[next];if(s.teamA==AlliedTeamId||s.teamB==AlliedTeamId)return false;quickSeriesSetup=true;bool started;try{started=PlayTournamentMap();}finally{quickSeriesSetup=false;}if(!started)return false;}
   if(!ComputerOnlyMatch||playingSeries<0)return false;
   ConfirmMapStart();skipSeries=playingSeries;skipMode=2;skipSteps=0;SkipMessage=null;return true;
  }
  public void CancelFastForward(){skipMode=0;SkipMessage=null;ResumeWatchGeometry();}
  public void PumpFastForward(int maxSteps=200){
   if(!FastForwarding)return;var watch=Stopwatch.StartNew();
   for(int step=0;step<maxSteps&&watch.ElapsedMilliseconds<12&&FastForwarding;step++){
    // Never automatically consume the human coach's tactical window.
    if(skipMode==1&&Stage==MatchStage.Timeout){skipMode=0;SkipMessage="Skip paused for timeout.";break;}
    if(skipMode==1&&CompletedRounds>=skipRound&&(Stage==MatchStage.Buying||Stage==MatchStage.Finished)){skipMode=0;break;}
    if(skipMode==2&&Stage==MatchStage.Finished){
     RecordTournamentMap();var series=Tournament.series[skipSeries];
     if(!string.IsNullOrEmpty(series.winner)){skipMode=0;tournamentBoard=true;break;}
     tournamentBoard=true;if(!PlayTournamentMap()){skipMode=0;SkipMessage="Simulation stopped.";break;}ConfirmMapStart();
    }
    AdvanceStatSkip();RecordTournamentMap();skipSteps++;
    if(skipSteps>=4096){skipMode=0;SkipMessage="Simulation limit reached. Continue watching or skip again.";}
   }
  }
  void DrawMatchControls(){
   if(AwaitingMapStart){
    HudPanel(new UnityEngine.Rect(456,342,440,142),new UnityEngine.Color(.03f,.05f,.08f,.96f));
    HudText(new UnityEngine.Rect(474,358,412,30),ComputerOnlyMatch?"Watch or skip this match.":"Choose tactics, then start the map.",17,HudWhite);
    if(HudButton(new UnityEngine.Rect(512,410,328,52),"START MATCH"))ConfirmMapStart();
   }
   if(FastForwarding){
    HudPanel(new UnityEngine.Rect(450,310,500,172),new UnityEngine.Color(.03f,.05f,.08f,.96f));
    HudText(new UnityEngine.Rect(470,326,465,30),"QUICK SIM / DEFAULT TACTICS",22,HudGold,true);
    HudText(new UnityEngine.Rect(470,363,465,26),roundWins[0]+" : "+roundWins[1]+" / ROUND "+(CompletedRounds+1),18,HudWhite);
    if(HudButton(new UnityEngine.Rect(510,415,360,43),"RETURN TO LIVE"))CancelFastForward();
   }else if(AutomaticMatch&&!AwaitingMapStart&&Stage!=MatchStage.Finished){
    bool can=Stage!=MatchStage.Timeout;
    if(HudButton(new UnityEngine.Rect(1366,695,202,34),ComputerOnlyMatch?"SKIP MATCH":"SKIP ROUND",can)){if(ComputerOnlyMatch)SkipOtherMatch();else SkipRound();}
   }
   if(SkipMessage!=null)HudText(new UnityEngine.Rect(360,300,920,28),SkipMessage,17,HudGold);
  }
 }
}

