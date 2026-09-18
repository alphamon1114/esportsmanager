using System;
namespace FpsManager {
 public partial class Prototype {
  AiCoach spectatorCoach=new AiCoach();bool spectatorPrepared;int spectatorLossStreak;
  bool PrepareSpectatorCoach(bool shared=false){
   if(!ComputerOnlyMatch||spectatorPrepared&&!shared)return false;spectatorPrepared=true;int team=AlliedTeamIndex,rifles=0,utility=0,snipers=0;
   for(int i=0;i<10;i++)if(teamIndex[i]==team){if(Array.IndexOf(WeaponCatalog.Ids,WeaponCatalog.Equipped(matchState[i].equipment).id)>=11)rifles++;utility+=MatchEconomy.UtilityCount(matchState[i]);if(Array.IndexOf(matchState[i].equipment,"awp")>=0)snipers++;}
   if(!spectatorCoach.Initialized){spectatorCoach.Prepare(team==ctTeam,rifles,utility,snipers,unchecked(roundSeed^51271));return false;}
   spectatorCoach.Prepare(team==ctTeam,rifles,utility,snipers,unchecked(roundSeed^51271),true);
   if(!shared&&!spectatorCoach.TryTimeout(spectatorLossStreak,roundWins[1-team],roundWins[team],unchecked(roundSeed^981471)))return false;
   spectatorCoach.Prepare(team==ctTeam,rifles,utility,snipers,unchecked(roundSeed^51271),true);TimeoutsRemaining=spectatorCoach.TimeoutsRemaining;
   if(shared)return false;OpponentTimeout=false;buyRemaining=0;Stage=MatchStage.Timeout;StageSeconds=30;CoachSharedTimeout();BeginTimeoutTalks();return true;
  }
 }
}
