using UnityEngine;
namespace FpsManager
{
 // One engagement identity shared by movement, peeking and firing. Memory never follows hidden transforms.
 public sealed class EngagementFocus
 {
  public int Target {get;private set;}=-1;
  public Vector2 Point {get;private set;}
  float unseen;
  public void Tick(float dt,int player,Vector2 position,Vector2 facing,int[] teams,VisionSystem vision,CombatSystem combat)
  {
   if(!combat.Alive(player)){Target=-1;return;}
   if(Target>=0&&(!combat.Alive(Target)||teams[Target]==teams[player]))Target=-1;
   int preferred=-1,rank=int.MinValue;float nearestPreferred=float.MaxValue;
   for(int e=0;e<teams.Length;e++){
    if(teams[e]==teams[player]||!combat.Alive(e)||!vision.Sees(player,e))continue;
    int priority=combat.PriorityFor(player,e);float distance=Vector2.Distance(position,vision.Knowledge(teams[player],e).lastKnownPosition);
    if(priority>rank||(priority==rank&&distance<nearestPreferred)){preferred=e;rank=priority;nearestPreferred=distance;}
   }
   if(Target>=0&&preferred>=0&&rank>combat.PriorityFor(player,Target)){
    Target=preferred;Point=vision.Knowledge(teams[player],Target).lastKnownPosition;unseen=0;return;
   }
   if(Target>=0)
   {
    if(vision.Sees(player,Target)){Point=vision.Knowledge(teams[player],Target).lastKnownPosition;unseen=0;}
    else unseen+=dt;
    // A genuinely close visible threat may interrupt a long-range engagement.
    float currentDistance=Vector2.Distance(position,Point);
    int urgent=-1;float nearest=6;
    for(int enemy=0;enemy<teams.Length;enemy++)
    {
     if(enemy==Target||teams[enemy]==teams[player]||!combat.Alive(enemy)||!vision.Sees(player,enemy))continue;
     float d=Vector2.Distance(position,vision.Knowledge(teams[player],enemy).lastKnownPosition);
     if(combat.PriorityFor(player,enemy)>=combat.PriorityFor(player,Target)&&currentDistance>12&&d<nearest&&d<currentDistance*.45f){urgent=enemy;nearest=d;}
    }
    if(urgent>=0){Target=urgent;Point=vision.Knowledge(teams[player],urgent).lastKnownPosition;unseen=0;return;}
    if(unseen<=.25f)return;
    Target=-1;
   }
   float best=float.MaxValue;
   for(int enemy=0;enemy<teams.Length;enemy++)
   {
    if(teams[enemy]==teams[player]||!combat.Alive(enemy)||!vision.Sees(player,enemy))continue;
    var point=vision.Knowledge(teams[player],enemy).lastKnownPosition;var delta=point-position;
    float score=-combat.PriorityFor(player,enemy)*10000+delta.magnitude*.35f+Mathf.Abs(CombatSystem.SignedAngle(facing,delta))*.1f;
    if(score>=best)continue;best=score;Target=enemy;Point=point;
   }
   unseen=0;
  }
 }
 public sealed partial class CombatSystem
 {
  public bool UnifiedAim;
  public System.Func<int,int,int> TargetPriority;
  public int PriorityFor(int player,int enemy){return TargetPriority==null?0:TargetPriority(player,enemy);}
  readonly EngagementFocus[] engagement=new EngagementFocus[10];
  public int FocusTarget(int i){return UnifiedAim&&engagement[i]!=null?engagement[i].Target:-1;}
  public Vector2 FocusPoint(int i){return engagement[i].Point;}
  public void UpdateEngagementFocus(float dt,Vector2[] positions,Vector2[] facing,int[] teams,VisionSystem vision)
  {
   if(!UnifiedAim)return;
   for(int i=0;i<count;i++)
   {if(engagement[i]==null)engagement[i]=new EngagementFocus();engagement[i].Tick(dt,i,positions[i],facing[i],teams,vision,this);if(PhysicalBullets&&engagement[i].Target<0){humanAim[i].Step(dt,-1,positions[i],facing[i],positions[i],0);heightTarget[i]=-1;}}
  }
 }
 public partial class Prototype
 {
  readonly bool[] aimApplied=new bool[10];
  bool UnifiedRoundAim {get{return AutomaticMatch&&roundMode&&director.Phase!=RoundPhase.Ended&&combat.UnifiedAim;}}
  Vector2 SharedAimDirection(int i,Vector2 fallback)
  {
   if(UnifiedRoundAim&&HasDirectFight(i))return combat.FocusPoint(i)-MapPosition(i);
   Vector2 threat;if(UnifiedRoundAim&&ImmediateCue(i,out threat))return threat-MapPosition(i);
   if(UnifiedRoundAim&&combat.Spamming(i))return combat.SuppressionPoint(i)-MapPosition(i);
   if(UnifiedRoundAim&&clutchSearch[i]!=null&&clutchSearch[i].Active)return clutchSearch[i].Point-MapPosition(i);
   movementAim[i].LocalThreatPriority=AutomaticMatch;
   return movementAim[i].Choose(i,MapPosition(i),fallback,teamIndex,vision);
  }
 }
}

