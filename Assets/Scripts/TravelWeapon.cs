using UnityEngine;
namespace FpsManager
{
 public sealed partial class CombatSystem
 {
  readonly bool[] knifeOut=new bool[10];
  public bool KnifeOut(int i){return knifeOut[i];}
  public void SetKnife(int i,bool value)
  {
   if(knifeOut[i]==value)return;knifeOut[i]=value;states[i].target=-1;
   if(!value)fireHold[i]=Mathf.Max(fireHold[i],.35f);
  }
 }
 public partial class Prototype
 {
  public string HeldWeapon(int i){return (!AutomaticMatch&&!roundMode&&!deploymentStarted)||combat.KnifeOut(i)?"knife":combat.WeaponFor(i).id;}
  bool SafeForTravel(int i)
  {
   if(combat.Engaging(i)||combat.Reloading(i)||autonomy==null||autonomy.Blinded[i])return false;
   var sound=autonomy.Sounds.Heard(i);if(sound.known&&sound.age<3&&Vector2.Distance(MapPosition(i),sound.position)<28)return false;
   for(int enemy=0;enemy<10;enemy++)if(teamIndex[enemy]!=teamIndex[i])
   {var known=vision.Knowledge(teamIndex[i],enemy);if(vision.Sees(i,enemy)||(known.known&&Vector2.Distance(MapPosition(i),known.lastKnownPosition)<28))return false;}
   return true;
  }
  void UpdateTravelWeapon(int i,PlayerObjective order)
  {
   if(!AutomaticMatch)return;
   bool travel=order.valid&&!order.cautious&&!order.disengage&&order.task!=PlayerTask.PlantBomb&&order.task!=PlayerTask.Defuse&&order.task!=PlayerTask.RecoverBomb&&order.task!=PlayerTask.Lurk&&order.task!=PlayerTask.Retake;
   float distance=Vector2.Distance(MapPosition(i),order.destination);
   bool safe=SafeForTravel(i);
   bool knife=travel&&safe&&distance>(combat.KnifeOut(i)?10:18)&&!peeking[i].Active;
   // After the opening run, only draw the knife on a clear known corridor.
   if(director.Clock>=15&&!navigation.SightClear(MapPosition(i),order.destination))knife=false;
   combat.SetKnife(i,knife);peeking[i].InformationAllowed=director.Clock>=15||!safe;
  }
 }
}
