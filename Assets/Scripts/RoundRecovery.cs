using System;
using System.Collections.Generic;
using UnityEngine;
namespace FpsManager
{
 public partial class Prototype
 {
  readonly int[] recoveryRecipient=new int[10],repaymentDonor=new int[10];
  readonly bool[] recoveryDone=new bool[10];
  bool buySupportPlanned;
  void ResetRecoveryTrades(){for(int i=0;i<10;i++){recoveryRecipient[i]=repaymentDonor[i]=-1;recoveryDone[i]=false;}}
  void BeginRecovery(){for(int i=0;i<10;i++){recoveryRecipient[i]=-1;recoveryDone[i]=false;routeValid[i]=false;repathDelay[i]=0;}}
  string OwnedPrimary(int i){foreach(var id in matchState[i].equipment)if(Array.IndexOf(WeaponCatalog.Ids,id)>=6)return id;return null;}
  int AwpRecipient(int donor)
  {
   if(Data.players[donor].weaponPosition=="awper")return -1;
   for(int i=0;i<10;i++)if(i!=donor&&teamIndex[i]==teamIndex[donor]&&Data.players[i].weaponPosition=="awper"&&Array.IndexOf(matchState[i].equipment,"awp")<0)
   {
    bool reserved=false;for(int j=0;j<10;j++)if(recoveryRecipient[j]==i)reserved=true;
    foreach(var drop in groundWeapons)if(drop.weapon=="awp"&&drop.receiver==i)reserved=true;
    if(!reserved)return i;
   }
   return -1;
  }
  void TickRoundRecovery(float dt)
  {
   BeginPlayerMovement();SettleGroundWeapons(dt);DeliverSupport(dt);
   for(int i=0;i<10;i++)
   {
    moving[i]=false;aimApplied[i]=false;repathDelay[i]=Mathf.Max(0,repathDelay[i]-dt);
    if(!combat.Alive(i))continue;
    tacticalCrouch[i]=false;if(StepSourceJump(i,dt))continue;
    var recoveryOrder=new PlayerObjective{valid=true,task=PlayerTask.Retake,destination=homeAnchor[i]};
    if(ElevatedMatch&&climbers[i].link>=0&&StepElevation(i,recoveryOrder,dt))continue;
    string held=OwnedPrimary(i);GroundWeapon best=null;float score=float.MinValue;
    foreach(var drop in groundWeapons)
    {
     if(recoveryDone[i]||drop.receiver>=0||drop.owner==i||Array.IndexOf(WeaponCatalog.Ids,drop.weapon)<6)continue;
     bool awp=drop.weapon=="awp"&&held!="awp";
     if(!awp&&held!=null&&(drop.weapon==held||MatchEconomy.Skill(Data.players[i],drop.weapon)<=MatchEconomy.Skill(Data.players[i],held)))continue;
     var at=new Vector2(drop.position.x,100-drop.position.z);float distance=Vector2.Distance(MapPosition(i),at);
     if(distance>24||Mathf.Abs(PlayerHeight(i)-drop.position.y)>.4f)continue;
     if(!elevation.Sight(MapPosition(i),PlayerHeight(i)+1.65f,at,drop.position.y+.15f)||!autonomy.ClearSight3D(MapPosition(i),PlayerHeight(i)+1.65f,at,drop.position.y+.15f))continue;
     float value=(awp?1000:MatchEconomy.Skill(Data.players[i],drop.weapon)*20)-distance;
     if(value>score){score=value;best=drop;}
    }
    if(best==null){
     var before=MapPosition(i);
     if(!routeValid[i]||routeSteps[i]>=routes[i].Count){
      var offset=new Vector2(i%2==0?3:-3,i%3==0?2:-2);var destination=before+offset;
      if(!navigation.Clear(before,destination))destination=before-offset;
      if(navigation.Clear(before,destination)){repathDelay[i]=0;MoveTo(i,destination);}
     }
     if(routeValid[i]&&routeSteps[i]<routes[i].Count){FaceWatch(i,routes[i][routeSteps[i]]-before,dt);StepRoute(i,dt);moving[i]=Vector2.Distance(before,MapPosition(i))>.001f;}
     continue;
    }
    var point=new Vector2(best.position.x,100-best.position.z);
    FaceWatch(i,point-MapPosition(i),dt);
    if(Vector2.Distance(MapPosition(i),point)<=1.5f)
    {
     if(TryPickUpWeapon(i,best))
     {
      recoveryDone[i]=true;
      if(best.weapon=="awp")
      {
       recoveryRecipient[i]=AwpRecipient(i);int recipient=recoveryRecipient[i];
       if(recipient>=0&&combat.Alive(recipient)&&Vector2.Distance(MapPosition(i),MapPosition(recipient))<=4)SendRecoveredAwp(i,recipient);
      }
     }
    }
    else
    {
     var before=MapPosition(i);autonomy.Walking[i]=false;MoveTo(i,point);
     if(routeValid[i]&&routeSteps[i]<routes[i].Count)StepRoute(i,dt);
     moving[i]=Vector2.Distance(before,MapPosition(i))>.001f;
    }
   }
   ResolvePlayerMovement(dt);
  }

  void SendRecoveredAwp(int donor,int recipient)
  {
   if(recipient<0||recipient==donor||teamIndex[donor]!=teamIndex[recipient]||Array.IndexOf(matchState[recipient].equipment,"awp")>=0)return;
   var drop=DropStoredWeapon(donor,"awp");if(drop==null)return;
   drop.receiver=recipient;drop.delivery=.35f;drop.recoveryDonor=donor;drop.recoveryTrade=true;recoveryRecipient[donor]=-1;
  }
  bool HasSecondary(int i){foreach(var id in matchState[i].equipment){int n=Array.IndexOf(WeaponCatalog.Ids,id);if(n>=0&&n<6)return true;}return false;}
  void PreserveRecoveryDeliveries()
  {
   foreach(var drop in groundWeapons.ToArray())if(drop.recoveryDonor>=0)
   {
    int donor=drop.recoveryDonor;var items=new List<string>(matchState[donor].equipment);
    items.RemoveAll(id=>Array.IndexOf(WeaponCatalog.Ids,id)>=6);items.Add("awp");matchState[donor].equipment=items.ToArray();
    recoveryRecipient[donor]=drop.receiver;groundWeapons.Remove(drop);
   }
  }
  void StartRecoveryTrades()
  {
   for(int i=0;i<10;i++)if(recoveryRecipient[i]>=0)SendRecoveredAwp(i,recoveryRecipient[i]);
   for(int i=0;i<10;i++)RepayRecoveredAwp(i);
  }
  bool PendingRecoveryDelivery(){foreach(var drop in groundWeapons)if(drop.recoveryTrade&&drop.receiver>=0)return true;return false;}
  void RecoveryDelivered(int recipient,GroundWeapon drop)
  {
   if(drop.weapon!="awp"||drop.recoveryDonor<0)return;
   repaymentDonor[recipient]=drop.recoveryDonor;
   if(Stage==MatchStage.Buying)RepayRecoveredAwp(recipient);
  }
  void RepayRecoveredAwp(int awper)
  {
   int donor=repaymentDonor[awper];if(donor<0)return;repaymentDonor[awper]=-1;
   if(teamIndex[donor]!=teamIndex[awper]||Array.IndexOf(matchState[awper].equipment,"awp")<0||matchState[awper].credits<=matchState[donor].credits)return;
   bool ct=teamIndex[donor]==ctTeam;string gun=ct?"m4a1_s":"ak_47";
   int price=MatchEconomy.Price(gun);if(price<=0||matchState[awper].credits<price)return;
   matchState[awper].credits-=price;
   groundWeapons.Add(new GroundWeapon{weapon=gun,ammo=WeaponDropRules.Fresh(gun),owner=awper,receiver=donor,delivery=.35f,recoveryTrade=true,position=actors[awper].transform.position-Vector3.up});
  }
 }
}
