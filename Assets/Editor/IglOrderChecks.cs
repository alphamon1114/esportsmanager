using System;
using UnityEngine;
using UnityEditor.SceneManagement;
using FpsManager;
public static class IglOrderChecks
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var game=new GameObject("IglCheck").AddComponent<Prototype>(); game.Initialize();
        for(int side=0;side<2;side++)
        {
            string first=null; bool varied=false;
            for(int seed=1;seed<=24;seed++)
            {
                game.SetRoundSeed(seed); game.PrepareIglOrders();
                var before=new int[10]; string signature="";
                for(int i=0;i<10;i++) { before[i]=game.AssignedZone(i); if(before[i]<0||before[i]>2) throw new Exception("Invalid order"); signature+=before[i]; }
                if(first==null) first=signature; else varied|=signature!=first;
                for(int i=0;i<10;i++) if(game.IsAlliedPlayer(i)) game.AssignZone(i,(before[i]+1)%3);
                game.PrepareIglOrders();
                for(int i=0;i<10;i++) if(before[i]!=game.AssignedZone(i)) throw new Exception("Orders depend on previous manual assignments");
            }
            if(!varied) throw new Exception("Orders never vary with seed");
            game.SwapPreviewSides();
        }
        Debug.Log("IGL_ALL_OK: valid orders, deterministic replay, seed variety, both sides, manual overrides replaced.");
    }
}