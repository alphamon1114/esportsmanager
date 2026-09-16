using System.Collections.Generic;
using UnityEngine;
namespace FpsManager
{
 public partial class Prototype
 {
  readonly List<KillEvent> killFeed=new List<KillEvent>();
  readonly List<int> roundWinners=new List<int>();
  readonly int[] roundWins=new int[2];
  public int LastRoundWinner { get; private set; } = -1;
  public int RoundWins(int team) { return roundWins[team]; }
  public int KillFeedCount { get { return killFeed.Count; } }
  public KillEvent KillFeedAt(int index) { return killFeed[index]; }
  void RecordKill(KillEvent entry)
  {
   killFeed.Add(entry); if(killFeed.Count>6) killFeed.RemoveAt(0);
  }
  void RecordRoundResult(RoundOutcome outcome)
  {
   if(outcome==RoundOutcome.None) return;
   bool ctWon=outcome==RoundOutcome.BombDefused||outcome==RoundOutcome.TerroristsEliminated||outcome==RoundOutcome.TimeExpired;
   LastRoundWinner=ctWon?ctTeam:1-ctTeam;
   roundWins[LastRoundWinner]++; roundWinners.Add(LastRoundWinner);
  }
  void DrawMatchScore()
  {
   GUI.Label(new Rect(20,10,1235,27),Data.teams[0].name+" ["+(ctTeam==0?"CT":"T")+"]   "+roundWins[0]+" : "+roundWins[1]+"   "+Data.teams[1].name+" ["+(ctTeam==1?"CT":"T")+"]    ROUND "+(CompletedRounds+1));
   string history="SPIRIT ROUND RESULTS  ";
   int start=System.Math.Max(0,roundWinners.Count-12);
   for(int i=start;i<roundWinners.Count;i++) history+="R"+(i+1)+":"+(roundWinners[i]==AlliedTeamIndex?"W":"L")+"  ";
   if(LastRoundWinner>=0) history+=" | Last winner: "+Data.teams[LastRoundWinner].name;
   GUI.Label(new Rect(20,61,1240,18),history);
  }
  void DrawKillFeed()
  {
   Color old=GUI.color;
   for(int row=0;row<killFeed.Count;row++)
   {
    var entry=killFeed[killFeed.Count-1-row];
    var area=new Rect(810,110+row*25,438,24);
    GUI.color=new Color(.03f,.04f,.06f,.85f); GUI.DrawTexture(area,Texture2D.whiteTexture,ScaleMode.StretchToFill);
    GUI.color=new Color(1,1,1,1);
    string weaponName=entry.weapon.Replace('_',' ').ToUpperInvariant();
    GUI.Label(new Rect(area.x+8,area.y,area.width-12,area.height),Data.players[entry.killer].handle+"   ["+weaponName+"] "+(entry.region==HitRegion.Head?"[HS] ":"")+"  "+Data.players[entry.victim].handle);
   }
   GUI.color=old;
  }
 }
}