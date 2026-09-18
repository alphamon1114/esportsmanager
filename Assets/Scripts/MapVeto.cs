using System;using System.Collections.Generic;using UnityEngine;
namespace FpsManager {
 public static class MatchMaps {
  public static readonly string[] Pool={"de_inferno","de_dust2","de_nuke","de_vertigo","de_mirage"};
  public static string Name(string id){switch(id){case "de_dust2":return "Dust II";case "de_nuke":return "Nuke";case "de_vertigo":return "Vertigo";case "de_mirage":return "Mirage";default:return "Inferno";}}
  public static bool Known(string id){return Array.IndexOf(Pool,id)>=0;}
  public static bool ValidDraft(TournamentSeries s){
   if(s.mapOrder==null||s.mapOrder.Length==0)return string.IsNullOrEmpty(s.banA)&&string.IsNullOrEmpty(s.banB);
   if(s.mapOrder.Length!=s.bestOf||new HashSet<string>(s.mapOrder).Count!=s.bestOf)return false;
   foreach(var id in s.mapOrder)if(!Known(id))return false;
   if(s.bestOf==3){if(!Known(s.banA)||!Known(s.banB)||s.banA==s.banB||Array.IndexOf(s.mapOrder,s.banA)>=0||Array.IndexOf(s.mapOrder,s.banB)>=0)return false;}
   else if(s.bestOf!=5||!string.IsNullOrEmpty(s.banA)||!string.IsNullOrEmpty(s.banB))return false;
   for(int i=0;i<s.maps.Count;i++)if(!string.IsNullOrEmpty(s.maps[i].mapId)&&s.maps[i].mapId!=s.mapOrder[i])return false;
   return true;
  }
  public static void Draft(TournamentSeries s,int seed,string humanTeam=null,string humanBan=null){
   if(s.mapOrder!=null&&s.mapOrder.Length>0)return;
   var remaining=new List<string>(Pool);var rng=new System.Random(seed);
   if(s.bestOf==3){
    string a=s.teamA==humanTeam?humanBan:null,b=s.teamB==humanTeam?humanBan:null;
    if(humanTeam!=null&&!Known(humanBan))throw new ArgumentException("Invalid map ban");
    if(a!=null)remaining.Remove(a);if(b!=null)remaining.Remove(b);
    if(a==null){a=remaining[rng.Next(remaining.Count)];remaining.Remove(a);}
    if(b==null){b=remaining[rng.Next(remaining.Count)];remaining.Remove(b);}
    s.banA=a;s.banB=b;
   }
   for(int n=remaining.Count-1;n>0;n--){int j=rng.Next(n+1);var x=remaining[n];remaining[n]=remaining[j];remaining[j]=x;}
   s.mapOrder=remaining.ToArray();
  }
 }
 public partial class Prototype {
  int vetoSeries=-1;
  public bool AwaitingMapBan {get{return vetoSeries>=0;}}
  public string ActiveMapId {get;private set;}="de_inferno";
  public string ActiveMapName {get{return MatchMaps.Name(ActiveMapId);}}
  public bool BanMap(string id){
   if(Tournament==null||vetoSeries<0||vetoSeries!=Tournament.Next||!MatchMaps.Known(id))return false;
   var s=Tournament.series[vetoSeries];if(s.teamA!=AlliedTeamId&&s.teamB!=AlliedTeamId)return false;
   MatchMaps.Draft(s,unchecked(Tournament.seed+vetoSeries*1009),AlliedTeamId,id);vetoSeries=-1;SaveTournament();return true;
  }
  void DrawMapVeto(){
   if(!AwaitingMapBan)return;
   HudPanel(new Rect(320,180,960,540),new Color(.04f,.06f,.09f,.99f));
   HudText(new Rect(355,212,890,40),"MAP BAN / BO3",26,HudWhite,true);
   HudText(new Rect(355,260,890,28),"Ban one map. Opponent bans one. Remaining maps play in random order.",16,HudMuted);
   for(int i=0;i<MatchMaps.Pool.Length;i++){string id=MatchMaps.Pool[i];if(HudButton(new Rect(400,315+i*65,800,48),MatchMaps.Name(id)))BanMap(id);}
  }
 }
}
