using System;using System.IO;using System.Collections.Generic;using UnityEngine;
namespace FpsManager {
 // Public format references: awpy-data AWMH v1 and ValveResourceFormat/Awpy Source2 nav.
 // Keep original Hammer coordinates on disk. Unity conversion is uniform, no 100x100 flattening.
 public sealed class SourceMapData {
  public Vector3[] vertices;public int[] triangles;
  public static Vector3 ToUnity(Vector3 p){return new Vector3(p.x,p.z,p.y)*.0254f;}
  static int Count(BinaryReader r,int max){uint n=r.ReadUInt32();if(n>max)throw new InvalidDataException("Map count exceeds limit");return(int)n;}
  static Vector3 Point(BinaryReader r){var p=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z))throw new InvalidDataException("Nonfinite coordinate");return p;}
  public static SourceMapData ReadMesh(string path){return ReadMesh(File.OpenRead(path));}
  public static SourceMapData ReadMesh(byte[] data){return ReadMesh(new MemoryStream(data,false));}
  static SourceMapData ReadMesh(Stream input){using(var r=new BinaryReader(input)){
   if(r.ReadUInt32()!=0x484d5741||r.ReadUInt32()!=1)throw new InvalidDataException("Expected AWMH v1");
   int nv=Count(r,5000000),nt=Count(r,10000000);if(r.BaseStream.Length!=16L+nv*12L+nt*12L)throw new InvalidDataException("Invalid mesh length");
   var m=new SourceMapData{vertices=new Vector3[nv],triangles=new int[nt*3]};for(int i=0;i<nv;i++)m.vertices[i]=ToUnity(Point(r));
   for(int i=0;i<m.triangles.Length;i++){int n=Count(r,nv);if(n>=nv)throw new InvalidDataException("Bad triangle");m.triangles[i]=n;}
   // Swapping Y/Z reverses handedness.
   for(int i=0;i<m.triangles.Length;i+=3){int a=m.triangles[i+1];m.triangles[i+1]=m.triangles[i+2];m.triangles[i+2]=a;}return m;
  }}
  public sealed class Area {public int id,hull;public long flags;public Vector3[] corners;public int[] connections,laddersAbove,laddersBelow;public Vector3 Center {get{var p=new Vector3();foreach(var c in corners)p+=c;return p/corners.Length;}}}
  static int[] Ids(BinaryReader r){int n=Count(r,100000);var a=new int[n];for(int i=0;i<n;i++)a[i]=(int)r.ReadUInt32();return a;}
  // v36 embeds aligned KV3 v5 metadata. These CS2 releases use uncompressed,
  // blob-free blocks here. Reject other encodings instead of guessing byte offsets.
  static void SkipNavMetadata(BinaryReader r){
   r.BaseStream.Position=(r.BaseStream.Position+7)&~7L;long start=r.BaseStream.Position;
   if(r.ReadUInt32()!=0x4b563305)throw new InvalidDataException("Expected KV3 v5 metadata");
   r.ReadBytes(16);if(r.ReadUInt32()!=0)throw new InvalidDataException("Compressed nav metadata unsupported");
   r.BaseStream.Position=start+48;int size=r.ReadInt32();r.ReadInt32();int blocks=r.ReadInt32(),blobs=r.ReadInt32();
   if(size<0||blocks!=0||blobs!=0||start+120L+size>r.BaseStream.Length)throw new InvalidDataException("Invalid nav metadata length");
   r.BaseStream.Position=start+120+size;
  }
  public static Dictionary<int,Area> ReadNav(string path){return ReadNav(File.OpenRead(path));}
  public static Dictionary<int,Area> ReadNav(byte[] data){return ReadNav(new MemoryStream(data,false));}
  static Dictionary<int,Area> ReadNav(Stream input){using(var r=new BinaryReader(input)){
   if(r.ReadUInt32()!=0xfeedface)throw new InvalidDataException("Expected Source nav");int v=(int)r.ReadUInt32();if(v<30||v>36)throw new InvalidDataException("Unsupported nav version "+v);r.ReadUInt32();r.ReadUInt32();
   if(v>=36)SkipNavMetadata(r);Vector3[][] polygons=null;
   if(v>=31){int n=Count(r,5000000);var corners=new Vector3[n];for(int i=0;i<n;i++)corners[i]=ToUnity(Point(r));int np=Count(r,1000000);polygons=new Vector3[np][];for(int i=0;i<np;i++){int nc=r.ReadByte();polygons[i]=new Vector3[nc];for(int k=0;k<nc;k++){int at=Count(r,n);if(at>=n)throw new InvalidDataException("Bad nav corner");polygons[i][k]=corners[at];}if(v>=35)r.ReadUInt32();}}
   if(v>=32)r.ReadUInt32();if(v>=35&&r.ReadUInt32()!=0)throw new InvalidDataException("Movable nav meshes unsupported");if(v>=36)SkipNavMetadata(r);int count=Count(r,1000000);var areas=new Dictionary<int,Area>();
   for(int i=0;i<count;i++){var a=new Area{id=(int)r.ReadUInt32(),flags=r.ReadInt64(),hull=r.ReadByte()};
    if(v>=31){int pi=Count(r,polygons.Length);if(pi>=polygons.Length)throw new InvalidDataException("Bad nav polygon");a.corners=polygons[pi];}
    else{int n=Count(r,256);a.corners=new Vector3[n];for(int k=0;k<n;k++)a.corners[k]=ToUnity(Point(r));}
    if(a.corners.Length<3)throw new InvalidDataException("Empty nav area");r.ReadUInt32();var links=new List<int>();for(int edge=0;edge<a.corners.Length;edge++){int n=Count(r,100000);for(int k=0;k<n;k++){links.Add((int)r.ReadUInt32());r.ReadUInt32();}}
    a.connections=links.ToArray();if(r.ReadByte()!=0||r.ReadUInt32()!=0)throw new InvalidDataException("Legacy hiding data unsupported");a.laddersAbove=Ids(r);a.laddersBelow=Ids(r);areas.Add(a.id,a);
   }
   foreach(var a in areas.Values)foreach(int id in a.connections)if(!areas.ContainsKey(id))throw new InvalidDataException("Dangling nav connection");return areas;
  }}
  // Directed graph route: never merge areas just because their X/Z projections overlap.
  // Ladder metadata is retained, but ladder traversal needs a separate movement controller.
  public static List<int> Route(Dictionary<int,Area> areas,int from,int to){
   var result=new List<int>();if(!areas.ContainsKey(from)||!areas.ContainsKey(to))return result;
   var parent=new Dictionary<int,int>();var q=new Queue<int>();parent[from]=from;q.Enqueue(from);
   while(q.Count>0){int at=q.Dequeue();if(at==to){while(at!=from){result.Add(at);at=parent[at];}result.Add(from);result.Reverse();return result;}foreach(int n in areas[at].connections)if(!parent.ContainsKey(n)&&areas[n].hull==areas[from].hull){parent[n]=at;q.Enqueue(n);}}
   return result;
  }
 }
}

