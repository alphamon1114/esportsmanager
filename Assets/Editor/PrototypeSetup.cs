using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FpsManager;

public static class PrototypeSetup
{
    [MenuItem("FPS Manager/Create prototype scene")]
    public static void CreateScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("FPS Manager").AddComponent<Prototype>();
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scenes/Prototype.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Prototype.unity",true)};
        PlayerSettings.productName="FPS Manager Prototype";
        PlayerSettings.defaultScreenWidth=1280; PlayerSettings.defaultScreenHeight=800;
        AssetDatabase.SaveAssets();
        var prototype=UnityEngine.Object.FindFirstObjectByType<Prototype>();
        prototype.Initialize();
        for(int i=0;i<10;i++) prototype.Select(i);
        if(prototype.Data.players.Length!=10) throw new Exception("Roster validation failed");
        Debug.Log("FPS_PROTOTYPE_SMOKE_OK: map, cameras, ten players and all POV selections initialized.");
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity");
        if (UnityEngine.Object.FindFirstObjectByType<Prototype>() == null) throw new Exception("Saved scene lost Prototype script reference");
        Debug.Log("FPS_SCENE_RELOAD_OK");
    }
}
