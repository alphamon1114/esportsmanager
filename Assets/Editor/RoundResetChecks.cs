using System;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class RoundResetChecks
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var game=new GameObject("ResetChecks").AddComponent<Prototype>(); game.Initialize();
        var spawn=new Vector2[10]; for(int i=0;i<10;i++) spawn[i]=game.MapPosition(i);
        game.MatchState(0).credits=4321;
        game.MatchState(0).equipment=new[]{"awp","usp_s"};
        game.SetRoundSeed(40); game.PrepareNextRound();
        var replay=new Vector2[10]; for(int i=0;i<10;i++) replay[i]=game.MapPosition(i);
        game.SetRoundSeed(40); game.PrepareNextRound();
        for(int i=0;i<10;i++) if(game.MapPosition(i)!=replay[i]) throw new Exception("Spawn seed replay failed");
        bool varied=false;
        for(int seed=1;seed<=8;seed++)
        {
            game.SetRoundSeed(seed); game.PrepareNextRound();
            for(int i=0;i<10;i++)
            {
                int matches=0; for(int j=0;j<10;j++) if(game.TeamIndexOf(i)==game.TeamIndexOf(j)&&game.MapPosition(i)==spawn[j]) matches++;
                if(matches!=1) throw new Exception("Player outside own spawn slots");
                for(int j=0;j<i;j++) if(game.MapPosition(i)==game.MapPosition(j)) throw new Exception("Duplicate spawn slot");
                varied |= game.MapPosition(i)!=replay[i];
            }
            game.ValidateMovementGeometry();
        }
        if(!varied) throw new Exception("Spawn never changes");
        Debug.Log("RESET_CASE_OK seeded-spawn-permutation");
        // Real frame path: finish two rounds, auto reset exactly once, remain in preparation.
        for(int round=0;round<2;round++)
        {
            int before=game.CompletedRounds;
            game.BeginRound();
            // Grenades are reissued for every round; weapons and credits are not touched.
            foreach(var item in PlayerMatchState.Consumables)
                if(Array.IndexOf(game.MatchState(0).equipment,item)<0) throw new Exception("Utility not restocked: "+item);
            for(int tick=0;tick<4000&&game.CompletedRounds==before;tick++) game.AdvanceFrame(.05f);
            if(game.CompletedRounds!=before+1||game.Director.Phase!=RoundPhase.Preparation||game.RoundMode) throw new Exception("Automatic reset failed");
            if(game.LastRoundOutcome==RoundOutcome.None) throw new Exception("Result lost");
            if(game.Combat.Shots!=0||game.Director.Clock!=0||game.Director.Carrier!=-1||game.Director.PlantedSite!=-1||game.Director.DefuseProgress!=0) throw new Exception("Round state leaked");
            for(int i=0;i<10;i++) if(!game.IsAlive(i)||game.Combat.Health(i)!=100||game.Combat.Engaging(i)) throw new Exception("Combat state leaked");
            for(int t=0;t<2;t++) if(game.Vision.KnownCount(t)!=0) throw new Exception("Contact leaked");
            for(int tick=0;tick<100;tick++) game.AdvanceFrame(.05f);
            if(game.CompletedRounds!=before+1||game.Combat.Shots!=0||game.Vision.KnownCount(0)!=0) throw new Exception("Preparation kept simulating");
            game.AssignZone(0,2);
            if(game.MatchState(0).credits!=4321) throw new Exception("Credits lost");
            var kept=new System.Collections.Generic.List<string>();
            foreach(var item in game.MatchState(0).equipment)
                if(Array.IndexOf(PlayerMatchState.Consumables,item)<0) kept.Add(item);
            if(string.Join(",",kept.ToArray())!="awp,usp_s") throw new Exception("Weapons lost");
            game.ValidateMovementGeometry();
        }
        Debug.Log("RESET_CASE_OK automatic-round-reset-and-persistence");
        Debug.Log("RESET_ALL_OK");
    }
}
