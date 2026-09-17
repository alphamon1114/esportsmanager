#if UNITY_EDITOR && UNITY_5_3_OR_NEWER
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor.SceneManagement;
using FpsManager;
public static class GunfireVisualChecks
{
 public static void Run()
 {
  GunfireChecks.Run();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var g=new GameObject("Gunfire visual QA").AddComponent<Prototype>();g.Initialize();g.BeginRound();
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var eye=(Camera)typeof(Prototype).GetField("eyeCamera",flags).GetValue(g);
  var actors=(System.Collections.Generic.List<GameObject>)typeof(Prototype).GetField("actors",flags).GetValue(g);
  string output=Environment.GetEnvironmentVariable("GUNFIRE_QA_OUTPUT")??Path.GetTempPath();Directory.CreateDirectory(output);
  var capture=typeof(CharacterModelSetup).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic);
  foreach(string id in new[]{"ak_47","awp","dual_berettas"})
  {
   g.Combat.Equip(0,WeaponCatalog.Find(id));g.Select(0);
   var arms=(GameObject)typeof(Prototype).GetField("equipmentModel",flags).GetValue(g);arms.GetComponentInChildren<Animator>().Update(.2f);
   typeof(Prototype).GetMethod("ResetShotPresentation",flags).Invoke(g,null);
   capture.Invoke(null,new object[]{eye,Path.Combine(output,id+"-before.png"),1000,600});
   Quaternion actorRotation=actors[0].transform.rotation,baseRotation=eye.transform.rotation;
   // Emit through the actual shot sampler; geometry and ammo remain game-owned.
   typeof(CombatSystem).GetMethod("Fire",flags).Invoke(g.Combat,new object[]{0,5,0f,10f,80});
   typeof(Prototype).GetMethod("UpdateShotPresentation",flags).Invoke(g,null);
   if(Vector3.Distance(baseRotation*Vector3.forward,eye.transform.forward)<.0001f||!actorRotation.Equals(actors[0].transform.rotation))throw new Exception("Camera kick missing or changed AI actor: camera="+Quaternion.Angle(baseRotation,eye.transform.rotation)+" actor="+Quaternion.Angle(actorRotation,actors[0].transform.rotation)+" kick="+typeof(Prototype).GetField("viewKick",flags).GetValue(g));
   var flashes=(GameObject[])typeof(Prototype).GetField("muzzleFlashes",flags).GetValue(g);
   if(flashes[0]==null||!flashes[0].activeSelf||flashes[0].layer!=13)throw new Exception("First-person flash missing");
   capture.Invoke(null,new object[]{eye,Path.Combine(output,id+"-shot.png"),1000,600});
   g.Select(1);if(((Vector3)typeof(Prototype).GetField("viewKick",flags).GetValue(g)).sqrMagnitude!=0)throw new Exception("Observer carried recoil");
   g.PrepareNextRound();if(flashes[0].activeSelf)throw new Exception("Flash survived reset");g.BeginRound();
  }
  Debug.Log("GUNFIRE_VISUAL_OK flash, camera kick, actor untouched, observer switch, round reset, three weapons rendered");
 }
}
#endif
