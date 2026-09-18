#if UNITY_EDITOR
using System;using System.IO;using System.Reflection;using System.Collections.Generic;using UnityEngine;using UnityEditor.SceneManagement;using FpsManager;
public static class BulletVisualChecks {public static void Run(){
 EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var g=new GameObject("trace QA").AddComponent<Prototype>();g.Initialize();var f=BindingFlags.Instance|BindingFlags.NonPublic;typeof(Prototype).GetMethod("ActivateMatchMap",f).Invoke(g,new object[]{"de_inferno"});g.StartMatch();g.AdvanceFrame(Prototype.BuySeconds+.01f);
 var actors=(List<GameObject>)typeof(Prototype).GetField("actors",f).GetValue(g);var arena=SourceArena.Load("de_inferno");actors[0].transform.position=arena.Sites[1]+Vector3.up;actors[0].transform.rotation=Quaternion.LookRotation(Vector3.forward);g.Select(0);var camera=(Camera)typeof(Prototype).GetField("eyeCamera",f).GetValue(g);
 var origin=camera.transform.position+camera.transform.right*2;var end=camera.transform.position+camera.transform.forward*16-camera.transform.right*3;
 typeof(Prototype).GetMethod("PresentBullet",f).Invoke(g,new object[]{1,origin,end});
 var lines=(LineRenderer[])typeof(Prototype).GetField("bulletLines",f).GetValue(g);if(lines[0]==null||!lines[0].enabled||lines[0].startWidth>.02f||!lines[0].sharedMaterial.shader.isSupported)throw new Exception("trace renderer invalid");
 var path=Environment.GetEnvironmentVariable("TRACE_QA_OUTPUT");Directory.CreateDirectory(path);typeof(CharacterModelSetup).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{camera,Path.Combine(path,"trace.png"),1200,800});
 typeof(Prototype).GetMethod("ResetBulletTraces",f).Invoke(g,null);if(lines[0].enabled)throw new Exception("trace survived reset");Debug.Log("TRACE_VISUAL_OK thin supported shader, render, reset");
}}
#endif
