#if UNITY_EDITOR && UNITY_5_3_OR_NEWER
using System;
using System.IO;
using System.Reflection;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class KnifeVisualChecks
{
 public static void Run()
 {
  TravelWeaponChecks.Run();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var g=new GameObject("Knife preview").AddComponent<Prototype>();g.Initialize();g.Select(0);
  var flags=BindingFlags.NonPublic|BindingFlags.Instance;
  var model=(GameObject)typeof(Prototype).GetField("equipmentModel",flags).GetValue(g);
  if(model.GetComponent<CharacterAnimation>().VisualWeapon!="weapon_knife")throw new Exception("Wrong first person model");
  foreach(var a in g.GetComponentsInChildren<Animator>())a.Update(.2f);
  var eye=(Camera)typeof(Prototype).GetField("eyeCamera",flags).GetValue(g);var output=Environment.GetEnvironmentVariable("KNIFE_QA_OUTPUT")??Path.Combine(Path.GetTempPath(),"esports-knife-captures");Directory.CreateDirectory(output);
  typeof(CharacterModelSetup).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{eye,Path.Combine(output,"knife-pov.png"),1000,750});
  Debug.Log("KNIFE_VISUAL_OK first person and lobby knife");PreAimChecks.Run();
 }
}

#endif
