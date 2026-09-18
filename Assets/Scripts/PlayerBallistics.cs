using System;using UnityEngine;
namespace FpsManager {
 public sealed partial class CombatSystem {
  public bool PhysicalBullets;
  public Func<Vector2,float,Vector2,float,bool> BulletClear;
  public Action<int,Vector3,Vector3> BulletTraced;
  Vector2[] shotPositions;int[] shotTeams;
  readonly float[] firingSpeed=new float[10];
  public void SetFiringSpeed(int i,float speed,float dt){firingSpeed[i]=Mathf.Max(Mathf.Max(0,speed),firingSpeed[i]-Mathf.Max(0,dt)*35);}
  public float MovementSpread(int i){float t=Mathf.Clamp01((firingSpeed[i]-.35f)/4.65f);string id=WeaponFor(i).id;return t*t*(id=="awp"?12:Array.IndexOf(WeaponCatalog.Ids,id)<6?3.5f:7);}
  void ResetPhysicalFire(){Array.Clear(firingSpeed,0,firingSpeed.Length);shotPositions=null;shotTeams=null;}
  float Feet(int i){return FeetHeight==null?0:FeetHeight(i);}
  public static float FleshRetention(string weapon){return weapon=="awp"?.75f:Array.IndexOf(WeaponCatalog.Ids,weapon)>=11?.6f:.4f;}
  // One ray, distance ordered bodies, no wall penetration. Coordinates match the map's horizontal axes.
  void TracePlayers(int shooter,Vector2 direction,float slope){
   var origin=shotPositions[shooter];float eye=Feet(shooter)+1.65f,max=55;
   if(BulletClear!=null&&!BulletClear(origin,eye,origin+direction*max,eye+slope*max)){float lo=0,hi=max;for(int n=0;n<16;n++){float mid=(lo+hi)*.5f;if(BulletClear(origin,eye,origin+direction*mid,eye+slope*mid))lo=mid;else hi=mid;}max=lo;}
   float after=0,power=1;bool hitAny=false;
   for(int pass=0;pass<3;pass++){
    int best=-1;float nearest=max;HitRegion region=HitRegion.Miss;
    for(int j=0;j<count;j++){if(j==shooter||!Alive(j))continue;var rel=shotPositions[j]-origin;float d=Vector2.Dot(rel,direction);if(d<=after||d>nearest)continue;
     float lateral=Mathf.Abs(rel.x*direction.y-rel.y*direction.x);var r=ResolveRegion(lateral,eye+slope*d-Feet(j)-1,settings.targetRadius);
     if(r==HitRegion.Miss)continue;best=j;nearest=d;region=r;
    }
    if(best<0)break;float friendly=shotTeams[best]==shotTeams[shooter]?.33f:1;
    ApplyScaledHit(shooter,best,region,power*friendly);hitAny=true;after=nearest+.001f;power*=FleshRetention(WeaponFor(shooter).id);
    if(pass==2||power<.12f){max=nearest;break;}
   }
   if(!hitAny)ApplyHit(shooter,-1,HitRegion.Miss);
   if(BulletTraced!=null)BulletTraced(shooter,new Vector3(origin.x,eye,100-origin.y),new Vector3(origin.x+direction.x*max,eye+slope*max,100-origin.y-direction.y*max));
  }
 }
}
