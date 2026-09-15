using System;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class AutonomyChecks
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var game=new GameObject("AutonomyChecks").AddComponent<Prototype>(); game.Initialize();
        var p=new Vector2[10]; var f=new Vector2[10]; var team=new int[10]; var live=new bool[10]; var arrived=new bool[10]; var aim=new int[10];
        for(int i=0;i<10;i++) { p[i]=new Vector2(500+i*100,500); f[i]=new Vector2(-1,0); team[i]=1; live[i]=true; arrived[i]=true; aim[i]=85; }
        p[0]=new Vector2(14,20); p[1]=new Vector2(20,20); f[0]=new Vector2(1,0); team[0]=0;
        var vision=new VisionSystem(game.Navigation,new VisionSettings(),10); vision.LegacyFootsteps=false;
        // Player 9 is the far away team mate: they are nowhere near any of this and must
        // still end up knowing, because sound is shared across the side.
        team[9]=0;
        foreach(SoundKind kind in Enum.GetValues(typeof(SoundKind)))
        {
            // The bomb beeps with no owner, so it is audible to both sides. Take the only
            // nearby member of the other team out for that case, otherwise their side
            // hears it legitimately and there is nothing left to test the range with.
            live[1]=kind!=SoundKind.Beep;
            var sound=new MatchSounds(); sound.Emit(kind==SoundKind.Beep?-1:1,p[1],kind);
            sound.Tick(.05f,p,team,live,game.Navigation,vision);
            if(!sound.Heard(0).known||sound.Heard(0).kind!=kind) throw new Exception("Sound not picked up: "+kind);
            if(sound.Heard(2).known) throw new Exception("A side with nobody in range heard it: "+kind);
            if(!sound.Heard(9).known) throw new Exception("Sound was not shared with a distant team mate: "+kind);
            if(sound.Heard(0).position==p[1]) throw new Exception("Sound leaked exact position");
            if(vision.Sees(0,1)) throw new Exception("Sound grants vision");
            sound.Tick(4.1f,p,team,live,game.Navigation,vision);
            if(sound.Heard(0).known||sound.Heard(9).known) throw new Exception("Sound never expires");
        }
        live[1]=true; team[9]=1;
        Debug.Log("AI_CASE_OK seven-sound-types-range-memory-team-shared-no-vision");

        // A dead player stops feeding the team pool, and nothing they already called is
        // taken back out of it.
        team[9]=0;
        var legacy=new MatchSounds();
        legacy.Emit(1,p[1],SoundKind.Footstep);
        legacy.Tick(.05f,p,team,live,game.Navigation,vision);
        if(!legacy.Heard(9).known) throw new Exception("A living spotter did not share the cue");
        Vector2 called=legacy.Heard(9).position;
        live[0]=false;                                   // the only ear near the action dies
        legacy.Emit(1,new Vector2(30,20),SoundKind.Footstep);
        legacy.Tick(.05f,p,team,live,game.Navigation,vision);
        if(legacy.Heard(9).position!=called) throw new Exception("A dead player kept gathering information");
        if(!legacy.Heard(9).known) throw new Exception("What a dead player already called was lost");
        legacy.Tick(4.1f,p,team,live,game.Navigation,vision);
        if(legacy.Heard(9).known) throw new Exception("An old cue never went stale");
        live[0]=true; team[9]=1;
        Debug.Log("AI_CASE_OK dead-players-keep-what-they-called-and-stop-listening");
        var brain=new PlayerAutonomy(1,game.Navigation); brain.Walking[0]=true; brain.Footstep(0,p[0],10);
        if(brain.Sounds.Emitted[(int)SoundKind.Footstep]!=0) throw new Exception("Walking made footsteps");
        brain.Walking[0]=false; brain.Footstep(0,p[0],3);
        if(brain.Sounds.Emitted[(int)SoundKind.Footstep]!=1) throw new Exception("Running is silent");
        Debug.Log("AI_CASE_OK silent-walk-audible-run");
        p[1]=new Vector2(30,20);
        var combat=new CombatSystem(new CombatSettings(),new WeaponProfile{damage=0},10); combat.AmmoEnabled=true;
        int reloads=0,shots=0; combat.ReloadStarted=i=>reloads++; combat.ShotFired=i=>shots++;
        for(int tick=0;tick<240;tick++) { vision.Tick(.05f,p,f,team,live); combat.Tick(.05f,p,f,team,arrived,aim,vision); }
        if(reloads==0||shots<40) throw new Exception("Reload does not resume fire");
        Debug.Log("AI_CASE_OK real-gunfire-reload-resume");
        foreach(bool smoke in new[]{false,true})
        {
            brain=new PlayerAutonomy(2,game.Navigation);
            var inventory=new PlayerMatchState{equipment=new[]{smoke?"smoke":"flash"}};
            var player=game.Data.players[0]; int utility=player.stats.utility; player.stats.utility=100;
            var order=new PlayerObjective{valid=true,task=PlayerTask.DefendSite,destination=p[0],disengage=smoke};
            brain.Decide(0,order,p[0],player,inventory,combat,vision,0,true); player.stats.utility=utility;
            if(brain.Throws!=1||inventory.equipment.Length!=0) throw new Exception("Utility not consumed: "+smoke);
            brain.Tick(1.1f,p,f,live);
            if(smoke&&brain.ClearSight(p[0],p[1])) throw new Exception("Smoke does not block sight");
            if(!smoke&&!brain.Blinded[1]) throw new Exception("Flash does not blind");
            brain.Tick(14,p,f,live);
            if(!brain.ClearSight(p[0],p[1])||brain.Blinded[1]) throw new Exception("Utility never expires");
        }
        Debug.Log("AI_CASE_OK utility-consumption-effect-expiry");
        // Synthetic director isolates independent launch from travel/combat randomness.
        var director=new RoundDirector(new RoundSettings(),game.Layout,10);
        for(int i=0;i<10;i++) team[i]=i<5?0:1;
        var anchors=new Vector2[10]; for(int i=0;i<10;i++) anchors[i]=new Vector2(1000,1000);
        vision=new VisionSystem(game.Navigation,new VisionSettings{maxRange=0,peripheralRange=0,hearingRange=0},10);
        combat.Reset(1); director.Begin(1,team,0);
        for(int tick=0;tick<200;tick++) director.Tick(.05f,p,team,0,anchors,aim,vision,combat);
        if(director.Objective(5).task==PlayerTask.MoveToLane||director.Objective(9).task!=PlayerTask.MoveToLane) throw new Exception("Players still wait for the whole team");
        Debug.Log("AI_CASE_OK independent-player-launch");
        Debug.Log("AI_ALL_OK");
    }
}
