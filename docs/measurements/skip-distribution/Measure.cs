using System;using FpsManager;using UnityEngine;
public static class Measure {
 public static void Main(){
  double share=0;int teams=0,half=0,kills=0,awp=0,maxKills=0;
  for(int seed=1;seed<=30;seed++){
   var g=new GameObject("skip distribution").AddComponent<Prototype>();g.Initialize();g.SetRoundSeed(seed*7919);if(!g.StartMatch())throw new Exception("start failed");
   for(int n=0;n<256&&g.Stage!=MatchStage.Finished;n++){
    if(g.Stage==MatchStage.Timeout)g.ResumeTimeout();
    if(!g.SkipRound())throw new Exception("skip rejected "+g.Stage);
    for(int p=0;p<2000&&g.FastForwarding;p++)g.PumpFastForward();
    if(g.FastForwarding)throw new Exception("skip did not finish");
   }
   if(g.Stage!=MatchStage.Finished)throw new Exception("map did not finish");
   for(int team=0;team<2;team++){
    int total=0,top=0;for(int i=0;i<10;i++)if(g.TeamIndexOf(i)==team){int k=g.Statistics.Result(i).kills;total+=k;top=Math.Max(top,k);kills+=k;if(g.Data.players[i].weaponPosition=="awper")awp+=k;maxKills=Math.Max(maxKills,k);}
    if(total>0){double portion=top/(double)total;share+=portion;teams++;if(portion>=.5)half++;}
   }
  }
  Console.WriteLine("SKIP_MAP_MEASURE maps=30 teams="+teams+" avg-top-share="+(100*share/teams).ToString("F1")+"% majority-teams="+half+" awper-share="+(100.0*awp/kills).ToString("F1")+"% total-kills="+kills+" max-player-kills="+maxKills);
 }
}
