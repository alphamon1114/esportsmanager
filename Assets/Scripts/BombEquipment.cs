using System;
using System.Collections.Generic;
using UnityEngine;
namespace FpsManager
{
 public static class DefuseKitRules
 {
  public const int Price=400;
  public static bool Buy(PlayerMatchState s){if(s.defuseKit||s.credits<Price)return false;s.credits-=Price;s.defuseKit=true;return true;}
 }
 public sealed partial class RoundDirector
 {
  public Func<int,bool> HasDefuseKit;
  public Func<int,Vector2,bool> BombReachable;
  public int Defuser {get;private set;}=-1;
  int lastDropper=-1;float reclaimAt;
  public float DefuseDuration {get{return Defuser>=0&&HasDefuseKit!=null&&HasDefuseKit(Defuser)?settings.defuseSeconds*.5f:settings.defuseSeconds;}}
  public void RandomizeCarrier(int seed,int[] teams,int ct)
  {
   if(!Running||PlantedSite>=0)return;var candidates=new List<int>();for(int i=0;i<count;i++)if(teams[i]!=ct)candidates.Add(i);
   if(candidates.Count>0)Carrier=candidates[(int)(new DeterministicRandom(unchecked(seed^734287)).NextUInt()%(uint)candidates.Count)];
  }
  public bool DropC4(int player,Vector2 position)
  {
   if(!Running||Carrier!=player||PlantedSite>=0)return false;
   Carrier=-1;BombDropped=true;BombPosition=position;PlantProgress=0;lastDropper=player;reclaimAt=Clock+2;return true;
  }
  public bool PassC4(int from,int to,int[] teams,int ct,Vector2[] positions,CombatSystem combat)
  {
   if(!Running||from!=Carrier||to<0||to>=count||teams[to]==ct||!combat.Alive(from)||!combat.Alive(to)||Vector2.Distance(positions[from],positions[to])>settings.interactRadius)return false;
   if(BombReachable!=null&&!BombReachable(to,positions[from]))return false;
   Carrier=to;PlantProgress=0;BombDropped=false;return true;
  }
 }
 public partial class Prototype
 {
  readonly bool[] kitBuyers=new bool[10];
  readonly List<Vector3> groundKits=new List<Vector3>();
  float droppedBombHeight;
  public int GroundKitCount {get{return groundKits.Count;}}
  public bool DropC4(int player)
  {
   if(!AutomaticMatch||Stage!=MatchStage.Live||player<0||player>=10||!combat.Alive(player))return false;
   if(!director.DropC4(player,MapPosition(player)))return false;droppedBombHeight=PlayerHeight(player);return true;
  }
  public bool PassC4(int from,int to)
  {
   if(!AutomaticMatch||Stage!=MatchStage.Live||from<0||from>=10||to<0||to>=10)return false;
   if(Mathf.Abs(PlayerHeight(from)-PlayerHeight(to))>1)return false;
   var positions=new Vector2[10];for(int i=0;i<10;i++)positions[i]=MapPosition(i);
   return director.PassC4(from,to,teamIndex,ctTeam,positions,combat);
  }
  void ConfigureBombEquipment()
  {
   if(!AutomaticMatch)return;
   director.RandomizeCarrier(roundSeed,teamIndex,ctTeam);
   director.HasDefuseKit=i=>matchState[i].defuseKit;
   director.BombReachable=(i,p)=>Mathf.Abs(PlayerHeight(i)-(director.BombDropped?droppedBombHeight:director.Carrier>=0?PlayerHeight(director.Carrier):0))<1&&elevation.Sight(MapPosition(i),PlayerHeight(i)+1.65f,p,(director.BombDropped?droppedBombHeight:0)+.3f);
  }
  void PlanKits()
  {
   Array.Clear(kitBuyers,0,10);int owned=0;var candidates=new List<int>();
   for(int i=0;i<10;i++)
   {
    if(teamIndex[i]!=ctTeam){matchState[i].defuseKit=false;continue;}
    if(matchState[i].defuseKit){owned++;continue;}
    if(buyPlans[ctTeam]==BuyPlan.Full&&matchState[i].credits>=MatchEconomy.CoreCost(matchState[i],Data.players[i],true)+DefuseKitRules.Price)candidates.Add(i);
   }
   candidates.Sort((a,b)=>matchState[b].credits.CompareTo(matchState[a].credits));
   for(int n=0;n<Math.Min(Math.Max(0,3-owned),candidates.Count);n++)kitBuyers[candidates[n]]=true;
  }
  void DropBombEquipmentOnDeath(int i)
  {
   if(!AutomaticMatch)return;
   if(director.Carrier==i){droppedBombHeight=PlayerHeight(i);director.DropC4(i,MapPosition(i));}
   if(matchState[i].defuseKit){groundKits.Add(actors[i].transform.position-Vector3.up);matchState[i].defuseKit=false;}
  }
  void ClearBombEquipment(){groundKits.Clear();droppedBombHeight=0;SyncBombEquipmentVisuals();}
  float ItemFloor(Vector3 p)
  {
   var point=new Vector2(p.x,100-p.z);float height=ElevationMap.Ground(point);
   foreach(var s in elevation.Solids)if(ElevationMap.Inside(s.area,point)&&s.top<=p.y+.1f)height=Mathf.Max(height,s.top);return height;
  }
  void TickBombEquipment(float dt)
  {
   if(!AutomaticMatch)return;
   if(director.BombDropped){var p=World(director.BombPosition,droppedBombHeight);droppedBombHeight=Mathf.Max(ItemFloor(p),droppedBombHeight-8*dt);}
   for(int k=groundKits.Count-1;k>=0;k--)
   {
    var p=groundKits[k];p.y=Mathf.Max(ItemFloor(p),p.y-8*dt);groundKits[k]=p;
    for(int i=0;i<10;i++)if(teamIndex[i]==ctTeam&&combat.Alive(i)&&!matchState[i].defuseKit&&(actors[i].transform.position-Vector3.up-p).magnitude<1.5f&&SafeToCollect(i)&&elevation.Sight(MapPosition(i),PlayerHeight(i)+1.65f,new Vector2(p.x,100-p.z),p.y+.2f))
    {matchState[i].defuseKit=true;groundKits.RemoveAt(k);break;}
   }
  }
  void DrawBombProgress()
  {
   if(Stage!=MatchStage.Live)return;
   bool planting=director.PlantProgress>0;float progress=planting?director.PlantProgress:director.DefuseProgress;
   if(progress<=0)return;float duration=planting?director.Settings.plantSeconds:director.DefuseDuration;var color=planting?HudGold:HudBlue;
   HudPanel(new Rect(654,101,246,8),new Color(.18f,.21f,.25f));HudPanel(new Rect(654,101,246*Mathf.Clamp01(progress/duration),8),color);
   HudText(new Rect(654,82,250,20),(planting?"PLANTING":"DEFUSING"+(director.Defuser>=0&&matchState[director.Defuser].defuseKit?" [KIT]":""))+"  "+Mathf.Max(0,duration-progress).ToString("F1")+"s",12,color,true);
  }
#if UNITY_5_3_OR_NEWER
  GameObject bombDummy; readonly List<GameObject> kitDummies=new List<GameObject>();
  GameObject BombDummy(string label,Color color,Vector3 size)
  {
   var root=new GameObject(label);root.transform.SetParent(transform,false);
   var body=GameObject.CreatePrimitive(PrimitiveType.Cube);body.transform.SetParent(root.transform,false);body.transform.localScale=size;body.GetComponent<Renderer>().sharedMaterial=Material(color);body.GetComponent<Collider>().enabled=false;body.layer=11;
   var detail=GameObject.CreatePrimitive(PrimitiveType.Cube);detail.transform.SetParent(root.transform,false);detail.transform.localPosition=new Vector3(0,.02f,-size.z*.55f);detail.transform.localScale=new Vector3(size.x*.5f,size.y*.25f,.03f);detail.GetComponent<Renderer>().sharedMaterial=Material(new Color(.12f,.8f,.45f));detail.GetComponent<Collider>().enabled=false;detail.layer=11;return root;
  }
#endif
  void SyncBombEquipmentVisuals()
  {
#if UNITY_5_3_OR_NEWER
   if(director==null)return;
   bool show=AutomaticMatch&&(Stage==MatchStage.Live||Stage==MatchStage.Result)&&(director.Carrier>=0||director.BombDropped||director.PlantedSite>=0);
   if(show&&bombDummy==null)bombDummy=BombDummy("C4 dummy",new Color(.34f,.29f,.15f),new Vector3(.4f,.48f,.2f));
   if(bombDummy!=null)
   {
    bombDummy.SetActive(show);
    if(show&&director.Carrier>=0){var actor=actors[director.Carrier].transform;bombDummy.transform.position=actor.position+Vector3.up*.1f-actor.forward*.35f;bombDummy.transform.rotation=actor.rotation;foreach(var t in bombDummy.GetComponentsInChildren<Transform>())t.gameObject.layer=director.Carrier==selected?12:11;}
    else if(show){bombDummy.transform.position=World(director.BombPosition,(director.BombDropped?droppedBombHeight:0)+.14f);bombDummy.transform.rotation=Quaternion.Euler(90,0,0);foreach(var t in bombDummy.GetComponentsInChildren<Transform>())t.gameObject.layer=11;}
   }
   while(kitDummies.Count<groundKits.Count)kitDummies.Add(BombDummy("Defuse kit dummy",new Color(.1f,.35f,.7f),new Vector3(.32f,.16f,.22f)));
   for(int i=0;i<kitDummies.Count;i++){kitDummies[i].SetActive(i<groundKits.Count);if(i<groundKits.Count)kitDummies[i].transform.position=groundKits[i]+Vector3.up*.1f;}
#endif
  }
 }
}
