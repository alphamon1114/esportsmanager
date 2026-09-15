using System;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;

public static class DeploymentChecks
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var game=new GameObject("Check").AddComponent<Prototype>(); game.Initialize();
        for(int side=0;side<2;side++)
        {
            for(int config=-1;config<3;config++)
            {
                game.PlaceTeams();
                for(int i=0;i<10;i++)
                {
                    if(game.IsAlliedPlayer(i)) game.AssignZone(i,config<0?i%3:config);
                    else {
                        bool rejected=false;
                        try { game.AssignZone(i,0); } catch(InvalidOperationException) { rejected=true; }
                        if(!rejected) throw new Exception("Opponent orders were accepted");
                    }
                }
                game.ValidateMovementGeometry(); game.BeginDeployment();
                bool locked=false;
                try { game.AssignZone(0,1); } catch(InvalidOperationException) { locked=true; }
                if(!locked) throw new Exception("Deployment can change during movement");
                for(int tick=0;tick<1800&&!game.DeploymentComplete;tick++)
                {
                    game.SimulateMovement(.05f); game.ValidateMovementGeometry();
                    game.Select(tick%10);
                }
                if(!game.DeploymentComplete) throw new Exception("Movement did not finish: "+side+" / "+config);
                Debug.Log("DEPLOYMENT_CASE_OK side="+side+" zone="+config);
            }
            game.PlaceTeams(); game.SwapPreviewSides();
        }
        Debug.Log("DEPLOYMENT_ALL_OK: 8 configurations, both sides, obstacle clearance, locked orders, completion, POV selection.");
    }
}
