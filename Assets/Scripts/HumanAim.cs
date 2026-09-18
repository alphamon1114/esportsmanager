using System;using UnityEngine;
namespace FpsManager {
 // Perception samples and motor correction are separate. These are game tuning values.
 public sealed class HumanAim {
  readonly DeterministicRandom random;int target=-1;float delay,refresh,speed,bias;Vector2 perceived;
  public HumanAim(int seed){random=new DeterministicRandom(seed);}
  public Vector2 Step(float dt,int contact,Vector2 position,Vector2 facing,Vector2 visiblePoint,int aim){
   if(contact<0){target=-1;speed=0;return facing;}
   float skill=Mathf.Clamp01(aim/100f);
   if(contact!=target){target=contact;delay=.14f+.1f*(1-skill)+random.Next01()*.06f;refresh=0;speed=0;perceived=position+facing*10;}
   delay-=dt;if(delay>0)return facing;
   refresh-=dt;if(refresh<=0){perceived=visiblePoint;refresh=.08f+.06f*(1-skill)+random.Next01()*.04f;bias=random.NextSigned()*(.18f+.65f*(1-skill));}
   var direction=perceived-position;float error=CombatSystem.SignedAngle(facing,direction)+bias;
   float desired=Mathf.Clamp(error*9,-(130+skill*70),130+skill*70);speed+=Mathf.Clamp(desired-speed,-800*dt,800*dt);
   float step=speed*dt;if(Mathf.Abs(step)>Mathf.Abs(error)&&step*error>0)step=error;
   float angle=Mathf.Atan2(facing.y,facing.x)+step*Mathf.Deg2Rad;return new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
  }
 }
 public sealed partial class CombatSystem {
  readonly HumanAim[] humanAim=new HumanAim[10];readonly int[] heightTarget=new int[10];readonly float[] heightIntent=new float[10],heightRefresh=new float[10],observedFeet=new float[10];readonly DeterministicRandom[] intentRandom=new DeterministicRandom[10];
  void ResetHumanAim(int seed){for(int i=0;i<count;i++){humanAim[i]=new HumanAim(unchecked(seed^(i+1)*72937));intentRandom[i]=new DeterministicRandom(unchecked(seed^(i+1)*61543));heightTarget[i]=-1;heightRefresh[i]=0;heightIntent[i]=0;}}
  public Vector2 HumanFacing(int i,Vector2 position,Vector2 facing,Vector2 visiblePoint,float dt,int aim){return humanAim[i].Step(dt,FocusTarget(i),position,facing,visiblePoint,aim);}
  float HumanHeight(int i,int target,float dt,int aim){
   if(heightTarget[i]!=target){heightTarget[i]=target;heightIntent[i]=intentRandom[i].Next01()<(WeaponFor(i).id=="awp"?.05f:.12f+.22f*Mathf.Clamp01(aim/100f))?.75f:.15f;heightRefresh[i]=0;}
   heightRefresh[i]-=dt;if(heightRefresh[i]<=0){observedFeet[i]=FeetHeight(target);heightRefresh[i]=.1f+intentRandom[i].Next01()*.06f;}
   return observedFeet[i]+1+heightIntent[i];
  }
 }
}
