using UnityEngine;
namespace FpsManager {
 public partial class Prototype {
  // Team ownership, casualties and observed contacts only; never inspect hidden enemy positions.
  int ChooseTrustedDefuser(){
   if(director.Phase!=RoundPhase.PostPlant)return -1;
   int defenders=0,attackers=0,near=0,ace=-1;float rating=-1;
   for(int i=0;i<10;i++)if(combat.Alive(i)){
    if(teamIndex[i]!=ctTeam){attackers++;continue;}
    defenders++;float value=PlayerOverall(Data.players[i]);
    if(value>rating){rating=value;ace=i;}
    if(Vector2.Distance(MapPosition(i),director.BombPosition)<=12&&Mathf.Abs(PlayerHeight(i)-BombFloor())<.4f)near++;
   }
   if(attackers==0||defenders<=attackers||near<2)return -1;
   for(int e=0;e<10;e++)if(teamIndex[e]!=ctTeam&&combat.Alive(e)){
    var info=vision.Knowledge(ctTeam,e);
    if(info.known&&info.age<2&&Vector2.Distance(info.lastKnownPosition,director.BombPosition)<10)return -1;
   }
   int chosen=-1;float nearest=float.MaxValue;
   for(int i=0;i<10;i++){
    if(teamIndex[i]!=ctTeam||!combat.Alive(i)||i==ace||Data.players[i].weaponPosition=="awper")continue;
    float distance=Vector2.Distance(MapPosition(i),director.BombPosition);
    if(distance<=12&&Mathf.Abs(PlayerHeight(i)-BombFloor())<.4f&&SafeToDefuse(i)&&distance<nearest){nearest=distance;chosen=i;}
   }
   return chosen;
  }
  bool StepUncontestedDefuse(int i,float dt){
   if(director.Phase!=RoundPhase.PostPlant||teamIndex[i]!=ctTeam||combat.LivingCount(teamIndex,1-ctTeam)>0)return false;
   savingEquipment[i]=saveCrouched[i]=saveGoalValid[i]=false;
   peeking[i].Cancel();clutchSearch[i].Cancel();hurtUntil[i]=tradeUntil[i]=0;autonomy.Walking[i]=false;
   var point=director.BombPosition;float distance=Vector2.Distance(MapPosition(i),point);
   var order=new PlayerObjective{valid=true,task=PlayerTask.Defuse,destination=point,watch=point-MapPosition(i)};
   combat.SetKnife(i,distance>3);
   if(StepElevation(i,order,dt))return true;
   if(distance<=2&&Mathf.Abs(PlayerHeight(i)-BombFloor())<1){moving[i]=false;arrived[i]=true;moveSpeed[i]=0;routeValid[i]=false;return true;}
   var before=MapPosition(i);MoveTo(i,point);
   if(routeValid[i]&&routeSteps[i]<routes[i].Count){FaceWatch(i,routes[i][routeSteps[i]]-before,dt);StepRoute(i,dt);}
   moving[i]=Vector2.Distance(before,MapPosition(i))>.001f;arrived[i]=!moving[i];return true;
  }
  int PostPlantTargetPriority(int player,int enemy){
   if(director.Phase!=RoundPhase.PostPlant||teamIndex[player]==ctTeam||director.Defuser<0||director.DefuseProgress<=0)return 0;
   int ct=0,t=0;for(int i=0;i<10;i++)if(combat.Alive(i)){if(teamIndex[i]==ctTeam)ct++;else t++;}
   // Seeing the defuser identifies the action. Sound alone does not reveal an identity through a wall.
   if(ct<=t||!vision.Sees(player,director.Defuser)||enemy!=director.Defuser)return 0;
   return director.BombTimer<=10?1:-1;
  }
 }
}
