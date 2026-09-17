#if UNITY_EDITOR && UNITY_5_3_OR_NEWER
using System;
using System.IO;
using System.Reflection;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class BombVisualChecks
{
 public static void Run()
 {
  BombEquipmentChecks.Run();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var g=new GameObject("Kit visual").AddComponent<Prototype>();g.Initialize();g.StartMatch();g.AdvanceFrame(3.01f);
  g.MatchState(0).defuseKit=true;var f=BindingFlags.Instance|BindingFlags.NonPublic;
  typeof(Prototype).GetMethod("DropBombEquipmentOnDeath",f).Invoke(g,new object[]{0});typeof(Prototype).GetMethod("SyncBombEquipmentVisuals",f).Invoke(g,null);
  var kits=(System.Collections.Generic.List<GameObject>)typeof(Prototype).GetField("kitDummies",f).GetValue(g);if(kits.Count!=1||!kits[0].activeSelf)throw new Exception("Kit model missing");
  var camera=new GameObject("Kit capture").AddComponent<Camera>();camera.transform.position=kits[0].transform.position+new Vector3(1,1.8f,-1);camera.transform.LookAt(kits[0].transform.position);camera.fieldOfView=38;camera.cullingMask=~((1<<10)|(1<<12)|(1<<13));
  var output=Environment.GetEnvironmentVariable("BOMB_QA_OUTPUT")??Path.Combine(Path.GetTempPath(),"esports-bomb-captures");Directory.CreateDirectory(output);
  typeof(CharacterModelSetup).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{camera,Path.Combine(output,"kit-ground.png"),800,600});
  Debug.Log("BOMB_VISUAL_OK kit ground dummy and equipment checks");PreAimChecks.Run();
 }
}
#endif
