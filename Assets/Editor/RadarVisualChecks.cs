#if UNITY_EDITOR && UNITY_5_3_OR_NEWER
using System;using System.IO;using System.Reflection;using FpsManager;using UnityEngine;using UnityEditor.SceneManagement;
public static class RadarVisualChecks {
 static readonly BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static object Field(Prototype g,string name){return typeof(Prototype).GetField(name,F).GetValue(g);}
 static void Call(Prototype g,string name,params object[] args){typeof(Prototype).GetMethod(name,F).Invoke(g,args);}
 static void Check(bool b,string message){if(!b)throw new Exception(message);}
 static Color32[] Capture(Prototype g,string path){
  var camera=(Camera)Field(g,"mapCamera");var target=camera.targetTexture;camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
  var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();var pixels=texture.GetPixels32();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);RenderTexture.active=previous;
  int visible=0;foreach(var p in pixels)if(p.r>55&&p.g>65&&p.b>75)visible++;
  Check(visible>pixels.Length*.025f,"blank radar: "+path+" visible="+visible);Debug.Log("RADAR_PIXELS_OK "+Path.GetFileName(path)+" visible="+visible);return pixels;
 }
 public static void Run(){
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var g=new GameObject("Radar checks").AddComponent<Prototype>();g.Initialize();
  string output=Environment.GetEnvironmentVariable("RADAR_QA_OUTPUT")??Path.Combine(Path.GetTempPath(),"esports-radar-qa");Directory.CreateDirectory(output);
  foreach(string id in new[]{"de_inferno","de_dust2","de_mirage","de_nuke","de_vertigo"}){
   Call(g,"ActivateMatchMap",id);var radar=(GameObject[])Field(g,"sourceRadar");bool floors=id=="de_nuke"||id=="de_vertigo";
   Check(radar[0].activeSelf&&radar[0].GetComponent<MeshFilter>().sharedMesh.vertexCount>0,"upper radar empty "+id);
   var upper=Capture(g,Path.Combine(output,id+"-2f.png"));
   Call(g,"SetSourceRadar",true);
   if(floors){
    Check(radar[0].activeSelf&&radar[1].activeSelf,"unselected floor disabled");
    var top=radar[0].GetComponent<MeshRenderer>().sharedMaterial;var bottom=radar[1].GetComponent<MeshRenderer>().sharedMaterial;
    Check(top.shader.isSupported&&top.color.a>.1f&&top.color.a<.4f&&bottom.color.a==1&&bottom.renderQueue>top.renderQueue,"lower selection alpha/order");
    var lower=Capture(g,Path.Combine(output,id+"-1f.png"));int changed=0;for(int i=0;i<upper.Length;i++)if(Math.Abs(upper[i].g-lower[i].g)>20)changed++;
    Check(changed>upper.Length*.025f,"floor switch did not change rendered image "+id);
    Call(g,"SetSourceRadar",false);Check(top.color.a==1&&bottom.color.a<.4f&&top.renderQueue>bottom.renderQueue,"upper selection alpha/order");
   }else {Check(!(bool)Field(g,"sourceLowerFloor")&&radar[0].activeSelf&&!radar[1].activeSelf,"single-floor empty selection");Capture(g,Path.Combine(output,id+"-single-floor-guard.png"));}
  }
  // A statistical skip changes the requested map, while leaving the loaded geometry alone.
  Call(g,"ActivateMatchMap","de_inferno");typeof(Prototype).GetField("quickSeriesSetup",F).SetValue(g,true);Call(g,"ActivateMatchMap","de_nuke");Call(g,"SetSourceRadar",true);
  typeof(Prototype).GetField("quickSeriesSetup",F).SetValue(g,false);Call(g,"ActivateMatchMap","de_inferno");
  Check(!(bool)Field(g,"sourceLowerFloor"),"skip left an empty lower Inferno floor active");Capture(g,Path.Combine(output,"inferno-after-skip.png"));
  Debug.Log("RADAR_VISUAL_ALL_OK five maps rendered; single-floor guard; 1F/2F alpha, draw order and image differences; skip-return regression");
 }
}
#endif
