using System;using System.Collections.Generic;using UnityEngine;
namespace FpsManager {
 [Serializable] public class TournamentMapResult {public string mapId;public int scoreA,scoreB;public string winner;public string[] playerIds;public PlayerResult[] playerStats;}
 [Serializable] public class TournamentSeries {
  public string teamA,teamB,winner,banA,banB;public string[] mapOrder;public int bestOf,winsA,winsB;public List<TournamentMapResult> maps=new List<TournamentMapResult>();
  public bool Ready {get{return !string.IsNullOrEmpty(teamA)&&!string.IsNullOrEmpty(teamB)&&string.IsNullOrEmpty(winner);}}
 }
 [Serializable] public class MajorBracket {
  public string controlledTeam="spirit";public int version=1;public string[] entrants;public TournamentSeries[] series;public int seed;
  public string Champion {get{return series[6].winner;}}
  public int Next {get{for(int i=0;i<7;i++)if(series[i].Ready)return i;return -1;}}
  public MajorBracket(){}
  public MajorBracket(TeamData[] teams,int seed,bool randomize=false){
   if(teams==null||teams.Length!=8)throw new ArgumentException("Eight teams required");this.seed=seed;
   var sorted=new List<TeamData>(teams);sorted.Sort((a,b)=>a.seed.CompareTo(b.seed));entrants=sorted.ConvertAll(t=>t.id).ToArray();
   if(randomize){var rng=new System.Random(seed);for(int i=7;i>0;i--){int j=rng.Next(i+1);string swap=entrants[i];entrants[i]=entrants[j];entrants[j]=swap;}}
   if(new HashSet<string>(entrants).Count!=8)throw new ArgumentException("Duplicate teams");
   series=new TournamentSeries[7];for(int i=0;i<7;i++)series[i]=new TournamentSeries{bestOf=i==6?5:3};
   int[] order={0,7,3,4,1,6,2,5};for(int i=0;i<4;i++){series[i].teamA=entrants[order[i*2]];series[i].teamB=entrants[order[i*2+1]];}
  }
  public void RecordMap(int index,string winningTeam,int scoreA,int scoreB){
   if(index!=Next||index<0||index>=7)throw new InvalidOperationException("Series not ready");var s=series[index];
   if(winningTeam!=s.teamA&&winningTeam!=s.teamB)throw new ArgumentException("Winner not in series");
   if(scoreA<0||scoreB<0||!Prototype.WinningScore(scoreA,scoreB)||scoreA==scoreB||(winningTeam==s.teamA)!=(scoreA>scoreB))throw new ArgumentException("Invalid map score");
   MatchMaps.Draft(s,unchecked(seed+index*1009));
   s.maps.Add(new TournamentMapResult{mapId=s.mapOrder[s.maps.Count],winner=winningTeam,scoreA=scoreA,scoreB=scoreB});if(winningTeam==s.teamA)s.winsA++;else s.winsB++;
   if(Math.Max(s.winsA,s.winsB)<=s.bestOf/2)return;s.winner=winningTeam;
   if(index<6){int next=index<4?4+index/2:6;if(index%2==0)series[next].teamA=winningTeam;else series[next].teamB=winningTeam;}
  }
  public bool Valid(){
   try{
    if(version!=1||entrants==null||entrants.Length!=8||new HashSet<string>(entrants).Count!=8||series==null||series.Length!=7)return false;
    if(Array.IndexOf(entrants,controlledTeam)<0)return false;var teams=new TeamData[8];for(int i=0;i<8;i++){if(string.IsNullOrEmpty(entrants[i]))return false;teams[i]=new TeamData{id=entrants[i],seed=i};}
    var replay=new MajorBracket(teams,seed);
    for(int i=0;i<7;i++){var s=series[i];if(s==null||s.maps==null||s.maps.Count>5||!MatchMaps.ValidDraft(s))return false;foreach(var map in s.maps){if(map==null)return false;replay.RecordMap(i,map.winner,map.scoreA,map.scoreB);}}
    for(int i=0;i<7;i++){var a=series[i];var b=replay.series[i];if((a.teamA??"")!=(b.teamA??"")||(a.teamB??"")!=(b.teamB??"")||(a.winner??"")!=(b.winner??"")||a.winsA!=b.winsA||a.winsB!=b.winsB||a.bestOf!=b.bestOf)return false;}
    return true;
   }catch(Exception){return false;}
  }
 }
 public static class TournamentRoster {
  public static Database Load(){var asset=Resources.Load<TextAsset>("tournament_roster");if(asset==null)throw new InvalidOperationException("Tournament roster missing");var db=JsonUtility.FromJson<Database>(ConditionRules.MigrateJson(asset.text));Validate(db);return db;}
  public static void Validate(Database db){
   if(db==null||db.teams==null||db.teams.Length!=8||db.players==null||db.players.Length!=40)throw new InvalidOperationException("Expected 8 teams / 40 players");
   var ids=new HashSet<string>();foreach(var p in db.players){if(!ids.Add(p.id)||p.stats==null||p.weapons==null)throw new InvalidOperationException("Invalid player");foreach(var n in new[]{p.stats.aim,p.stats.utility,p.stats.movement,p.stats.mental,p.stats.composure})if(n<1||n>100)throw new InvalidOperationException("Invalid stat");}
   var teams=new HashSet<string>();foreach(var t in db.teams){if(!teams.Add(t.id))throw new InvalidOperationException("Duplicate team");int count=0;bool captain=false;foreach(var p in db.players)if(p.teamId==t.id){count++;captain|=p.id==t.iglPlayerId;}if(count!=5||!captain)throw new InvalidOperationException("Invalid five / IGL: "+t.id);}
  }
  public static Database Pair(Database db,string a,string b){if(a==b)throw new ArgumentException("Duplicate fixture");if(b=="spirit"){string swap=a;a=b;b=swap;}var teams=new TeamData[2];foreach(var t in db.teams){if(t.id==a)teams[0]=t;if(t.id==b)teams[1]=t;}if(teams[0]==null||teams[1]==null)throw new ArgumentException("Unknown fixture");var p=new List<PlayerData>();foreach(var t in teams)foreach(var player in db.players)if(player.teamId==t.id)p.Add(player);if(p.Count!=10)throw new InvalidOperationException("Invalid match roster");return new Database{teams=teams,players=p.ToArray()};}
 }
 public partial class Prototype {
  public MajorBracket Tournament {get;private set;}
  internal static string TournamentSaveKey="fps.major.playoffs.v1";
  Database tournamentRoster;bool tournamentBoard,startingTournamentMap,tournamentMapRecorded;int playingSeries=-1;
  public bool ComputerOnlyMatch {get{return Tournament!=null&&Data.teams[0].id!=AlliedTeamId&&Data.teams[1].id!=AlliedTeamId;}}
  public bool TournamentBoard {get{return tournamentBoard;}}
  public bool StartTournament(){return StartTournament(AlliedTeamId,roundSeed,false);}
  public bool StartTournament(string team,int seed,bool randomize=true){if(AutomaticMatch&&Stage!=MatchStage.Finished)return false;tournamentRoster=TournamentRoster.Load();bool found=false;foreach(var t in tournamentRoster.teams)found|=t.id==team;if(!found)return false;AlliedTeamId=team;Tournament=new MajorBracket(tournamentRoster.teams,seed,randomize){controlledTeam=team};menuPage=0;tournamentBoard=true;historySeries=-1;playingSeries=-1;vetoSeries=-1;SaveTournament();return true;}
  public bool PlayTournamentMap(bool interactiveVeto=false){
   if(Tournament==null||!tournamentBoard||Tournament.Next<0)return false;playingSeries=Tournament.Next;var s=Tournament.series[playingSeries];
   if(s.mapOrder==null||s.mapOrder.Length==0){
    if(interactiveVeto&&s.bestOf==3&&(s.teamA==AlliedTeamId||s.teamB==AlliedTeamId)){vetoSeries=playingSeries;return false;}
    MatchMaps.Draft(s,unchecked(Tournament.seed+playingSeries*1009));SaveTournament();
   }
   vetoSeries=-1;ActivateMatchMap(s.mapOrder[s.maps.Count]);
   Data=TournamentRoster.Pair(tournamentRoster,s.teamA,s.teamB);for(int i=0;i<10;i++){teamIndex[i]=i/5;aimStats[i]=Data.players[i].stats.aim;composureStats[i]=Data.players[i].stats.composure;actors[i].name=Data.players[i].handle;}
   ctTeam=s.maps.Count%2;roundSeed=unchecked(Tournament.seed+playingSeries*1009+s.maps.Count*7919);Stage=MatchStage.Finished;startingTournamentMap=true;
   bool started=StartMatch();startingTournamentMap=false;tournamentBoard=!started;tournamentMapRecorded=false;if(started){AwaitingMapStart=true;Stage=MatchStage.Lobby;StageSeconds=0;Select(AlliedTeamIndex*5);}return started;
  }
  void RecordTournamentMap(){if(Tournament==null||Stage!=MatchStage.Finished||tournamentMapRecorded||playingSeries<0)return;var s=Tournament.series[playingSeries];int a=Data.teams[0].id==s.teamA?0:1;
   Tournament.RecordMap(playingSeries,Data.teams[LastRoundWinner].id,roundWins[a],roundWins[1-a]);var result=s.maps[s.maps.Count-1];result.playerIds=new string[10];result.playerStats=new PlayerResult[10];for(int i=0;i<10;i++){result.playerIds[i]=Data.players[i].id;var p=Statistics.Result(i);result.playerStats[i]=new PlayerResult{rounds=p.rounds,kills=p.kills,deaths=p.deaths,assists=p.assists,headshots=p.headshots,kast=p.kast,damage=p.damage};}tournamentMapRecorded=true;SaveTournament();}
  string TournamentCaption(){if(Tournament==null||playingSeries<0)return ActiveMapName.ToUpperInvariant()+" / LIVE OBSERVER";var s=Tournament.series[playingSeries];bool first=Data.teams[0].id==s.teamA;return (playingSeries<4?"QF":playingSeries<6?"SF":"FINAL")+" BO"+s.bestOf+" / MAPS "+(first?s.winsA:s.winsB)+":"+(first?s.winsB:s.winsA)+" / "+ActiveMapName.ToUpperInvariant()+" "+(s.maps.Count+1);}
  string TeamName(string id){if(string.IsNullOrEmpty(id))return "TBD";foreach(var t in tournamentRoster.teams)if(t.id==id)return t.name;return id;}
  void SaveTournament(){
#if UNITY_5_3_OR_NEWER
   PlayerPrefs.SetString(TournamentSaveKey,JsonUtility.ToJson(Tournament));PlayerPrefs.Save();
#endif
  }
  public bool LoadTournament(){
#if UNITY_5_3_OR_NEWER
   if(AutomaticMatch&&Stage!=MatchStage.Finished)return false;
   if(!PlayerPrefs.HasKey(TournamentSaveKey))return false;
   try{var saved=JsonUtility.FromJson<MajorBracket>(PlayerPrefs.GetString(TournamentSaveKey));if(saved==null||!saved.Valid())return false;var roster=TournamentRoster.Load();foreach(var id in saved.entrants){bool found=false;foreach(var t in roster.teams)found|=t.id==id;if(!found)return false;}tournamentRoster=roster;vetoSeries=-1;Tournament=saved;AlliedTeamId=saved.controlledTeam;menuPage=0;tournamentBoard=true;historySeries=-1;playingSeries=-1;return true;}catch(Exception){return false;}
#else
   return false;
#endif
  }
  void DrawTournamentBoard(){
   if(historySeries>=0){DrawMatchResults();return;}
   HudPanel(new Rect(0,0,1600,900),new Color(.035f,.05f,.075f));HudText(new Rect(55,25,1450,44),"MAJOR PLAYOFFS / 8 TEAMS",30,HudWhite,true);
   HudText(new Rect(55,78,1450,30),"BO3: one ban per team / BO5: five-map pool, first to 3 / random map order",16,HudMuted);
   for(int i=0;i<7;i++){float x=i<4?55:i<6?575:1095;float y=i<4?145+i*151:i<6?220+(i-4)*300:360;var s=Tournament.series[i];
    HudPanel(new Rect(x,y,445,128),new Color(.09f,.13f,.19f));HudText(new Rect(x+12,y+8,420,25),(i<4?"QUARTERFINAL "+(i+1):i<6?"SEMIFINAL "+(i-3):"GRAND FINAL")+" / BO"+s.bestOf,14,HudMuted,true);
    HudText(new Rect(x+12,y+38,420,28),TeamName(s.teamA)+"    "+s.winsA,19,s.winner==s.teamA?HudGold:HudWhite,true);HudText(new Rect(x+12,y+70,420,28),TeamName(s.teamB)+"    "+s.winsB,19,s.winner==s.teamB?HudGold:HudWhite,true);
    if(s.maps.Count>0&&HudButton(new Rect(x+320,y+5,113,30),korean?"경기 스탯":"STATS"))OpenSeriesStats(i);
    string maps=s.mapOrder!=null?string.Join(" > ",Array.ConvertAll(s.mapOrder,MatchMaps.Name)):"";if(s.maps.Count>0)maps="";foreach(var m in s.maps)maps+=MatchMaps.Name(m.mapId)+" "+m.scoreA+":"+m.scoreB+"  ";HudText(new Rect(x+12,y+99,420,22),maps,13,HudMuted);
   }
   if(Tournament.Next>=0&&!AwaitingMapBan){var next=Tournament.series[Tournament.Next];if(next.mapOrder!=null){
    string bans=next.bestOf==3?TeamName(next.teamA)+" BAN: "+MatchMaps.Name(next.banA)+"   /   "+TeamName(next.teamB)+" BAN: "+MatchMaps.Name(next.banB):"BO5 / NO BANS";
    HudText(new Rect(55,740,1470,25),bans,15,HudGold);
   }}
   HudText(new Rect(55,775,1470,24),"Roster snapshot: 2026-09-18 / MongolZ: last-event five (includes currently benched players).",14,HudMuted);
   HudText(new Rect(55,802,1060,25),Tournament.Next<0?"CHAMPION: "+TeamName(Tournament.Champion):TeamName(AlliedTeamId)+" / "+Ui("Your team. Other matches: AI vs AI. Saved after each map."),16,HudGold,true);
   if(Tournament.Next>=0&&HudButton(new Rect(1170,822,360,48),"PLAY NEXT MAP",!AwaitingMapBan))PlayTournamentMap(true);
   if(Tournament.Next>=0){var next=Tournament.series[Tournament.Next];if(next.teamA!=AlliedTeamId&&next.teamB!=AlliedTeamId&&HudButton(new Rect(790,822,350,48),"SKIP MATCH",!AwaitingMapBan))SkipOtherMatch();}
   DrawMapVeto();
   if(Tournament.Next<0&&HudButton(new Rect(1170,822,360,48),"NEW TOURNAMENT"))OpenTeamSelection();
  }
 }
}




