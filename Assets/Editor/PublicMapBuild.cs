#if UNITY_EDITOR
using System.IO;using UnityEditor;using UnityEditor.Build;using UnityEditor.Build.Reporting;
public sealed class PublicMapBuild:IPreprocessBuildWithReport {
 public static void PrepareWebMaps(){
  const string output="Assets/Resources/PublicMaps";Directory.CreateDirectory(output);
  foreach(var id in SourceMapImport.Maps)foreach(var ext in new[]{".nav",".awmh"})File.Copy("Assets/MapSources/2000908/"+id+ext,output+"/"+id+ext+".bytes",true);
  AssetDatabase.Refresh();
 }
 public int callbackOrder {get{return 0;}}
 public void OnPreprocessBuild(BuildReport report){
  if(report.summary.platform==BuildTarget.WebGL){PrepareWebMaps();return;}
  const string output="Assets/StreamingAssets/PublicMaps";Directory.CreateDirectory(output);
  foreach(var id in SourceMapImport.Maps)foreach(var ext in new[]{".nav",".awmh"})File.Copy("Assets/MapSources/2000908/"+id+ext,output+"/"+id+ext,true);
 }
}
#endif
