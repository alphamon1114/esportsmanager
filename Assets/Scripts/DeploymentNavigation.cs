using System;
using System.Collections.Generic;
using UnityEngine;

namespace FpsManager
{
    // One-unit navigation grid; obstacles include clearance for the capsule radius.
    public sealed class DeploymentNavigation
    {
        readonly List<Rect> obstacles = new List<Rect>();
        readonly bool[] open = new bool[10000];
        public DeploymentNavigation(List<Rect> geometry)
        {
            foreach (var r in geometry) obstacles.Add(Rect.MinMaxRect(r.xMin-.66f,r.yMin-.66f,r.xMax+.66f,r.yMax+.66f));
            for(int i=0;i<open.Length;i++) open[i]=Clear(Point(i),Point(i));
        }
        static Vector2 Point(int i) { return new Vector2(i%100+.5f,i/100+.5f); }
        public bool Clear(Vector2 a, Vector2 b)
        {
            if(a.x<.7f||a.y<.7f||a.x>99.3f||a.y>99.3f||b.x<.7f||b.y<.7f||b.x>99.3f||b.y>99.3f) return false;
            foreach(var r in obstacles)
            {
                float enter=0,exit=1; Vector2 delta=b-a;
                bool hit=true;
                for(int axis=0;axis<2;axis++)
                {
                    float origin=axis==0?a.x:a.y, d=axis==0?delta.x:delta.y;
                    float min=axis==0?r.xMin:r.yMin,max=axis==0?r.xMax:r.yMax;
                    if(Mathf.Abs(d)<.00001f) { if(origin<min||origin>max) { hit=false; break; } }
                    else
                    {
                        float near=(min-origin)/d,far=(max-origin)/d;
                        if(near>far) { float swap=near; near=far; far=swap; }
                        enter=Mathf.Max(enter,near); exit=Mathf.Min(exit,far);
                        if(enter>exit) { hit=false; break; }
                    }
                }
                if(hit) return false;
            }
            return true;
        }
        int Nearest(Vector2 point, bool visible)
        {
            int best=-1; float distance=float.MaxValue;
            for(int i=0;i<open.Length;i++) if(open[i])
            {
                float candidate=(Point(i)-point).sqrMagnitude;
                if(candidate<distance&&(!visible||Clear(point,Point(i)))) { distance=candidate; best=i; }
            }
            return best;
        }
        public List<Vector2> Route(Vector2 start,Vector2 destination,List<Vector2> reserved)
        {
            int source=Nearest(start,true);
            if(source<0) throw new InvalidOperationException("Spawn is inside an obstacle.");
            var parents=new int[10000]; for(int i=0;i<parents.Length;i++) parents[i]=-2;
            parents[source]=-1; var queue=new Queue<int>(); queue.Enqueue(source);
            int best=-1; float distance=float.MaxValue;
            while(queue.Count>0)
            {
                int cell=queue.Dequeue(); Vector2 point=Point(cell);
                bool available=true;
                foreach(var other in reserved) if((other-point).sqrMagnitude<4) { available=false; break; }
                if(available&&(point-destination).sqrMagnitude<distance) { distance=(point-destination).sqrMagnitude; best=cell; }
                int x=cell%100,y=cell/100;
                foreach(int next in new[]{x>0?cell-1:-1,x<99?cell+1:-1,y>0?cell-100:-1,y<99?cell+100:-1})
                    if(next>=0&&open[next]&&parents[next]==-2&&Clear(point,Point(next))) { parents[next]=cell; queue.Enqueue(next); }
            }
            if(best<0||distance>225) throw new InvalidOperationException("Deployment zone is unreachable.");
            var raw=new List<Vector2>(); for(int i=best;i!=-1;i=parents[i]) raw.Add(Point(i)); raw.Reverse();
            var result=new List<Vector2>(); Vector2 cursor=start;
            for(int i=0;i<raw.Count;)
            {
                int far=i; while(far+1<raw.Count&&Clear(cursor,raw[far+1])) far++;
                result.Add(raw[far]); cursor=raw[far]; i=far+1;
            }
            reserved.Add(Point(best)); return result;
        }
    }
}
