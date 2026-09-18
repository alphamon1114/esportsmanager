#if UNITY_EDITOR
using System;using System.IO;using UnityEngine;using UnityEditor;using UnityEditor.Build;using UnityEditor.Build.Reporting;using UnityEngine.Rendering;
public static class WebReleaseBuild {
 [MenuItem("FPS Manager/Build/itch.io Web release")]
 public static void Build(){
  if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL,BuildTarget.WebGL))throw new BuildFailedException("Install Web Build Support for this Unity version.");
  var output=Environment.GetEnvironmentVariable("ESPORTS_WEB_OUTPUT");if(string.IsNullOrEmpty(output))output=Path.GetFullPath("Builds/WebGL");
  Directory.CreateDirectory(output);PublicMapBuild.PrepareWebMaps();
  PlayerSettings.productName="Esports Manager Prototype";
  PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;PlayerSettings.WebGL.decompressionFallback=true;
  PlayerSettings.WebGL.dataCaching=true;PlayerSettings.WebGL.threadsSupport=false;
  PlayerSettings.WebGL.initialMemorySize=256;PlayerSettings.WebGL.maximumMemorySize=2048;
  PlayerSettings.WebGL.template="APPLICATION:Minimal";
  PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL,false);
  PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL,new[]{GraphicsDeviceType.OpenGLES3});
  PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL,ManagedStrippingLevel.Low);
  var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
  var list=graphics.FindProperty("m_AlwaysIncludedShaders");
  foreach(var name in new[]{"Unlit/Color","Standard","Sprites/Default","FpsManager/SourceMapPreview","FpsManager/SourceRadar"}){
   var shader=Shader.Find(name);if(shader==null)throw new BuildFailedException("Missing shader: "+name);
   bool found=false;for(int n=0;n<list.arraySize;n++)if(list.GetArrayElementAtIndex(n).objectReferenceValue==shader)found=true;
   if(!found){int n=list.arraySize;list.InsertArrayElementAtIndex(n);list.GetArrayElementAtIndex(n).objectReferenceValue=shader;}
  }
  graphics.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
  var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Prototype.unity"},locationPathName=output,target=BuildTarget.WebGL,options=BuildOptions.None});
  if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Web export failed: "+report.summary.result+" errors="+report.summary.totalErrors);
  Debug.Log("WEB_RELEASE_OK output="+output+" bytes="+report.summary.totalSize+" duration="+report.summary.totalTime);
 }
}
#endif
