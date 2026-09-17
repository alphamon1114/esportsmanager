using System;
using UnityEngine;
namespace FpsManager
{
 public sealed partial class RoundDirector
 {
  readonly bool[] dangerCleared=new bool[10];
  readonly int[] patrolStep=new int[10];
  readonly Vector2[] patrolCentre=new Vector2[10];
  readonly float[] patrolPause=new float[10];
  DeploymentNavigation tacticalNav;
  int lurker=-1,lurkStep,lurkVariant;
  float lurkStarted;
  public int Lurker { get { return lurker; } }
  public int LurkStep { get { return lurkStep; } }
  void ResetTactics()
  {
   Array.Clear(dangerCleared,0,10);Array.Clear(patrolStep,0,10);Array.Clear(patrolPause,0,10);
   Array.Clear(patrolCentre,0,10);lurker=-1;lurkStep=0;lurkStarted=0;tacticalNav=null;
  }
  public void ConfigureTactics(PlayerData[] players,int[] teams,int ct,DeploymentNavigation navigation)
  {
   tacticalNav=navigation;int best=-1;
   for(int i=0;i<count;i++)
    if(teams[i]!=ct&&i!=Carrier&&players[i].riflerRole=="anchor_lurker"&&(best<0||players[i].stats.composure>players[best].stats.composure))best=i;
   lurker=best;lurkVariant=(int)(random.NextUInt()%2);
  }
  bool LurkObjective(int player,Vector2[] positions,int[] teams,int ct,CombatSystem combat,out PlayerObjective order)
  {
   order=new PlayerObjective();
   if(player!=lurker||player==Carrier||layout.FlankRoutes==null||combat.LivingCount(teams,1-ct)<=2||Clock>55||(PlantedSite>=0&&BombTimer<20))return false;
   var path=layout.FlankRoutes[TargetSite][lurkVariant%layout.FlankRoutes[TargetSite].Length];
   if(lurkStep>=path.Length)return false;
   if(Vector2.Distance(positions[player],path[lurkStep])<settings.interactRadius)
   {
    if(lurkStarted==0)lurkStarted=Clock;
    // Check the angle briefly before extending territory.
    if(Clock-lurkStarted<1.2f&&!combat.Engaging(player)){}
    else {lurkStep++;lurkStarted=0;}
   }
   if(lurkStep>=path.Length)return false;
   order.valid=true;order.task=PlayerTask.Lurk;order.destination=path[lurkStep];
   order.watch=(lurkStep+1<path.Length?path[lurkStep+1]:layout.Sites[TargetSite])-positions[player];
   order.cautious=lurkStep>0;
   return true;
  }
  PlayerObjective Investigate(int player,Vector2 position,Vector2 danger,int ct,int[] teams,bool underThreat)
  {
   if(Vector2.Distance(patrolCentre[player],danger)>1)
   {patrolCentre[player]=danger;patrolStep[player]=0;patrolPause[player]=0;}
   int step=patrolStep[player];
   Vector2 target=danger;
   if(step>0)
   {
    Vector2 direction=step==1?new Vector2(1,0):new Vector2(-1,0);
    for(int turn=0;turn<4;turn++)
    {
     var candidate=danger+direction*6;
     if(tacticalNav==null||tacticalNav.Clear(danger,candidate)){target=candidate;break;}
     direction=new Vector2(-direction.y,direction.x);
    }
   }
   if(underThreat)patrolPause[player]=0;
   if(!underThreat&&Vector2.Distance(position,target)<settings.interactRadius)
   {
    if(patrolPause[player]==0)patrolPause[player]=Clock;
    if(Clock-patrolPause[player]>1.5f)
    {
     patrolStep[player]++;patrolPause[player]=0;
     if(patrolStep[player]>2)
      for(int i=0;i<count;i++)if(teams[i]==ct&&lossClock[i]>=0&&Vector2.Distance(lossPosition[i],danger)<=14)dangerCleared[i]=true;
    }
   }
   return new PlayerObjective{valid=true,cautious=true,task=step==0?PlayerTask.Rotate:PlayerTask.Patrol,destination=target,watch=danger-position};
  }
 }
}