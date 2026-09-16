using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEditor.SceneManagement;
using FpsManager;

// Independent, paired-side trials. No economy carry-over and no gameplay mutations.
public static class BalanceMeasure
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var game=new GameObject("BalanceMeasure").AddComponent<Prototype>(); game.Initialize();
        var csv=new StringBuilder("seed,ctTeam,outcome,seconds,planted,fallBack,retake,shots,throws,footsteps,gunshots,reloads,plantSounds,beeps,defuseSounds,throwSounds\n");
        int ctWins=0,plants=0,falls=0,retakes=0; float seconds=0;
        for(int side=0;side<2;side++)
        {
            for(int seed=1;seed<=30;seed++)
            {
                game.SetRoundSeed(seed-1); game.PrepareNextRound();
                for(int i=0;i<10;i++) { game.MatchState(i).credits=800; game.MatchState(i).equipment=new[]{"ak_47","flash","smoke"}; }
                game.BeginRound(); bool planted=false,fell=false,retook=false; int ticks=0;
                while(game.Director.Phase!=RoundPhase.Ended&&ticks<4000)
                {
                    game.SimulateMovement(.05f); ticks++;
                    planted|=game.Director.PlantedSite>=0;
                    for(int i=0;i<10;i++) { var task=game.Director.Objective(i).task; fell|=task==PlayerTask.FallBack; retook|=task==PlayerTask.Retake||task==PlayerTask.Defuse; }
                }
                if(game.Director.Phase!=RoundPhase.Ended) throw new Exception("Round did not terminate");
                var outcome=game.Director.Outcome;
                bool ct=outcome==RoundOutcome.BombDefused||outcome==RoundOutcome.TerroristsEliminated||outcome==RoundOutcome.TimeExpired;
                if(ct) ctWins++; if(planted) plants++; if(fell) falls++; if(retook) retakes++; seconds+=ticks*.05f;
                csv.Append(seed).Append(',').Append(game.Data.teams[game.CounterTerroristTeam].id).Append(',').Append(outcome).Append(',').Append((ticks*.05f).ToString("F2",CultureInfo.InvariantCulture)).Append(',').Append(planted).Append(',').Append(fell).Append(',').Append(retook).Append(',').Append(game.Combat.Shots).Append(',').Append(game.Autonomy.Throws);
                foreach(var count in game.Autonomy.Sounds.Emitted) csv.Append(',').Append(count);
                csv.AppendLine();
                if(seed%10==0) Debug.Log("BALANCE_PROGRESS side="+side+" trials="+seed);
            }
            game.PlaceTeams(); game.SwapPreviewSides();
        }
        string report="BALANCE_60_OK ctWins="+ctWins+" plants="+plants+" fallbackRounds="+falls+" retakeRounds="+retakes+" meanSeconds="+(seconds/60).ToString("F2",CultureInfo.InvariantCulture);
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"esportsmanager-balance-60.csv"),csv.ToString());
        Debug.Log(report);
    }
}