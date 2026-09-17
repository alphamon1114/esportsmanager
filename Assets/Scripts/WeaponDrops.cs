using System;
using System.Collections.Generic;
using UnityEngine;
namespace FpsManager
{
 public struct WeaponAmmo { public int rounds,spares; public float reloadRemaining; }
 public sealed partial class CombatSystem
 {
  public WeaponAmmo CaptureAmmo(int i){return new WeaponAmmo{rounds=magazine[i],spares=spareMagazines[i],reloadRemaining=reload[i]};}
  public void EquipSaved(int i,string id,WeaponAmmo ammo)
  {
   Equip(i,WeaponCatalog.Find(id));magazine[i]=Math.Max(0,Math.Min(WeaponFor(i).magazineSize,ammo.rounds));spareMagazines[i]=Math.Max(0,ammo.spares);reload[i]=Math.Max(0,ammo.reloadRemaining);
   recoil[i]=0;burstShots[i]=0;choseFollowup[i]=retap[i]=false;fireHold[i]=0;states[i].target=-1;states[i].reaction=0;states[i].cooldown=.35f;
  }
 }
 public sealed class GroundWeapon
 {
  public string weapon; public WeaponAmmo ammo; public Vector3 position; public int owner,receiver=-1; public float delivery;
 }
 public static class WeaponDropRules
 {
  // A stable equipment tier, rather than raw per-bullet damage (which overvalues pistols).
  public static int Strength(string id)
  {
   int n=Array.IndexOf(WeaponCatalog.Ids,id);if(n<0)return 0;
   if(n<2)return 10;if(n<5)return 15;if(n==5)return 20;if(n<11)return 30;if(n<13)return 40;if(n<17)return 50;return 60;
  }
  public static bool Wants(string held,int rounds,int spares,string ground)
  {return Strength(ground)>Strength(held)||(spares==0&&rounds<=Math.Max(1,(WeaponCatalog.Find(held)??new WeaponProfile()).magazineSize/5));}
  public static WeaponAmmo Fresh(string id){var w=WeaponCatalog.Find(id);return new WeaponAmmo{rounds=w.magazineSize,spares=w.reserveMagazines};}
 }
 public partial class Prototype
 {
  readonly List<GroundWeapon> groundWeapons=new List<GroundWeapon>();
  readonly Dictionary<string,WeaponAmmo>[] holsteredAmmo=new Dictionary<string,WeaponAmmo>[10];
  readonly float[] pickupDelay=new float[10];
  public int GroundWeaponCount {get{return groundWeapons.Count;}}
  public GroundWeapon GroundWeaponAt(int index){return groundWeapons[index];}
  void ClearGroundWeapons()
  {
   groundWeapons.Clear();for(int i=0;i<10;i++){holsteredAmmo[i]=new Dictionary<string,WeaponAmmo>();pickupDelay[i]=0;}SyncGroundWeaponVisuals();
  }
  string StrongestWeapon(int i)
  {
   string best=null;foreach(var id in matchState[i].equipment)if(WeaponDropRules.Strength(id)>WeaponDropRules.Strength(best))best=id;return best;
  }
  void EquipInventory(int i)
  {
   string id=WeaponCatalog.Equipped(matchState[i].equipment).id;WeaponAmmo saved;
   if(holsteredAmmo[i]!=null&&holsteredAmmo[i].TryGetValue(id,out saved))combat.EquipSaved(i,id,saved);else combat.Equip(i,WeaponCatalog.Find(id)??WeaponCatalog.Equipped(new string[0]));
  }
  GroundWeapon DropStoredWeapon(int i,string id)
  {
   if(id==null||Array.IndexOf(matchState[i].equipment,id)<0)return null;
   WeaponAmmo ammo;bool active=combat.WeaponFor(i).id==id;
   if(active)ammo=combat.CaptureAmmo(i);else if(holsteredAmmo[i]==null||!holsteredAmmo[i].TryGetValue(id,out ammo))ammo=WeaponDropRules.Fresh(id);
   var items=new List<string>(matchState[i].equipment);items.Remove(id);matchState[i].equipment=items.ToArray();
   if(holsteredAmmo[i]!=null)holsteredAmmo[i].Remove(id);
   var drop=new GroundWeapon{weapon=id,ammo=ammo,owner=i,position=actors[i].transform.position-Vector3.up};groundWeapons.Add(drop);
   if(active)EquipInventory(i);return drop;
  }
  // Explicit player action; AI exchanges use the same drop path. No opponent coach control.
  public bool DropWeapon(int i)
  {
   if(!AutomaticMatch||i<0||i>=10||!combat.Alive(i)||(Stage!=MatchStage.Live&&Stage!=MatchStage.Buying))return false;
   var drop=DropStoredWeapon(i,StrongestWeapon(i));if(drop==null)return false;pickupDelay[i]=5;return true;
  }
  void DropOnDeath(int i){if(AutomaticMatch)DropStoredWeapon(i,StrongestWeapon(i));}
  bool SafeToCollect(int i)
  {
   if(combat.Engaging(i)||combat.Reloading(i)||autonomy==null||autonomy.Blinded[i])return false;
   var heard=autonomy.Sounds.Heard(i);if(heard.known&&heard.age<3&&Vector2.Distance(MapPosition(i),heard.position)<22)return false;
   for(int enemy=0;enemy<10;enemy++)if(teamIndex[enemy]!=teamIndex[i]){var known=vision.Knowledge(teamIndex[i],enemy);if(vision.Sees(i,enemy)||(known.known&&Vector2.Distance(MapPosition(i),known.lastKnownPosition)<22))return false;}
   return true;
  }
  public bool TryPickUpWeapon(int i,GroundWeapon drop)
  {
   if(!AutomaticMatch||i<0||i>=10||!combat.Alive(i)||drop==null||!groundWeapons.Contains(drop)||drop.delivery>0)return false;
   if(Stage!=MatchStage.Live&&Stage!=MatchStage.Buying)return false;
   if((actors[i].transform.position-Vector3.up-drop.position).magnitude>1.5f)return false;
   var point=new Vector2(drop.position.x,100-drop.position.z);
   if(!elevation.Sight(MapPosition(i),PlayerHeight(i)+1.65f,point,drop.position.y+.15f))return false;
   if(Stage==MatchStage.Live&&!SafeToCollect(i))return false;
   if(drop.receiver>=0&&drop.receiver!=i)return false;
   // Save the untouched secondary before changing active weapon; never refill it by swapping.
   if(holsteredAmmo[i]==null)holsteredAmmo[i]=new Dictionary<string,WeaponAmmo>();
   string held=combat.WeaponFor(i).id;holsteredAmmo[i][held]=combat.CaptureAmmo(i);
   if(WeaponDropRules.Strength(held)>0)DropStoredWeapon(i,held);
   bool primary=Array.IndexOf(WeaponCatalog.Ids,drop.weapon)>=6;
   foreach(var id in (string[])matchState[i].equipment.Clone())if(WeaponCatalog.Find(id)!=null&&(Array.IndexOf(WeaponCatalog.Ids,id)>=6)==primary)DropStoredWeapon(i,id);
   var items=new List<string>(matchState[i].equipment);items.Add(drop.weapon);matchState[i].equipment=items.ToArray();
   combat.SetKnife(i,false);combat.EquipSaved(i,drop.weapon,drop.ammo);groundWeapons.Remove(drop);pickupDelay[i]=5;routeValid[i]=false;return true;
  }
  bool CollectNearbyWeapon(int i,PlayerObjective order,float dt)
  {
   if(!AutomaticMatch)return false;pickupDelay[i]=Mathf.Max(0,pickupDelay[i]-dt);
   if(pickupDelay[i]>0||!SafeToCollect(i)||combat.Health(i)<30||order.task==PlayerTask.PlantBomb||order.task==PlayerTask.Defuse||order.task==PlayerTask.RecoverBomb||order.task==PlayerTask.Retake||order.task==PlayerTask.FallBack)return false;
   GroundWeapon best=null;float distance=6;
   foreach(var item in groundWeapons)
   {
    if(item.owner==i||item.receiver>=0||item.delivery>0)continue;
    // Deliberately never inspect item.ammo here: ammunition is discovered only after pickup.
    if(!WeaponDropRules.Wants(combat.WeaponFor(i).id,combat.Magazine(i),combat.SpareMagazines(i),item.weapon))continue;
    float d=(actors[i].transform.position-Vector3.up-item.position).magnitude;if(d>=distance||Mathf.Abs(PlayerHeight(i)-item.position.y)>.4f)continue;
    var point=new Vector2(item.position.x,100-item.position.z);
    if((d>1.5f&&!navigation.Clear(MapPosition(i),point))||!vision.LineOfSight(MapPosition(i),MapFacing(i),point))continue;
    if(!elevation.Sight(MapPosition(i),PlayerHeight(i)+1.65f,point,item.position.y+.15f)||!autonomy.ClearSight3D(MapPosition(i),PlayerHeight(i)+1.65f,point,item.position.y+.15f))continue;
    best=item;distance=d;
   }
   if(best==null)return false;
   if(distance<=1.5f){TryPickUpWeapon(i,best);arrived[i]=true;return true;}
   var destination=new Vector2(best.position.x,100-best.position.z);var before=MapPosition(i);var next=Vector2.MoveTowards(before,destination,dt*2.4f);
   actors[i].transform.position=World(next,PlayerHeight(i)+1);GroundActor(i);moving[i]=true;arrived[i]=false;AimWhileMoving(i,order,dt);routeValid[i]=false;return true;
  }
  void PlanAceSupport()
  {
   if(CompletedRounds==0)return;
   for(int team=0;team<2;team++)
   {
    var plan=buyPlans[team];if(plan!=BuyPlan.Eco&&plan!=BuyPlan.Force)continue;
    int ace=-1;for(int i=0;i<10;i++)if(teamIndex[i]==team&&(ace<0||Statistics.Result(i).Rating>Statistics.Result(ace).Rating||(Statistics.Result(i).Rating==Statistics.Result(ace).Rating&&Statistics.Result(i).kills>Statistics.Result(ace).kills)))ace=i;
    if(ace<0||Statistics.Result(ace).rounds==0||WeaponDropRules.Strength(StrongestWeapon(ace))>=40)continue;
    bool ct=team==ctTeam;string gun=ct?"m4a1_s":"ak_47";int price=MatchEconomy.Price(gun),donor=-1;
    for(int i=0;i<10;i++)if(i!=ace&&teamIndex[i]==team)
    {
     int reserve=plan==BuyPlan.Eco?Math.Max(0,MatchEconomy.FullBuyFunds(matchState[i],Data.players[i],ct)-2400):(ct?650:1000);
     if(matchState[i].credits-price<reserve)continue;
     if(donor<0||matchState[i].credits>matchState[donor].credits)donor=i;
    }
    if(donor<0)continue;
    matchState[donor].credits-=price;ecoBuyers[donor]=ecoBuyers[ace]=false;
    // Bought for the teammate, so the donor's existing gun is not replaced.
    groundWeapons.Add(new GroundWeapon{weapon=gun,ammo=WeaponDropRules.Fresh(gun),owner=donor,receiver=ace,delivery=.35f,position=actors[donor].transform.position-Vector3.up});
   }
  }
  void SettleGroundWeapons(float dt)
  {
   foreach(var item in groundWeapons)if(item.receiver<0)
   {
    var point=new Vector2(item.position.x,100-item.position.z);float floor=ElevationMap.Ground(point);
    foreach(var solid in elevation.Solids)if(ElevationMap.Inside(solid.area,point)&&solid.top<=item.position.y+.1f)floor=Mathf.Max(floor,solid.top);
    var p=item.position;p.y=Mathf.Max(floor,p.y-8*dt);item.position=p;
   }
  }
  void DeliverSupport(float dt)
  {
   foreach(var item in groundWeapons.ToArray())if(item.receiver>=0)
   {
    item.delivery=Mathf.Max(0,item.delivery-dt);
    // Short spawn-area toss; the recipient equips the actual dropped item on arrival.
    var target=actors[item.receiver].transform.position-Vector3.up;item.position=Vector3.MoveTowards(item.position,target,dt*30);
    if(item.delivery<=0&&(item.position-target).magnitude<=1.5f)TryPickUpWeapon(item.receiver,item);
   }
  }
#if UNITY_5_3_OR_NEWER
  readonly Dictionary<GroundWeapon,GameObject> dropModels=new Dictionary<GroundWeapon,GameObject>();
#endif
  void SyncGroundWeaponVisuals()
  {
#if UNITY_5_3_OR_NEWER
   foreach(var pair in new List<KeyValuePair<GroundWeapon,GameObject>>(dropModels))if(!groundWeapons.Contains(pair.Key)){if(pair.Value!=null){pair.Value.SetActive(false);if(Application.isPlaying)Destroy(pair.Value);else DestroyImmediate(pair.Value);}dropModels.Remove(pair.Key);}
   foreach(var item in groundWeapons)
   {
    GameObject model;if(!dropModels.TryGetValue(item,out model))
    {
     var prefab=Resources.Load<GameObject>("Weapons/"+CharacterAnimation.ModelFor(item.weapon,false));
     model=prefab!=null?Instantiate(prefab):GameObject.CreatePrimitive(PrimitiveType.Cube);model.name="Dropped "+item.weapon;model.transform.SetParent(transform,false);
     var renderers=model.GetComponentsInChildren<Renderer>();if(renderers.Length>0){var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);float span=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));if(span>.001f)model.transform.localScale*=.85f/span;}
     foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=11;
     foreach(var collider in model.GetComponentsInChildren<Collider>())collider.enabled=false;
     model.transform.rotation=Quaternion.Euler(0,35,90);dropModels.Add(item,model);
    }
    model.transform.position=item.position+Vector3.up*.12f;
   }
#endif
  }
 }
}
