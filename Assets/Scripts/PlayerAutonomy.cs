using System;
using System.Collections.Generic;
using UnityEngine;
namespace FpsManager
{
    public enum SoundKind { Footstep, Gunshot, Reload, Plant, Beep, Defuse, Throw }
    public struct HeardSound { public bool known; public Vector2 position; public SoundKind kind; public float age; }
    public struct SoundPulse { public int source; public Vector2 position; public SoundKind kind; }
    // Per-listener anonymous sound memory. True source IDs never enter HeardSound.
    public sealed class MatchSounds
    {
        readonly List<SoundPulse> pending=new List<SoundPulse>();
        readonly HeardSound[] heard=new HeardSound[10];
        public readonly int[] Emitted=new int[7];
        public HeardSound Heard(int i) { return heard[i]; }
        public void Emit(int source,Vector2 position,SoundKind kind) { pending.Add(new SoundPulse{source=source,position=position,kind=kind}); Emitted[(int)kind]++; }
        public static float Range(SoundKind kind) { return kind==SoundKind.Gunshot?55:kind==SoundKind.Beep?40:kind==SoundKind.Reload?12:kind==SoundKind.Plant||kind==SoundKind.Defuse?16:18; }
        public void Tick(float dt,Vector2[] positions,int[] teams,bool[] alive,DeploymentNavigation navigation,VisionSystem vision)
        {
            for(int i=0;i<10;i++) { heard[i].age+=dt; if(heard[i].age>4) heard[i].known=false; }
            foreach(var pulse in pending) for(int listener=0;listener<10;listener++)
            {
                if(!alive[listener]||listener==pulse.source) continue;
                if(pulse.source>=0&&teams[listener]==teams[pulse.source]) continue;
                float range=Range(pulse.kind)*(navigation.SightClear(positions[listener],pulse.position)?1:.7f);
                if(Vector2.Distance(positions[listener],pulse.position)>range) continue;
                // Coarse 4-unit acoustic region rather than a precise tracked position.
                Vector2 estimate=new Vector2(Mathf.Floor(pulse.position.x/4)*4+2,Mathf.Floor(pulse.position.y/4)*4+2);
                heard[listener]=new HeardSound{known=true,position=estimate,kind=pulse.kind,age=0};
                if(pulse.source>=0) vision.ReportSound(teams[listener],pulse.source,listener,estimate);
            }
            pending.Clear();
        }
    }
    public sealed class PlayerAutonomy
    {
        public readonly MatchSounds Sounds=new MatchSounds();
        public readonly bool[] Walking=new bool[10];
        public readonly bool[] Blinded=new bool[10];
        readonly float[] think=new float[10],steps=new float[10],blind=new float[10],utilityCooldown=new float[10];
        readonly Vector2[] offset=new Vector2[10];
        readonly DeterministicRandom[] random=new DeterministicRandom[10];
        struct Utility { public int owner; public Vector2 position; public bool smoke; public float fuse,life; }
        readonly List<Utility> utilities=new List<Utility>();
        readonly DeploymentNavigation navigation;
        public int Investigations,Patrols,SilentMoves,Throws;
        public string Radio { get; private set; }
        float radioAge,bombPulse;
        public PlayerAutonomy(int seed,DeploymentNavigation navigation)
        { this.navigation=navigation; for(int i=0;i<10;i++) random[i]=new DeterministicRandom(unchecked(seed^(i+1)*19349663)); }
        public bool ClearSight(Vector2 a,Vector2 b)
        {
            foreach(var u in utilities) if(u.smoke&&u.fuse<=0&&u.life>0)
            {
                var d=b-a; float t=d.sqrMagnitude<.001f?0:Mathf.Clamp01(Vector2.Dot(u.position-a,d)/d.sqrMagnitude);
                if(Vector2.Distance(a+d*t,u.position)<5) return false;
            }
            return true;
        }
        public void Tick(float dt,Vector2[] positions,Vector2[] facing,bool[] alive)
        {
            radioAge-=dt; if(radioAge<=0) Radio=null;
            for(int i=0;i<10;i++) { think[i]-=dt; utilityCooldown[i]-=dt; blind[i]=Mathf.Max(0,blind[i]-dt); Blinded[i]=blind[i]>0; }
            for(int j=utilities.Count-1;j>=0;j--)
            {
                var u=utilities[j]; float previous=u.fuse; u.fuse-=dt;
                if(previous>0&&u.fuse<=0&&!u.smoke)
                {
                    for(int i=0;i<10;i++) if(alive[i]&&Vector2.Distance(positions[i],u.position)<15&&navigation.SightClear(positions[i],u.position))
                    {
                        bool looking=Vector2.Dot(facing[i].normalized,(u.position-positions[i]).normalized)>.3f;
                        blind[i]=Mathf.Max(blind[i],looking?2.2f:.35f); Blinded[i]=true;
                    }
                }
                if(u.fuse<=0) u.life-=dt;
                if(u.life<=0) utilities.RemoveAt(j); else utilities[j]=u;
            }
        }
        public void Footstep(int i,Vector2 position,float travelled)
        {
            if(Walking[i]) { SilentMoves++; return; }
            steps[i]+=travelled;
            if(steps[i]>=2) { steps[i]%=2; Sounds.Emit(i,position,SoundKind.Footstep); }
        }
        public PlayerObjective Decide(int i,PlayerObjective order,Vector2 position,PlayerData player,PlayerMatchState inventory,CombatSystem combat,VisionSystem vision,int team,bool isCt)
        {
            var noise=Sounds.Heard(i);
            bool urgent=order.disengage||order.task==PlayerTask.Retake||order.task==PlayerTask.Defuse||order.task==PlayerTask.RecoverBomb;
            Walking[i]=!urgent&&!combat.Engaging(i)&&noise.known&&Vector2.Distance(position,noise.position)<20;
            if(think[i]>0) { if(!urgent&&(order.task==PlayerTask.DefendSite||order.task==PlayerTask.HoldSite)) order.destination+=offset[i]; return order; }
            think[i]=.6f+random[i].Next01()*.7f;
            bool visible=false; Vector2 contact=position;
            for(int enemy=0;enemy<10;enemy++)
            {
                var known=vision.Knowledge(team,enemy);
                if(vision.Sees(i,enemy)) { visible=true; contact=known.lastKnownPosition; break; }
            }
            if(combat.Magazine(i)<9&&!visible) combat.RequestReload(i);
            if((visible||noise.known)&&utilityCooldown[i]<=0)
            {
                Vector2 target=visible?contact:noise.position;
                bool smoke=order.disengage||combat.Health(i)<45;
                string item=smoke?"smoke":"flash";
                var equipment=new List<string>(inventory.equipment);
                float distance=Vector2.Distance(position,target);
                // Straight throws only until ballistic grenade geometry is implemented.
                Vector2 landing=Vector2.MoveTowards(position,target,Mathf.Min(distance,14));
                if(distance>5&&distance<22&&equipment.Contains(item)&&navigation.SightClear(position,landing))
                {
                    float error=(100-player.stats.utility)/100f*2;
                    landing+=new Vector2(random[i].NextSigned(),random[i].NextSigned())*error;
                    if(navigation.Clear(landing,landing)&&navigation.SightClear(position,landing))
                    {
                        equipment.Remove(item); inventory.equipment=equipment.ToArray();
                        utilities.Add(new Utility{owner=i,position=landing,smoke=smoke,fuse=1,life=smoke?12:.1f});
                        Sounds.Emit(i,position,SoundKind.Throw); Throws++; utilityCooldown[i]=8;
                        if(team==0) { Radio=player.handle+": "+(smoke?"Smoke out!":"Flash out!"); radioAge=2.5f; }
                    }
                }
            }
            // Keep critical objectives, but let guards change angles and investigate nearby cues.
            if(!urgent&&(order.task==PlayerTask.DefendSite||order.task==PlayerTask.HoldSite))
            {
                Vector2 candidate=order.destination;
                if(noise.known&&Vector2.Distance(order.destination,noise.position)<22)
                {
                    candidate=Vector2.MoveTowards(order.destination,noise.position,3); order.watch=noise.position-position; Investigations++;
                }
                else
                {
                    float angle=random[i].Next01()*Mathf.PI*2;
                    candidate+=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*2.5f; Patrols++;
                }
                offset[i]=navigation.Clear(candidate,candidate)&&navigation.Clear(order.destination,candidate)?candidate-order.destination:Vector2.zero;
                order.destination+=offset[i];
            }
            return order;
        }
        public void BombAudio(float dt,RoundDirector director,Vector2[] positions,int[] team,int ctTeam,CombatSystem combat)
        {
            bombPulse-=dt; if(bombPulse>0) return; bombPulse=.75f;
            if(director.PlantProgress>0&&director.Carrier>=0) Sounds.Emit(director.Carrier,positions[director.Carrier],SoundKind.Plant);
            if(director.PlantedSite>=0) Sounds.Emit(-1,director.BombPosition,SoundKind.Beep);
            if(director.DefuseProgress>0)
                for(int i=0;i<10;i++) if(team[i]==ctTeam&&combat.Alive(i)&&Vector2.Distance(positions[i],director.BombPosition)<3)
                { Sounds.Emit(i,positions[i],SoundKind.Defuse); break; }
        }
    }
}
