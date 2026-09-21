using System;using System.IO;using System.Collections.Generic;using UnityEngine;
namespace FpsManager {
 // Runtime source geometry and directed, height-aware navigation. All coordinates remain metres.
 public sealed class SourceArena {
  public readonly string Id;public readonly SourceMapData Mesh;public readonly Dictionary<int,SourceMapData.Area> Areas;
  public Vector3 Offset,CT,T,Mid;public Vector3[] Sites;public float Span;
  readonly Dictionary<long,List<SourceMapData.Area>> cells=new Dictionary<long,List<SourceMapData.Area>>();
  static long Cell(int x,int z){return ((long)x<<32)^(uint)z;}
  readonly List<SourceMapData.Area> walk=new List<SourceMapData.Area>();
  sealed class Node {public Vector3 lo,hi;public int start,count;public Node a,b;}
  readonly int[] triangles;Node root;
  static readonly Dictionary<string,SourceArena> cache=new Dictionary<string,SourceArena>();
  public static SourceArena Load(string id){SourceArena result;if(cache.TryGetValue(id,out result))return result;result=new SourceArena(id);cache[id]=result;return result;}
  static string PathFor(string id,string ext){
#if UNITY_5_3_OR_NEWER
   string root=Application.streamingAssetsPath+"/PublicMaps/";if(!File.Exists(root+id+ext))root=Application.dataPath+"/MapSources/2000908/";
#else
   string root="Assets/MapSources/2000908/";
#endif
   return root+id+ext;
  }
#if UNITY_WEBGL && !UNITY_EDITOR
  static byte[] WebBytes(string id,string extension){
   var asset=Resources.Load<TextAsset>("PublicMaps/"+id+extension);
   if(asset==null)throw new InvalidOperationException("Missing bundled map: "+id+extension);
   var bytes=asset.bytes;Resources.UnloadAsset(asset);return bytes;
  }
#endif
  SourceArena(string id){
   Id=id;
#if UNITY_WEBGL && !UNITY_EDITOR
   Mesh=SourceMapData.ReadMesh(WebBytes(id,".awmh"));Areas=SourceMapData.ReadNav(WebBytes(id,".nav"));
#else
   Mesh=SourceMapData.ReadMesh(PathFor(id,".awmh"));Areas=SourceMapData.ReadNav(PathFor(id,".nav"));
#endif
   Vector3 lo=new Vector3(float.MaxValue,float.MaxValue,float.MaxValue),hi=new Vector3(float.MinValue,float.MinValue,float.MinValue);
   foreach(var a in Areas.Values)if(a.hull==0){walk.Add(a);foreach(var p in a.corners){lo=Min(lo,p);hi=Max(hi,p);}}
   Offset=new Vector3(50-(lo.x+hi.x)/2,-lo.y,50-(lo.z+hi.z)/2);Span=Math.Max(hi.x-lo.x,hi.z-lo.z);
   var shifted=new HashSet<Vector3[]>();foreach(var a in Areas.Values)if(shifted.Add(a.corners))for(int n=0;n<a.corners.Length;n++)a.corners[n]+=Offset;
   for(int n=0;n<Mesh.vertices.Length;n++)Mesh.vertices[n]+=Offset;
   foreach(var a in walk){float x0=float.MaxValue,z0=float.MaxValue,x1=float.MinValue,z1=float.MinValue;foreach(var p in a.corners){x0=Math.Min(x0,p.x);x1=Math.Max(x1,p.x);z0=Math.Min(z0,p.z);z1=Math.Max(z1,p.z);}for(int x=(int)Math.Floor(x0/4);x<=(int)Math.Floor(x1/4);x++)for(int z=(int)Math.Floor(z0/4);z<=(int)Math.Floor(z1/4);z++){long key=Cell(x,z);List<SourceMapData.Area> bucket;if(!cells.TryGetValue(key,out bucket)){bucket=new List<SourceMapData.Area>();cells[key]=bucket;}bucket.Add(a);}}

   triangles=new int[Mesh.triangles.Length/3];for(int n=0;n<triangles.Length;n++)triangles[n]=n;root=Build(0,triangles.Length);
   // Anchor seeds from public observer/spawn coordinates, snapped onto this NAV revision.
   if(id=="de_inferno"){CT=Seed(2353,1977,199);T=Seed(-1650,718,0);Sites=new[]{Seed(2060.5105f,422.71582f,160.03125f),Seed(176.47f,2768.02f,164.03f)};Mid=Seed(1300,800,128); /* Mid corridor; old seed snapped from a building to a tight corner. */}
   else if(id=="de_dust2"){CT=Seed(235,2250,-120);T=Seed(-800,-800,120);Sites=new[]{Seed(1120,2450,100),Seed(-1600,2650,35)};Mid=Seed(-450,1400,0);}
   else if(id=="de_mirage"){CT=Seed(-1776,-1976,-202);T=Seed(1296,-160,-104);Sites=new[]{Seed(-400,-2000,-170),Seed(-2000,230,-160)};Mid=Seed(-350,-650,-165);}
   else if(id=="de_nuke"){CT=Seed(2552,-424,-288);T=Seed(-1808,-1089,-352);Sites=new[]{Seed(650,-550,-415),Seed(650,-1000,-775)};Mid=Seed(600,-1600,-415);}
   else if(id=="de_vertigo"){CT=Seed(-930,780,11840);T=Seed(-1332,-1453,11552);Sites=new[]{Seed(-450,-500,11776),Seed(-2200,650,11776)};Mid=Seed(-1400,-100,11776);}
   else throw new ArgumentException("Unknown source map");
  }
  public Vector3 Seed(float x,float y,float z){return Snap(SourceMapData.ToUnity(new Vector3(x,y,z))+Offset);}
  public static Vector2 Flat(Vector3 p){return new Vector2(p.x,100-p.z);}
  public static Vector3 At(Vector2 p,float y){return new Vector3(p.x,y,100-p.y);}
  static Vector3 Min(Vector3 a,Vector3 b){return new Vector3(Math.Min(a.x,b.x),Math.Min(a.y,b.y),Math.Min(a.z,b.z));}
  static Vector3 Max(Vector3 a,Vector3 b){return new Vector3(Math.Max(a.x,b.x),Math.Max(a.y,b.y),Math.Max(a.z,b.z));}
  static float Axis(Vector3 p,int axis){return axis==0?p.x:axis==1?p.y:p.z;}
  Vector3 Vertex(int triangle,int corner){return Mesh.vertices[Mesh.triangles[triangle*3+corner]];}
  Node Build(int start,int count){
   var n=new Node{start=start,count=count,lo=new Vector3(float.MaxValue,float.MaxValue,float.MaxValue),hi=new Vector3(float.MinValue,float.MinValue,float.MinValue)};
   for(int j=start;j<start+count;j++)for(int k=0;k<3;k++){var p=Vertex(triangles[j],k);n.lo=Min(n.lo,p);n.hi=Max(n.hi,p);}
   if(count>16){var size=n.hi-n.lo;int axis=size.x>size.y?(size.x>size.z?0:2):(size.y>size.z?1:2);
    Array.Sort(triangles,start,count,Comparer<int>.Create((a,b)=>Axis(Vertex(a,0)+Vertex(a,1)+Vertex(a,2),axis).CompareTo(Axis(Vertex(b,0)+Vertex(b,1)+Vertex(b,2),axis))));
    int half=count/2;n.a=Build(start,half);n.b=Build(start+half,count-half);
   }return n;
  }
  static bool BoxHit(Node n,Vector3 origin,Vector3 delta){float first=0,last=1;for(int k=0;k<3;k++){
   float o=Axis(origin,k),d=Axis(delta,k),lo=Axis(n.lo,k),hi=Axis(n.hi,k);
   if(Math.Abs(d)<.000001f){if(o<lo||o>hi)return false;continue;}
   float a=(lo-o)/d,b=(hi-o)/d;if(a>b){float t=a;a=b;b=t;}first=Math.Max(first,a);last=Math.Min(last,b);if(first>last)return false;
  }return true;}
  bool Hit(Node n,Vector3 origin,Vector3 delta){
   if(!BoxHit(n,origin,delta))return false;if(n.a!=null)return Hit(n.a,origin,delta)||Hit(n.b,origin,delta);
   for(int j=n.start;j<n.start+n.count;j++){
    int tri=triangles[j];var a=Vertex(tri,0);var e1=Vertex(tri,1)-a;var e2=Vertex(tri,2)-a;var h=Vector3.Cross(delta,e2);float det=Vector3.Dot(e1,h);if(Math.Abs(det)<.0000001f)continue;
    float inv=1/det;var s=origin-a;float u=inv*Vector3.Dot(s,h);if(u<0||u>1)continue;var q=Vector3.Cross(s,e1);float v=inv*Vector3.Dot(delta,q);if(v<0||u+v>1)continue;float t=inv*Vector3.Dot(e2,q);if(t>.0001f&&t<.9999f)return true;
   }return false;
  }
  public bool Sight(Vector3 a,Vector3 b){return !Hit(root,a,b-a);}
  static Vector3 OnSegment(Vector3 p,Vector3 a,Vector3 b){var d=b-a;float length=Vector3.Dot(d,d);return a+d*(length<.00001f?0:Mathf.Clamp01(Vector3.Dot(p-a,d)/length));}
  public static Vector3 Project(SourceMapData.Area area,Vector3 point){
   var c=area.corners;bool pos=false,neg=false;float best=float.MaxValue;Vector3 nearest=c[0];
   for(int i=0;i<c.Length;i++){var a=c[i];var b=c[(i+1)%c.Length];float cross=(b.x-a.x)*(point.z-a.z)-(b.z-a.z)*(point.x-a.x);pos|=cross>.0001f;neg|=cross<-.0001f;
    var flat=OnSegment(new Vector3(point.x,0,point.z),new Vector3(a.x,0,a.z),new Vector3(b.x,0,b.z));float d=(flat.x-point.x)*(flat.x-point.x)+(flat.z-point.z)*(flat.z-point.z);if(d<best){best=d;float length=(b.x-a.x)*(b.x-a.x)+(b.z-a.z)*(b.z-a.z);float t=length<.00001f?0:((flat.x-a.x)*(b.x-a.x)+(flat.z-a.z)*(b.z-a.z))/length;nearest=new Vector3(flat.x,a.y+(b.y-a.y)*t,flat.z);}}
   if(!(pos&&neg)){var normal=Vector3.Cross(c[1]-c[0],c[2]-c[0]);float y=Math.Abs(normal.y)<.0001f?area.Center.y:c[0].y-(normal.x*(point.x-c[0].x)+normal.z*(point.z-c[0].z))/normal.y;return new Vector3(point.x,y,point.z);}return nearest;
  }
  public SourceMapData.Area Nearest(Vector3 point){
   SourceMapData.Area best=null;float score=float.MaxValue;int cx=(int)Math.Floor(point.x/4),cz=(int)Math.Floor(point.z/4);
   for(int ring=0;ring<5;ring++){
    for(int x=cx-ring;x<=cx+ring;x++)for(int z=cz-ring;z<=cz+ring;z++){if(ring>0&&Math.Abs(x-cx)!=ring&&Math.Abs(z-cz)!=ring)continue;List<SourceMapData.Area> bucket;if(!cells.TryGetValue(Cell(x,z),out bucket))continue;foreach(var a in bucket){var delta=Project(a,point)-point;float d=Vector3.Dot(delta,delta);if(d<score){score=d;best=a;}}}
    if(best!=null&&score<ring*ring*16)return best;
   }
   if(best!=null)return best;foreach(var a in walk){var delta=Project(a,point)-point;float d=Vector3.Dot(delta,delta);if(d<score){score=d;best=a;}}return best;
  }
  public Vector3 Snap(Vector3 p){return Project(Nearest(p),p);}
  public float Floor(Vector2 p,float hint){return Snap(At(p,hint)).y;}
  public bool WalkClear(Vector3 a,Vector3 b){
   if(Math.Abs(a.y-b.y)>1.4f||!Sight(a+Vector3.up*.8f,b+Vector3.up*.8f))return false;
   float distance=(b-a).magnitude;int count=Math.Max(1,(int)(distance/.65f));int last=-1;
   for(int n=0;n<=count;n++){var p=a+(b-a)*(n/(float)count);var area=Nearest(p);var snap=Project(area,p);if((snap-p).magnitude>.5f)return false;
    if(last>=0&&area.id!=last&&Array.IndexOf(Areas[last].connections,area.id)<0&&Array.IndexOf(area.connections,last)<0)return false;last=area.id;
   }return true;
  }
  public List<Vector3> Route(Vector3 from,Vector3 to){
   var source=Nearest(from);var goal=Nearest(to);var ids=SourceMapData.Route(Areas,source.id,goal.id);if(ids.Count==0)throw new InvalidOperationException(Id+": disconnected nav endpoints");
   var path=new List<Vector3>();
   for(int i=0;i<ids.Count-1;i++){
    var a=Areas[ids[i]];var b=Areas[ids[i+1]];Vector3 pa=a.Center,pb=b.Center;float score=float.MaxValue;var middle=(a.Center+b.Center)*.5f;
    for(int x=0;x<a.corners.Length;x++)for(int y=0;y<b.corners.Length;y++){
     var ca=OnSegment(middle,a.corners[x],a.corners[(x+1)%a.corners.Length]);var cb=OnSegment(ca,b.corners[y],b.corners[(y+1)%b.corners.Length]);ca=OnSegment(cb,a.corners[x],a.corners[(x+1)%a.corners.Length]);
     float d=(ca-cb).magnitude+(ca-middle).magnitude*.0001f;if(d<score){score=d;pa=ca;pb=cb;}
    }
    path.Add(a.Center);path.Add(pa);if((pb-pa).magnitude>.05f)path.Add(pb);
   }
   path.Add(goal.Center);path.Add(Project(goal,to));return path;
  }
 }
}
