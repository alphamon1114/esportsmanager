#if UNITY_EDITOR && UNITY_5_3_OR_NEWER
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class WeaponDropVisualChecks
{
 public static void Run()
 {
  WeaponDropChecks.Run();
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var g=new GameObject("Drop preview").AddComponent<Prototype>();g.Initialize();g.StartMatch();g.AdvanceFrame(Prototype.BuySeconds+.01f);
  var f=BindingFlags.NonPublic|BindingFlags.Instance;var actors=(List<GameObject>)typeof(Prototype).GetField("actors",f).GetValue(g);
  g.MatchState(0).equipment=new[]{"usp_s","ak_47"};g.Combat.EquipSaved(0,"ak_47",new WeaponAmmo{rounds=7,spares=1});g.DropWeapon(0);
  typeof(Prototype).GetMethod("SyncGroundWeaponVisuals",f).Invoke(g,null);
  var models=(System.Collections.IDictionary)typeof(Prototype).GetField("dropModels",f).GetValue(g);if(models.Count!=1)throw new Exception("Missing drop render");
  var drop=g.GroundWeaponAt(0);var camera=new GameObject("Drop overview").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.1f,.13f);camera.cullingMask=~((1<<10)|(1<<12)|(1<<13));
  camera.transform.position=drop.position+new Vector3(1.5f,2.4f,-2);camera.transform.LookAt(drop.position+Vector3.up*.12f);camera.fieldOfView=42;
  foreach(var animator in g.GetComponentsInChildren<Animator>())animator.Update(.2f);
  var output=Environment.GetEnvironmentVariable("DROP_QA_OUTPUT")??Path.Combine(Path.GetTempPath(),"esports-drop-captures");Directory.CreateDirectory(output);
  typeof(CharacterModelSetup).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{camera,Path.Combine(output,"ground-weapon.png"),1000,750});
  actors[1].transform.position=actors[0].transform.position;if(!g.TryPickUpWeapon(1,drop)||g.Combat.Magazine(1)!=7)throw new Exception("Actual Unity pickup failed");
  typeof(Prototype).GetMethod("SyncGroundWeaponVisuals",f).Invoke(g,null);
  if(models.Contains(drop))throw new Exception("Picked model remains");
  // Natural match smoke test: no injected positions, loadouts or objectives.
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);g=new GameObject("Natural drops").AddComponent<Prototype>();g.Initialize();g.StartMatch();int peak=0;
  for(int t=0;t<1800;t++){g.AdvanceFrame(.1f);peak=Math.Max(peak,g.GroundWeaponCount);}
  if(peak==0||g.CompletedRounds==0)throw new Exception("Natural match did not drop or finish");
  Debug.Log("DROP_VISUAL_OK ground model, saved ammo pickup, render cleanup; natural peak drops="+peak+" rounds="+g.CompletedRounds);
 }
}
#endif

