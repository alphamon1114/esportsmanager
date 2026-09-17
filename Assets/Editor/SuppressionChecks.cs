using System;
using System.Collections.Generic;
using System.Reflection;
using FpsManager;
using UnityEngine;
public static class SuppressionChecks
{
    sealed class Fixture
    {
        public CombatSystem combat; public VisionSystem vision; public PlayerAutonomy ai; public DeploymentNavigation nav;
        public Vector2[] p=new Vector2[10],f=new Vector2[10]; public int[] teams=new int[10],aim=new int[10];
        public bool[] alive=new bool[10],ready=new bool[10];
        public Fixture(int seed,bool blind,bool smoke,bool cue,bool wall=false)
        {
            nav=new DeploymentNavigation(wall?new List<Rect>{new Rect(25,0,1,100)}:new List<Rect>());
            combat=new CombatSystem(new CombatSettings{health=100000},new WeaponProfile{damage=0},10); combat.Reset(seed); combat.AmmoEnabled=true;
            ai=new PlayerAutonomy(seed,nav); ai.Blinded[0]=blind;
            if(smoke) ((List<PlayerAutonomy.Utility>)typeof(PlayerAutonomy).GetField("utilities",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ai)).Add(new PlayerAutonomy.Utility{position=new Vector2(25,20),smoke=true,life=9});
            vision=new VisionSystem(nav,new VisionSettings{hearingRange=0},10); vision.LegacyFootsteps=false; vision.Blinded=ai.Blinded; vision.ExtraSight=ai.ClearSight;
            for(int i=0;i<10;i++) { p[i]=new Vector2(90,90); f[i]=new Vector2(1,0); alive[i]=true; aim[i]=70; teams[i]=i<5?0:1; }
            p[0]=new Vector2(20,20); p[5]=new Vector2(30,20); ready[0]=true;
            combat.ConfigureSpam(ai,nav); combat.SpamAllowed[0]=true;
            if(cue) { ai.Sounds.Emit(5,p[5],SoundKind.Gunshot); ai.Sounds.Tick(0,p,teams,alive,nav,vision); }
        }
        public void Step() { vision.Tick(.05f,p,f,teams,alive); combat.Tick(.05f,p,f,teams,ready,aim,vision); }
    }
    static void Check(bool value,string text) { if(!value) throw new Exception(text); }
    public static void Run()
    {
        int blindShots=0,smokeShots=0,probeShots=0,memoryShots=0;
        for(int seed=1;seed<=40;seed++)
        {
            var a=new Fixture(seed,true,false,true); var b=new Fixture(seed,true,false,true);
            b.p[5]=new Vector2(40,40); // Same intel, different unseen world: identical aim and firing.
            for(int tick=0;tick<30;tick++) { a.Step(); b.Step(); }
            Check(a.combat.SpamShots==b.combat.SpamShots&&Vector2.Distance(a.combat.SuppressionPoint(0),b.combat.SuppressionPoint(0))<.0001f,"Hidden enemy tracked");
            Check(a.combat.SpamShots<=6,"Unbounded burst"); blindShots+=a.combat.SpamShots;
            Check(a.combat.Magazine(0)==30-a.combat.Shots,"Spam did not consume ammo");
            var smoke=new Fixture(seed,false,true,true); var probe=new Fixture(seed,false,true,false);
            var none=new Fixture(seed,true,false,false); var wall=new Fixture(seed,true,false,true,true);
            var denied=new Fixture(seed,true,false,true); denied.combat.SpamAllowed[0]=false;
            for(int tick=0;tick<30;tick++) { smoke.Step(); probe.Step(); none.Step(); wall.Step(); denied.Step(); }
            smokeShots+=smoke.combat.SpamShots; probeShots+=probe.combat.SpamShots;
            Check(none.combat.Shots==0&&wall.combat.Shots==0&&denied.combat.Shots==0,"No-intel/wall/urgent guard failed");
            var stale=new Fixture(seed,true,false,true); stale.ai.Sounds.Tick(3,stale.p,stale.teams,stale.alive,stale.nav,stale.vision);
            for(int tick=0;tick<30;tick++) stale.Step();
            Check(stale.combat.Shots==0,"Expired sound used");
            var memory=new Fixture(seed,false,false,false);
            for(int tick=0;tick<10;tick++) memory.vision.Tick(.05f,memory.p,memory.f,memory.teams,memory.alive);
            memory.ai.Blinded[0]=true;
            for(int tick=0;tick<30;tick++) memory.Step();
            memoryShots+=memory.combat.SpamShots;
            if(a.combat.Shots>0)
            {
                a.combat.RequestReload(0); int shots=a.combat.Shots;
                for(int tick=0;tick<10;tick++) a.Step();
                Check(a.combat.Shots==shots,"Suppression fired while reloading");
            }
            smoke.ai.Blinded[0]=false;
            ((List<PlayerAutonomy.Utility>)typeof(PlayerAutonomy).GetField("utilities",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(smoke.ai)).Clear();
            for(int tick=0;tick<10;tick++) smoke.vision.Tick(.05f,smoke.p,smoke.f,smoke.teams,smoke.alive);
            int spamBefore=smoke.combat.SpamShots; smoke.Step();
            Check(smoke.combat.SpamShots==spamBefore&&!smoke.combat.Spamming(0),"Visible target did not replace suppression");
            a.combat.Reset(seed); Check(a.combat.SpamShots==0&&!a.combat.Spamming(0),"Reset retained suppression");
        }
        Check(blindShots>0&&smokeShots>0&&probeShots>0&&memoryShots>0,"Suppression never occurs");
        // Isolate physical collisions from aim uncertainty, using the actual shot method.
        var fire=typeof(CombatSystem).GetMethod("FireSpam",BindingFlags.NonPublic|BindingFlags.Instance);
        foreach(int mode in new[]{0,1,2,3})
        {
            var c=new Fixture(1,true,false,true,mode==1);
            c.combat.Equip(0,new WeaponProfile{damage=100000,baseSpreadDegrees=0,recoilPerShot=0});
            if(mode==2) c.p[1]=new Vector2(24,20); // Friendly body absorbs; no damage to either.
            if(mode==3) c.p[5]=new Vector2(30,30); // Empty ray misses despite nearby unseen enemy.
            int events=0;c.combat.Killed=k=>events++;
            fire.Invoke(c.combat,new object[]{0,c.p,c.f,c.teams,100});
            Check(mode==0?!c.combat.Alive(5)&&events==1:c.combat.Hits==0&&events==0,"Physical hit/wall/friendly/miss failed");
        }
        Debug.Log("SUPPRESSION_ALL_OK blind="+blindShots+" smoke="+smokeShots+" probe="+probeShots+" memory="+memoryShots+"; no tracking, bounded burst, ammo, stale intel, walls, friendlies, actual kills, reset");
    }
}