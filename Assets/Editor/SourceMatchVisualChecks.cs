#if UNITY_EDITOR && UNITY_5_3_OR_NEWER
using System;using System.IO;using System.Reflection;using System.Collections.Generic;using FpsManager;using UnityEngine;using UnityEditor.SceneManagement;
public static class SourceMatchVisualChecks {
 public static void Run(){
  string output=Environment.GetEnvironmentVariable("MAPS_QA_OUTPUT")??Path.Combine(Path.GetTempPath(),"esports-map-qa");Directory.CreateDirectory(output);
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;var capture=typeof(CharacterModelSetup).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static);
  foreach(var id in new[]{"de_inferno","de_dust2","de_mirage","de_nuke","de_vertigo"}){
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var game=new GameObject(id+" QA").AddComponent<Prototype>();game.Initialize();
   typeof(Prototype).GetMethod("ActivateMatchMap",flags).Invoke(game,new object[]{id});game.StartMatch();game.AdvanceFrame(Prototype.BuySeconds+.01f);var arena=SourceArena.Load(id);
   var actors=(List<GameObject>)typeof(Prototype).GetField("actors",flags).GetValue(game);actors[0].transform.position=arena.Sites[0]+Vector3.up;var at=game.Layout.Approaches[0][0];actors[0].transform.rotation=Quaternion.LookRotation(new Vector3(at.x-actors[0].transform.position.x,0,100-at.y-actors[0].transform.position.z));game.Select(0);
   var eye=(Camera)typeof(Prototype).GetField("eyeCamera",flags).GetValue(game);capture.Invoke(null,new object[]{eye,Path.Combine(output,id+"-pov.png"),1200,800});
   var radar=(Camera)typeof(Prototype).GetField("mapCamera",flags).GetValue(game);capture.Invoke(null,new object[]{radar,Path.Combine(output,id+"-radar.png"),800,800});
   if(id=="de_nuke"||id=="de_vertigo"){typeof(Prototype).GetMethod("SetSourceRadar",flags).Invoke(game,new object[]{true});capture.Invoke(null,new object[]{radar,Path.Combine(output,id+"-lower.png"),800,800});}
   Debug.Log("SOURCE_MATCH_VISUAL_OK "+id+" spawn/actual mesh/site POV/radar");
  }
 }
}
#endif
