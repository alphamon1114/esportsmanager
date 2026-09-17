using System;
using System.Collections.Generic;
using UnityEngine;
namespace FpsManager
{
 // Map coordinates are X/Z projected to Vector2; all height values are feet above ground.
 public sealed class ElevationMap
 {
  public struct Solid { public Rect area;public float bottom,top; }
  public sealed class Link
  {
   public string name;public Vector2 entry;public Vector3[] points;public bool jump;
  }
  public readonly List<Solid> Solids=new List<Solid>();
  public readonly List<Rect> GroundObstacles=new List<Rect>();
  public readonly Link[] Links={
   new Link{name="A upper balcony",entry=new Vector2(84,35.5f),points=new[]{new Vector3(84,3,27),new Vector3(86,3,27),new Vector3(86,3,32),new Vector3(89,3,32)},jump=false},
   new Link{name="A jump box",entry=new Vector2(76,39),points=new[]{new Vector3(76,1.2f,36)},jump=true},
   new Link{name="B jump box",entry=new Vector2(22,20),points=new[]{new Vector3(22,1.2f,17)},jump=true}
  };
  public static bool Inside(Rect r,Vector2 p){return p.x>=r.xMin&&p.x<=r.xMax&&p.y>=r.yMin&&p.y<=r.yMax;}
  public static float Ground(Vector2 p)
  {
   // Smooth approaches on all edges, including the side lane joining mid.
   float across=Mathf.Clamp01(Mathf.Min((p.x-39)/3,(51-p.x)/3));
   float along=Mathf.Clamp01(Mathf.Min((p.y-48)/6,(70-p.y)/8));
   return 1.2f*across*along;
  }
  public void Add(Rect area,float bottom,float top,bool groundObstacle=false)
  {
   Solids.Add(new Solid{area=area,bottom=bottom,top=top});
   if(groundObstacle)GroundObstacles.Add(Rect.MinMaxRect(area.xMin-.66f,area.yMin-.66f,area.xMax+.66f,area.yMax+.66f));
  }
  static bool Slab(float origin,float delta,float min,float max,ref float enter,ref float exit)
  {
   if(Mathf.Abs(delta)<.000001f)return origin>=min&&origin<=max;
   float a=(min-origin)/delta,b=(max-origin)/delta;if(a>b){float t=a;a=b;b=t;}
   enter=Mathf.Max(enter,a);exit=Mathf.Min(exit,b);return enter<=exit;
  }
  public static bool Intersects(Rect r,float bottom,float top,Vector2 a,float ay,Vector2 b,float by)
  {
   float enter=0,exit=1;return Slab(a.x,b.x-a.x,r.xMin,r.xMax,ref enter,ref exit)&&Slab(a.y,b.y-a.y,r.yMin,r.yMax,ref enter,ref exit)&&Slab(ay,by-ay,bottom,top,ref enter,ref exit);
  }
  public bool Sight(Vector2 a,float ay,Vector2 b,float by)
  {
   foreach(var s in Solids)if(Intersects(s.area,s.bottom,s.top,a,ay,b,by))return false;return true;
  }
  public bool GroundClear(Vector2 a,Vector2 b)
  {
   foreach(var r in GroundObstacles)if(Intersects(r,-1,10,a,0,b,0))return false;return true;
  }
 }
 public sealed partial class CombatSystem
 {
  public Func<int,float> FeetHeight;
  public Func<int,int,float,bool> HeightShotClear;
  readonly float[] aimElevation=new float[10];
  public float AimElevation(int i){return FeetHeight==null?0:aimElevation[i];}
  bool TrackElevation(int i,int target,float distance,float dt,int aim)
  {
   if(FeetHeight==null)return true;
   float desired=Mathf.Atan2(FeetHeight(target)-FeetHeight(i)-.65f,Mathf.Max(.01f,distance))/Mathf.Deg2Rad;
   float change=(65+Mathf.Clamp01(aim/100f)*95)*dt;
   aimElevation[i]+=Mathf.Clamp(desired-aimElevation[i],-change,change);
   return Mathf.Abs(desired-aimElevation[i])<settings.fireAlignmentDegrees;
  }
  float ElevationError(int shooter,int target,float distance)
  {
   if(FeetHeight==null)return 0;
   return FeetHeight(shooter)+.65f+Mathf.Tan(aimElevation[shooter]*Mathf.Deg2Rad)*distance-FeetHeight(target);
  }
 }
}
