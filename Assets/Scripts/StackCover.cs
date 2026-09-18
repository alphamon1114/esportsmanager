using System;using System.Collections.Generic;using UnityEngine;
namespace FpsManager {
 public partial class Prototype {
  void BuildStackCover(){
   layout.StackPost=new Vector2[2][];layout.StackPeek=new Vector2[2][];
   for(int site=0;site<2;site++){
    var center=sourceArena.Sites[site];var candidates=new List<Vector3>();
    // Geometry only: no opponent positions are used to choose defensive posts.
    for(float x=-14;x<=14;x+=1.5f)for(float z=-14;z<=14;z+=1.5f){
     var raw=center+new Vector3(x,0,z);var p=sourceArena.Snap(raw);
     if((p-raw).magnitude>.6f||Math.Abs(p.y-center.y)>1.2f)continue;
     if(!sourceArena.Sight(p+Vector3.up*.15f,p+Vector3.up*1.15f))continue;
     candidates.Add(p);
    }
    var posts=new Vector3[5];layout.StackPost[site]=new Vector2[5];layout.StackPeek[site]=new Vector2[5];
    for(int n=0;n<5;n++){
     float best=float.MinValue;Vector3 chosen=center,chosenPeek=center;bool found=false;
     foreach(var p in candidates){
      bool spaced=true;for(int j=0;j<n;j++)if((p-posts[j]).magnitude<2.2f)spaced=false;if(!spaced)continue;
      var approach=SourceArena.At(layout.Approaches[site][n%2],GoalFloor(layout.Approaches[site][n%2],center.y));
      bool exposed=sourceArena.Sight(p+Vector3.up*1.65f,approach+Vector3.up*1.65f);
      int hidden=0;foreach(var a in layout.Approaches[site])if(!sourceArena.Sight(p+Vector3.up*1.1f,SourceArena.At(a,GoalFloor(a,center.y)+1.65f)))hidden++;
      int walls=0;for(int d=0;d<4;d++){var side=new Vector3(d<2?(d==0?1:-1):0,0,d>=2?(d==2?1:-1):0);if(!sourceArena.Sight(p+Vector3.up*.8f,p+Vector3.up*.8f+side*1.8f))walls++;}
      Vector3 peek=p;bool canPeek=false;
      for(int d=0;d<8;d++){float a=d*Mathf.PI/4;var edge=sourceArena.Snap(p+new Vector3(Mathf.Cos(a)*2.2f,0,Mathf.Sin(a)*2.2f));if((edge-p).magnitude<.7f||!sourceArena.WalkClear(p,edge))continue;if(sourceArena.Sight(edge+Vector3.up*1.65f,approach+Vector3.up*1.65f)){peek=edge;canPeek=true;break;}}
      float score=(n<2?(exposed?30:-30):(hidden*60+(canPeek?20:-20)))+Math.Min(walls,2)*8-(p-center).magnitude*.8f;
      if(score<=best)continue;
      try{var path=sourceArena.Route(center,p);float length=0;for(int k=1;k<path.Count;k++)length+=(path[k]-path[k-1]).magnitude;if(length>55)continue;}catch(InvalidOperationException){continue;}
      best=score;chosen=p;chosenPeek=n<2?approach:peek;
      if(n>=2&&!canPeek){try{chosenPeek=PointAlong(sourceArena.Route(p,approach),2.5f);}catch(InvalidOperationException){chosenPeek=approach;}}
      found=true;
     }
     if(!found){chosen=SourceArena.At(layout.HoldRing[site][n],center.y);chosenPeek=SourceArena.At(layout.Approaches[site][n%2],center.y);}
     posts[n]=chosen;layout.StackPost[site][n]=Anchor(chosen);layout.StackPeek[site][n]=Anchor(chosenPeek);
    }
   }
  }
  bool StepStackPost(int i,PlayerObjective order,float dt){
   if(!order.stackHold||combat.Engaging(i))return false;
   peeking[i].Cancel();clutchSearch[i].Cancel();
   float distance=Vector2.Distance(MapPosition(i),order.destination);
   tacticalCrouch[i]=order.stackHidden&&distance<.65f;
   if(distance>.35f){MoveTo(i,order.destination);var before=MapPosition(i);if(routeValid[i]&&routeSteps[i]<routes[i].Count)StepRoute(i,dt);moving[i]=Vector2.Distance(before,MapPosition(i))>.001f;}
   else {moving[i]=false;moveSpeed[i]=0;}
   arrived[i]=!moving[i];
   // Keep the exit of cover in view, rather than alternating unrelated approach angles.
   var point=MapPosition(i)+order.watch;var visible=navigation.VisibleAimPoint(MapPosition(i),point);
   FaceWatch(i,SharedAimDirection(i,Vector2.Distance(visible,MapPosition(i))>.5f?visible:point),dt);
   return true;
  }
 }
}
