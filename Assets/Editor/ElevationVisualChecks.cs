#if UNITY_EDITOR && UNITY_5_3_OR_NEWER
using System;
using System.IO;
using System.Reflection;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class ElevationVisualChecks
{
 public static void Run()
 {
  ElevationChecks.Run();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var game=new GameObject("Elevation QA").AddComponent<Prototype>();game.Initialize();game.StartMatch();game.AdvanceFrame(Prototype.BuySeconds+.01f);
  string output=Environment.GetEnvironmentVariable("ELEVATION_QA_OUTPUT")??Path.GetTempPath();Directory.CreateDirectory(output);
  var flags=BindingFlags.NonPublic|BindingFlags.Instance;
  var capture=typeof(CharacterModelSetup).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static);
  var camera=new GameObject("Elevation overview").AddComponent<Camera>();camera.backgroundColor=new Color(.06f,.08f,.11f);camera.clearFlags=CameraClearFlags.SolidColor;camera.fieldOfView=53;camera.cullingMask=~((1<<10)|(1<<12)|(1<<13));
  camera.transform.position=new Vector3(106,20,47);camera.transform.LookAt(new Vector3(84,2,70));capture.Invoke(null,new object[]{camera,Path.Combine(output,"a-building.png"),1200,800});
  camera.transform.position=new Vector3(62,14,35);camera.transform.LookAt(new Vector3(45,0,42));capture.Invoke(null,new object[]{camera,Path.Combine(output,"mid-steps.png"),1200,800});
  var actors=(System.Collections.Generic.List<GameObject>)typeof(Prototype).GetField("actors",flags).GetValue(game);
  actors[0].transform.position=new Vector3(89,4,68);actors[5].transform.position=new Vector3(83,1,67);
  actors[0].transform.rotation=Quaternion.LookRotation(new Vector3(-6,0,-1));
  typeof(CombatSystem).GetMethod("TrackElevation",flags).Invoke(game.Combat,new object[]{0,5,Mathf.Sqrt(37),1f,85});
  game.Select(0);foreach(var animator in game.GetComponentsInChildren<Animator>())animator.Update(.2f);
  var eye=(Camera)typeof(Prototype).GetField("eyeCamera",flags).GetValue(game);
  capture.Invoke(null,new object[]{eye,Path.Combine(output,"balcony-pov.png"),1200,800});
  // Natural AI progression uses no injected objectives or actor positions in this second fixture.
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  game=new GameObject("Natural vertical match").AddComponent<Prototype>();game.Initialize();game.StartMatch();
  float peak=0;int elevatedTicks=0;float minHeight=0;
  for(int frame=0;frame<1400;frame++)
  {
   game.AdvanceFrame(.1f);
   for(int i=0;i<10;i++){float y=game.PlayerHeight(i);peak=Mathf.Max(peak,y);minHeight=Mathf.Min(minHeight,y);if(y>1.25f)elevatedTicks++;if(float.IsNaN(y))throw new Exception("NaN height");}
  }
  if(peak<2.9f||elevatedTicks==0||minHeight<-.01f||game.CompletedRounds<1)throw new Exception("Natural vertical match failed: peak="+peak+" rounds="+game.CompletedRounds);
  Debug.Log("ELEVATION_VISUAL_OK building, ramps, balcony POV; natural AI peak="+peak+" elevated ticks="+elevatedTicks+" completed rounds="+game.CompletedRounds);
 }
}
#endif
