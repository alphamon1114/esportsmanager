#if UNITY_EDITOR && UNITY_5_3_OR_NEWER
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
[InitializeOnLoad]
public static class ConditionHudCapture
{
 static FpsManager.Prototype game;static EditorWindow view;static int frame;
 static ConditionHudCapture(){if(SessionState.GetBool("ConditionHudActive",false))EditorApplication.update+=Tick;}
 public static void Run()
 {
  ConditionChecks.Run();
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  new GameObject("HUD preview").AddComponent<FpsManager.Prototype>();
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scenes/HudEditorPreview.unity");
  SessionState.SetBool("ConditionHudActive",true);
  EditorApplication.EnterPlaymode();
 }
 static void Tick()
 {
  if(!EditorApplication.isPlaying)return;
  try
  {
   frame++;
   if(frame==5)
   {
    game=UnityEngine.Object.FindFirstObjectByType<FpsManager.Prototype>();game.Initialize();Time.timeScale=0;
    typeof(FpsManager.Prototype).GetField("menuPage",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(game,0);
    SessionState.SetInt("ConditionOldLanguage",PlayerPrefs.GetInt("fps.language",0));game.SetLanguage(true);game.StartMatch();game.Select(0);
    view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
    view.position=new Rect(0,0,1600,940);
    view.GetType().BaseType.GetProperty("targetSize",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,new Vector2(1600,900));
   }
   if(frame==10)Capture("buy-ko.png");
   if(frame==12){game.RequestTimeout();game.Select(0);}
   if(frame==18)Capture("timeout-ko.png");
   if(frame==20){game.SetLanguage(false);game.Select(0);}
   if(frame==25)Capture("timeout-en.png");
   if(frame==27){game.ChooseTimeoutTalk(FpsManager.TimeoutTalk.Confidence);game.SetLanguage(true);game.Select(0);}
   if(frame==32){Capture("timeout-selected-ko.png");PlayerPrefs.SetInt("fps.language",SessionState.GetInt("ConditionOldLanguage",0));PlayerPrefs.Save();SessionState.SetBool("ConditionHudActive",false);Debug.Log("CONDITION_HUD_OK");EditorApplication.Exit(0);}
  }
  catch(Exception e){SessionState.SetBool("ConditionHudActive",false);Debug.LogException(e);EditorApplication.Exit(1);}
 }
 static void Capture(string name)
 {
  var method=view.GetType().BaseType.GetMethod("RenderView",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance);
  var rt=(RenderTexture)method.Invoke(view,new object[]{new Vector2(-1,-1),false});
  if(rt==null)throw new Exception("Game view has no render texture");
  var old=RenderTexture.active;RenderTexture.active=rt;
  var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
  image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
  var pixels=image.GetPixels();var upright=new Color[pixels.Length];for(int row=0;row<rt.height;row++)Array.Copy(pixels,row*rt.width,upright,(rt.height-1-row)*rt.width,rt.width);image.SetPixels(upright);image.Apply();
  File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("HUD_CAPTURE_OUTPUT"),name),image.EncodeToPNG());
  RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);
  Debug.Log("HUD_CAPTURE "+name+" "+rt.width+"x"+rt.height);
 }
}




#endif
