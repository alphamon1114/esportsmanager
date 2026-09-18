#if UNITY_EDITOR && UNITY_5_3_OR_NEWER
using System;using System.IO;using System.Reflection;using System.Collections.Generic;using UnityEngine;using UnityEditor.SceneManagement;using FpsManager;
public static class InfernoVisualChecks {
 public static void Run(){
  SourceMapImport.BuildInferno();
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var g=new GameObject("Inferno visual").AddComponent<Prototype>();
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;typeof(Prototype).GetMethod("Start",flags).Invoke(g,null);
  if(!g.SourceMapActive||g.ActiveMapId!="de_inferno")throw new Exception("normal startup did not load public Inferno");
  typeof(Prototype).GetField("menuPage",flags).SetValue(g,0);g.StartMatch();g.AdvanceFrame(Prototype.BuySeconds+.01f);
  var arena=SourceArena.Load("de_inferno");var actors=(List<GameObject>)typeof(Prototype).GetField("actors",flags).GetValue(g);var eye=(Camera)typeof(Prototype).GetField("eyeCamera",flags).GetValue(g);var radar=(Camera)typeof(Prototype).GetField("mapCamera",flags).GetValue(g);
  var capture=typeof(CharacterModelSetup).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static);string output=Environment.GetEnvironmentVariable("INFERNO_QA_OUTPUT");Directory.CreateDirectory(output);
  var points=new[]{arena.Sites[0],arena.Sites[1],arena.Mid,arena.T,arena.CT,arena.Seed(900,1000,100),arena.Seed(1100,1100,140)};string[] names={"a-site","b-site","mid","t-spawn","ct-spawn","mid-street","mid-upper"};
  for(int n=0;n<points.Length;n++){actors[0].transform.position=points[n]+Vector3.up;var target=n<2?SourceArena.At(g.Layout.Approaches[n][0],points[n].y):arena.Mid;var delta=target-points[n];delta.y=0;if(n==2)delta=arena.T-points[n];if(n>=5)delta=arena.Sites[0]-points[n];delta.y=0;if(delta.sqrMagnitude<1)delta=Vector3.forward;actors[0].transform.rotation=Quaternion.LookRotation(delta);g.Select(0);capture.Invoke(null,new object[]{eye,Path.Combine(output,names[n]+".png"),1200,800});}
  capture.Invoke(null,new object[]{radar,Path.Combine(output,"radar.png"),900,900});
  Debug.Log("INFERNO_VISUAL_OK startup, both sites, mid, CT/T spawns and radar; verts="+arena.Mesh.vertices.Length+" tris="+arena.Mesh.triangles.Length/3+" nav="+arena.Areas.Count);
 }
}
#endif
