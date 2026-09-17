using System;
namespace FpsManager
{
 public sealed class PlayerResult
 {
  public int rounds,kills,deaths,assists,headshots,kast;public float damage;
  public float Adr {get{return rounds==0?0:damage/rounds;}}
  public float Kast {get{return rounds==0?0:100f*kast/rounds;}}
  public float HeadshotPercent {get{return kills==0?0:100f*headshots/kills;}}
  public float Rating {get{return rounds==0?0:Math.Max(0,.35f*(kills/(float)rounds)/.7f+.35f*Adr/80f+.3f*(Kast/100)/.7f+.15f*(.7f-deaths/(float)rounds));}}
 }
 public sealed class MatchStatistics
 {
  readonly PlayerResult[,] totals=new PlayerResult[3,10];
  readonly float[,] damage=new float[10,10];
  readonly bool[] contributed=new bool[10],dead=new bool[10];
  readonly int[] killers=new int[10],teams=new int[10];readonly float[] deathAt=new float[10];
  int ct;bool active;
  public MatchStatistics(){for(int side=0;side<3;side++)for(int i=0;i<10;i++)totals[side,i]=new PlayerResult();}
  public PlayerResult Result(int i,int side=0){return totals[side,i];}
  void Each(int i,Action<PlayerResult> action){action(totals[0,i]);action(totals[teams[i]==ct?1:2,i]);}
  public void Begin(int[] team,int counterTerrorists)
  {
   Array.Copy(team,teams,10);ct=counterTerrorists;active=true;Array.Clear(damage,0,damage.Length);Array.Clear(contributed,0,10);Array.Clear(dead,0,10);
   for(int i=0;i<10;i++){killers[i]=-1;deathAt[i]=-100;}
  }
  public void Damage(int shooter,int target,float healthLost)
  {
   if(!active||teams[shooter]==teams[target]||healthLost<=0)return;
   damage[shooter,target]+=healthLost;Each(shooter,s=>s.damage+=healthLost);
  }
  public void Kill(KillEvent e,float clock)
  {
   if(!active||dead[e.victim])return;dead[e.victim]=true;killers[e.victim]=e.killer;deathAt[e.victim]=clock;Each(e.victim,s=>s.deaths++);
   if(teams[e.killer]==teams[e.victim])return;
   Each(e.killer,s=>{s.kills++;if(e.region==HitRegion.Head)s.headshots++;});contributed[e.killer]=true;
   for(int i=0;i<10;i++)
   {
    if(i!=e.killer&&teams[i]==teams[e.killer]&&damage[i,e.victim]>=50){Each(i,s=>s.assists++);contributed[i]=true;}
    if(dead[i]&&teams[i]==teams[e.killer]&&killers[i]==e.victim&&clock-deathAt[i]<=5)contributed[i]=true;
   }
  }
  public void End(CombatSystem combat)
  {
   if(!active)return;active=false;
   for(int i=0;i<10;i++){bool success=contributed[i]||(!dead[i]&&combat.Alive(i));Each(i,s=>{s.rounds++;if(success)s.kast++;});}
  }
 }
 public partial class Prototype
 {
  public MatchStatistics Statistics {get;private set;}=new MatchStatistics();
  int resultSide;
  void DrawMatchResults()
  {
   HudPanel(new UnityEngine.Rect(0,0,1600,900),new UnityEngine.Color(.035f,.05f,.075f));
   HudText(new UnityEngine.Rect(90,35,1000,42),"MAP COMPLETE  /  INFERNO PROTOTYPE",28,HudWhite,true);
   HudText(new UnityEngine.Rect(90,88,1300,42),Data.teams[0].name+"   "+roundWins[0]+" : "+roundWins[1]+"   "+Data.teams[1].name,30,HudWhite,true);
   HudText(new UnityEngine.Rect(90,140,1000,28),"WINNER  "+Data.teams[LastRoundWinner].name.ToUpperInvariant()+"    /    "+CompletedRounds+" ROUNDS",17,HudGold,true);
   string[] tabs={"BOTH","CT","T"};for(int side=0;side<3;side++)if(HudButton(new UnityEngine.Rect(1160+side*105,128,95,42),tabs[side],true,resultSide==side))resultSide=side;
   for(int team=0;team<2;team++)
   {
    float top=195+team*278;var color=team==0?HudBlue:HudGold;
    HudPanel(new UnityEngine.Rect(90,top,1420,40),new UnityEngine.Color(.10f,.14f,.20f));
    HudText(new UnityEngine.Rect(110,top+7,500,30),Data.teams[team].name.ToUpperInvariant(),20,color,true);
    string[] headers={"K - D","A","HS %","ADR","KAST","RATING*"};float[] xs={660,815,920,1060,1200,1355};
    for(int c=0;c<headers.Length;c++)HudText(new UnityEngine.Rect(xs[c],top+9,145,28),headers[c],16,HudWhite,true);
    var order=new System.Collections.Generic.List<int>();for(int i=0;i<10;i++)if(teamIndex[i]==team)order.Add(i);
    order.Sort((a,b)=>{int n=Statistics.Result(b,resultSide).Rating.CompareTo(Statistics.Result(a,resultSide).Rating);return n!=0?n:a.CompareTo(b);});
    for(int row=0;row<order.Count;row++)
    {
     int i=order[row];var s=Statistics.Result(i,resultSide);float y=top+42+row*42;
     HudPanel(new UnityEngine.Rect(90,y,1420,40),row%2==0?new UnityEngine.Color(.07f,.09f,.13f):new UnityEngine.Color(.055f,.075f,.11f));
     HudText(new UnityEngine.Rect(110,y+7,520,28),Data.players[i].handle,19,HudWhite,true);
     string[] values={s.kills+" - "+s.deaths,s.assists.ToString(),s.HeadshotPercent.ToString("F1")+"%",s.Adr.ToString("F1"),s.Kast.ToString("F1")+"%",s.Rating.ToString("F2")};
     for(int c=0;c<values.Length;c++)HudText(new UnityEngine.Rect(xs[c],y+8,145,28),s.rounds==0?"--":values[c],17,c==5?(s.Rating>=1?HudBlue:HudGold):HudWhite,c==5);
    }
   }
   HudText(new UnityEngine.Rect(90,765,1400,25),"ADR = enemy HP damage / rounds   |   KAST = kill, damage assist (50+), survive, or traded within 5s",14,HudMuted);
   HudText(new UnityEngine.Rect(90,792,1400,25),"* Prototype rating: kills, ADR, KAST and deaths. Not HLTV Rating 3.0. No flash assists yet.",14,HudMuted);
   if(HudButton(new UnityEngine.Rect(1230,832,280,46),"START NEW MAP"))StartMatch();
  }
 }
}
