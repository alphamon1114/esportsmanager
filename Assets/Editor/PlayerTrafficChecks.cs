using System;using System.Reflection;using FpsManager;using UnityEngine;
public static class PlayerTrafficChecks {
 public static void Run(){
  foreach(int fps in new[]{60,144})foreach(int seed in new[]{1,42,123}){
   var g=new GameObject("Vertigo traffic").AddComponent<Prototype>();g.Initialize();typeof(Prototype).GetMethod("ActivateMatchMap",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(g,new object[]{"de_vertigo"});g.SetRoundSeed(seed);g.StartMatch();g.AdvanceFrame(Prototype.BuySeconds+.01f);
   var spawn=new Vector2[10];for(int i=0;i<10;i++)spawn[i]=g.MapPosition(i);
   for(int frame=0;frame<fps*15;frame++){
    g.AdvanceFrame(1f/fps);
    if(frame%12==0)for(int i=0;i<10;i++)for(int j=i+1;j<10;j++)if(g.IsAlive(i)&&g.IsAlive(j)&&g.PlayerHeight(i)+(g.IsCrouched(i)?1.2f:1.8f)>g.PlayerHeight(j)&&g.PlayerHeight(j)+(g.IsCrouched(j)?1.2f:1.8f)>g.PlayerHeight(i)&&Vector2.Distance(g.MapPosition(i),g.MapPosition(j))<.995f)throw new Exception("traffic overlap fps="+fps+" seed="+seed+" frame="+frame+" pair="+i+","+j+" distance="+Vector2.Distance(g.MapPosition(i),g.MapPosition(j))+" heights="+g.PlayerHeight(i)+","+g.PlayerHeight(j)+" crouch="+g.IsCrouched(i)+","+g.IsCrouched(j));
   }
   float minimum=float.MaxValue;for(int i=0;i<10;i++){float distance=Vector2.Distance(spawn[i],g.MapPosition(i));minimum=Math.Min(minimum,distance);if(distance<8)throw new Exception("Vertigo spawn jam fps="+fps+" seed="+seed+" player="+i+" distance="+distance);}
   Debug.Log("TRAFFIC_CASE_OK fps="+fps+" seed="+seed+" minSpawnDistance="+minimum);
  }
  Debug.Log("PLAYER_TRAFFIC_ALL_OK 60/144 FPS, six seed/timestep scenarios, all players leave spawn, body spacing preserved");
 }
}
