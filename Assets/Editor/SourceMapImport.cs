#if UNITY_EDITOR
using System;using System.IO;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using FpsManager;
public static class SourceMapImport {
 public static readonly string[] Maps={"de_inferno","de_dust2","de_mirage","de_nuke","de_vertigo"};
 [MenuItem("FPS Manager/Maps/Build public map previews")]
 public static void Build(){BuildMaps(Maps);}
 public static void BuildInferno(){BuildMaps(new[]{"de_inferno"});}
 static void BuildMaps(string[] maps){
  string root="Assets/MapSources/2000908",output="Assets/Maps/PublicPreviews";Directory.CreateDirectory(output);
  foreach(string id in maps){
   var data=SourceMapData.ReadMesh(root+"/"+id+".awmh");var nav=SourceMapData.ReadNav(root+"/"+id+".nav");
   
   var go=new GameObject(id+" / public geometry preview");
   var mesh=new Mesh{name=id,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=data.vertices;mesh.triangles=data.triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
   string meshPath=output+"/"+id+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(existing!=null){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,meshPath);
   go.AddComponent<MeshFilter>().sharedMesh=mesh;var shader=Shader.Find("FpsManager/SourceMapPreview");var material=new Material(shader){color=new Color(.56f,.61f,.66f)};
   string matPath=output+"/"+id+".mat";var oldMat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(oldMat!=null){EditorUtility.CopySerialized(material,oldMat);UnityEngine.Object.DestroyImmediate(material);material=oldMat;}else AssetDatabase.CreateAsset(material,matPath);
   go.AddComponent<MeshRenderer>().sharedMaterial=material;go.AddComponent<MeshCollider>().sharedMesh=mesh;
   var navRoot=new GameObject("Navigation areas (select to inspect)");navRoot.transform.SetParent(go.transform);navRoot.AddComponent<SourceMapPreview>().sourceMap=id;
   var light=new GameObject("Preview sun");light.transform.SetParent(go.transform);var sun=light.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;light.transform.rotation=Quaternion.Euler(55,-35,0);
   var camera=new GameObject("Preview camera");camera.transform.SetParent(go.transform);var c=camera.AddComponent<Camera>();c.farClipPlane=2000;var b=mesh.bounds;float span=Mathf.Max(b.size.x,b.size.z);camera.transform.position=b.center+new Vector3(0,span,.05f);camera.transform.rotation=Quaternion.Euler(90,0,0);c.orthographic=true;c.orthographicSize=span*.55f;c.backgroundColor=new Color(.05f,.07f,.10f);c.clearFlags=CameraClearFlags.SolidColor;
   PrefabUtility.SaveAsPrefabAsset(go,output+"/"+id+".prefab");UnityEngine.Object.DestroyImmediate(go);
   Debug.Log("MAP_IMPORTED "+id+" vertices="+mesh.vertexCount+" triangles="+data.triangles.Length/3+" areas="+nav.Count);
  }AssetDatabase.SaveAssets();AssetDatabase.Refresh();
 }
}
#endif



