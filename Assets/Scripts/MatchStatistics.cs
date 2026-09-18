using System;
namespace FpsManager
{
 [Serializable] public sealed class PlayerResult
 {
  public int rounds,kills,deaths,assists,headshots,kast;public float damage;
  public static int Mvp(PlayerResult[] players,Predicate<int> eligible=null){int best=-1;if(players==null)return best;for(int i=0;i<players.Length;i++){var p=players[i];if(p==null||p.rounds==0||eligible!=null&&!eligible(i))continue;var b=best<0?null:players[best];if(b==null||p.Rating>b.Rating||p.Rating==b.Rating&&(p.kills>b.kills||p.kills==b.kills&&p.damage>b.damage))best=i;}return best;}
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
  int resultSide;int historySeries=-1,historyMap;
  public bool OpenSeriesStats(int series){if(Tournament==null||series<0||series>=Tournament.series.Length||Tournament.series[series].maps.Count==0)return false;historySeries=series;historyMap=0;return true;}
  public void CloseSeriesStats(){historySeries=-1;}
  void DrawMatchResults()
  {
   bool history=historySeries>=0&&Tournament!=null;
   TournamentMapResult saved=history?Tournament.series[historySeries].maps[historyMap]:null;
   var roster=history?TournamentRoster.Pair(tournamentRoster,Tournament.series[historySeries].teamA,Tournament.series[historySeries].teamB):Data;
   var results=new PlayerResult[10];var overall=new PlayerResult[10];
   for(int i=0;i<10;i++){if(!history){results[i]=Statistics.Result(i,resultSide);overall[i]=Statistics.Result(i);}else{int index=saved.playerIds==null?-1:Array.IndexOf(saved.playerIds,roster.players[i].id);results[i]=index>=0&&saved.playerStats!=null&&index<saved.playerStats.Length?saved.playerStats[index]:null;results[i]=results[i]??new PlayerResult();overall[i]=results[i];}}
   bool first=!history||roster.teams[0].id==Tournament.series[historySeries].teamA;
   int score0=history?(first?saved.scoreA:saved.scoreB):roundWins[0],score1=history?(first?saved.scoreB:saved.scoreA):roundWins[1];
   string winnerId=history?saved.winner:Data.teams[LastRoundWinner].id;
   int mvp=PlayerResult.Mvp(overall,i=>roster.players[i].teamId==winnerId);
   int evp=PlayerResult.Mvp(overall,i=>roster.players[i].teamId!=winnerId);
   string winner=history?TeamName(saved.winner):Data.teams[LastRoundWinner].name;

   HudPanel(new UnityEngine.Rect(0,0,1600,900),new UnityEngine.Color(.035f,.05f,.075f));
   HudText(new UnityEngine.Rect(90,35,1000,42),"MAP COMPLETE  /  "+(history?MatchMaps.Name(saved.mapId):ActiveMapName).ToUpperInvariant(),28,HudWhite,true);
   HudText(new UnityEngine.Rect(90,88,1300,42),roster.teams[0].name+"   "+score0+" : "+score1+"   "+roster.teams[1].name,30,HudWhite,true);
   HudText(new UnityEngine.Rect(90,140,1000,28),"WINNER  "+winner.ToUpperInvariant()+"    /    "+(score0+score1)+" ROUNDS",17,HudGold,true);
   if(!history){string[] tabs={"BOTH","CT","T"};for(int side=0;side<3;side++)if(HudButton(new UnityEngine.Rect(1160+side*105,128,95,42),tabs[side],true,resultSide==side))resultSide=side;}
   for(int team=0;team<2;team++)
   {
    float top=195+team*278;var color=team==0?HudBlue:HudGold;
    HudPanel(new UnityEngine.Rect(90,top,1420,40),new UnityEngine.Color(.10f,.14f,.20f));
    HudText(new UnityEngine.Rect(110,top+7,500,30),roster.teams[team].name.ToUpperInvariant(),20,color,true);
    string[] headers={"K - D","A","HS %","ADR","KAST","RATING*"};float[] xs={660,815,920,1060,1200,1355};
    for(int c=0;c<headers.Length;c++)HudText(new UnityEngine.Rect(xs[c],top+9,145,28),headers[c],16,HudWhite,true);
    var order=new System.Collections.Generic.List<int>();for(int i=0;i<10;i++)if(roster.players[i].teamId==roster.teams[team].id)order.Add(i);
    order.Sort((a,b)=>{int n=results[b].Rating.CompareTo(results[a].Rating);return n!=0?n:a.CompareTo(b);});
    for(int row=0;row<order.Count;row++)
    {
     int i=order[row];var s=results[i];float y=top+42+row*42;
     HudPanel(new UnityEngine.Rect(90,y,1420,40),row%2==0?new UnityEngine.Color(.07f,.09f,.13f):new UnityEngine.Color(.055f,.075f,.11f));
     HudText(new UnityEngine.Rect(110,y+7,520,28),roster.players[i].handle+(i==mvp?"   [MVP]":i==evp?"   [EVP]":""),19,i==mvp?HudGold:i==evp?HudBlue:HudWhite,true);
     string[] values={s.kills+" - "+s.deaths,s.assists.ToString(),s.HeadshotPercent.ToString("F1")+"%",s.Adr.ToString("F1"),s.Kast.ToString("F1")+"%",s.Rating.ToString("F2")};
     for(int c=0;c<values.Length;c++)HudText(new UnityEngine.Rect(xs[c],y+8,145,28),s.rounds==0?"--":values[c],17,c==5?(s.Rating>=1?HudBlue:HudGold):HudWhite,c==5);
    }
   }
   HudText(new UnityEngine.Rect(90,765,1400,25),"ADR = enemy HP damage / rounds   |   KAST = kill, damage assist (50+), survive, or traded within 5s",14,HudMuted);
   HudText(new UnityEngine.Rect(90,792,1400,25),"* Prototype rating: kills, ADR, KAST and deaths. Not HLTV Rating 3.0. MVP: winning team / EVP: losing team. Rating > kills > damage.",14,HudMuted);
   if(history){
    var maps=Tournament.series[historySeries].maps;for(int n=0;n<maps.Count;n++)if(HudButton(new UnityEngine.Rect(90+n*200,832,190,46),MatchMaps.Name(maps[n].mapId),true,n==historyMap))historyMap=n;
    if(HudButton(new UnityEngine.Rect(1230,832,280,46),korean?"대진표로 돌아가기":"BACK TO BRACKET"))CloseSeriesStats();
   }
   else if(Tournament!=null){if(HudButton(new UnityEngine.Rect(1230,832,280,46),"TOURNAMENT BRACKET"))tournamentBoard=true;}
   else {if(HudButton(new UnityEngine.Rect(1230,832,280,46),"START NEW MAP"))StartMatch();if(HudButton(new UnityEngine.Rect(90,832,280,46),"NEW TOURNAMENT"))OpenTeamSelection();}
  }
 }
}
