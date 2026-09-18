using System;using System.IO;using FpsManager;using UnityEngine;
public static class WebMapDataChecks {
 public static void Run(){
  foreach(var id in new[]{"de_inferno","de_dust2","de_mirage","de_nuke","de_vertigo"}){
   var path="Assets/MapSources/2000908/"+id;var a=SourceMapData.ReadMesh(path+".awmh");var b=SourceMapData.ReadMesh(File.ReadAllBytes(path+".awmh"));
   if(a.vertices.Length!=b.vertices.Length||a.triangles.Length!=b.triangles.Length)throw new Exception("Web mesh size mismatch");
   for(int i=0;i<a.vertices.Length;i++)if((a.vertices[i]-b.vertices[i]).magnitude>.00001f)throw new Exception("Web mesh vertex mismatch");
   var na=SourceMapData.ReadNav(path+".nav");var nb=SourceMapData.ReadNav(File.ReadAllBytes(path+".nav"));if(na.Count!=nb.Count)throw new Exception("Web NAV size mismatch");
   foreach(var pair in na){var x=pair.Value;var y=nb[pair.Key];if((x.Center-y.Center).magnitude>.00001f||x.connections.Length!=y.connections.Length)throw new Exception("Web NAV mismatch");for(int n=0;n<x.connections.Length;n++)if(x.connections[n]!=y.connections[n])throw new Exception("Web NAV link mismatch");}
   Debug.Log("WEB_MAP_BYTES_OK "+id);
  }
 }
}
