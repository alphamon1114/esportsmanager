using System;
using System.Collections.Generic;
using UnityEngine;
namespace FpsManager
{
 public sealed partial class DeploymentNavigation
 {
  struct QueueNode { public int cell;public float score; }
  static void Push(List<QueueNode> heap,int cell,float score)
  {
   heap.Add(new QueueNode{cell=cell,score=score});int i=heap.Count-1;
   while(i>0){int p=(i-1)/2;if(heap[p].score<=score)break;heap[i]=heap[p];i=p;}
   heap[i]=new QueueNode{cell=cell,score=score};
  }
  static int Pop(List<QueueNode> heap)
  {
   int result=heap[0].cell;var last=heap[heap.Count-1];heap.RemoveAt(heap.Count-1);
   if(heap.Count==0)return result;int i=0;
   while(i*2+1<heap.Count){int c=i*2+1;if(c+1<heap.Count&&heap[c+1].score<heap[c].score)c++;if(heap[c].score>=last.score)break;heap[i]=heap[c];i=c;}
   heap[i]=last;return result;
  }
  // Costs encode our own spawn and shared observations only; no enemy transforms.
  public List<Vector2> CombatRoute(Vector2 start,Vector2 destination,Vector2 spawn,List<Vector2> knownThreats)
  {
   if(RouteOverride!=null)return RouteOverride(start,destination);
   int source=Nearest(start,true),goal=Nearest(destination,false);
   if(source<0||goal<0)throw new InvalidOperationException("Combat route endpoint blocked");
   bool avoidSpawn=Vector2.Distance(start,spawn)>26&&Vector2.Distance(destination,spawn)>26;
   Func<Vector2,float> risk=p=>{
    float value=avoidSpawn&&Vector2.Distance(p,spawn)<24?5:0;
    foreach(var threat in knownThreats)if(Vector2.Distance(p,threat)<12)value+=2;
    return value;
   };
   var distance=new float[10000];var parent=new int[10000];var visited=new bool[10000];
   for(int i=0;i<distance.Length;i++){distance[i]=float.MaxValue;parent[i]=-1;}
   distance[source]=0;var heap=new List<QueueNode>();Push(heap,source,0);
   while(heap.Count>0)
   {
    int cell=Pop(heap);if(visited[cell])continue;visited[cell]=true;if(cell==goal)break;
    int x=cell%100,y=cell/100;
    foreach(int next in new[]{x>0?cell-1:-1,x<99?cell+1:-1,y>0?cell-100:-1,y<99?cell+100:-1})
    {
     if(next<0||!open[next]||visited[next]||!Clear(Point(cell),Point(next)))continue;
     float cost=distance[cell]+1+risk(Point(next));if(cost>=distance[next])continue;
     distance[next]=cost;parent[next]=cell;
     var d=Point(next)-Point(goal);Push(heap,next,cost+Mathf.Abs(d.x)+Mathf.Abs(d.y));
    }
   }
   if(!visited[goal])return Route(start,destination,new List<Vector2>());
   var raw=new List<Vector2>();for(int cell=goal;cell>=0;cell=parent[cell])raw.Add(Point(cell));raw.Reverse();
   var result=new List<Vector2>();Vector2 cursor=start;
   for(int i=0;i<raw.Count;)
   {
    int far=i;
    while(far+1<raw.Count&&Clear(cursor,raw[far+1]))
    {
     bool safe=true;float length=Vector2.Distance(cursor,raw[far+1]);
     for(float t=0;t<=length;t+=2)if(risk(Vector2.MoveTowards(cursor,raw[far+1],t))>0){safe=false;break;}
     if(!safe)break;far++;
    }
    result.Add(raw[far]);cursor=raw[far];i=far+1;
   }
   return result;
  }
 }
}