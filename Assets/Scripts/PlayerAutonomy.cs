using System;
using System.Collections.Generic;
using UnityEngine;
namespace FpsManager
{
    public enum SoundKind { Footstep, Gunshot, Reload, Plant, Beep, Defuse, Throw }
    public struct HeardSound { public bool known; public Vector2 position; public SoundKind kind; public float age; }
    public struct SoundPulse { public bool impact; public int source; public Vector2 position; public SoundKind kind; }
    // Per-listener anonymous sound memory. True source IDs never enter HeardSound.
    // Sound is a team asset. Whatever one living player picks up, the whole side knows
    // about a moment later: that is what calling it out is. A dead player stops adding to
    // the pool, but nothing they already called is taken back out of it.
    public sealed class MatchSounds
    {
        readonly List<SoundPulse> pending=new List<SoundPulse>();
        readonly List<HeardSound>[] shared={ new List<HeardSound>(), new List<HeardSound>() };
        readonly HeardSound[] heard=new HeardSound[10];
        readonly List<HeardSound>[] footTracks={new List<HeardSound>(),new List<HeardSound>()};
        readonly List<HeardSound>[] impacts={new List<HeardSound>(),new List<HeardSound>()};
        public void EmitImpact(int source,Vector2 position)
        { pending.Add(new SoundPulse{source=source,position=position,kind=SoundKind.Throw,impact=true}); }
        static void Age(List<HeardSound> records,float dt,float lifetime)
        {
            for(int i=records.Count-1;i>=0;i--) { var r=records[i]; r.age+=dt; if(r.age>lifetime) records.RemoveAt(i); else records[i]=r; }
        }
        void FootTrack(int team,Vector2 estimate)
        {
            int nearest=-1; float best=float.MaxValue;
            for(int i=0;i<footTracks[team].Count;i++)
            {
                var r=footTracks[team][i]; float distance=Vector2.Distance(r.position,estimate);
                if(distance<=3+5*r.age&&distance<best) { nearest=i; best=distance; }
            }
            var record=new HeardSound{known=true,position=estimate,kind=SoundKind.Footstep};
            if(nearest>=0) footTracks[team][nearest]=record; else footTracks[team].Add(record);
        }
        public bool AttackSignal(int team,Vector2 site,float approachRadius,float siteRadius)
        {
            int steps=0,landings=0;
            foreach(var r in footTracks[team]) if(Vector2.Distance(r.position,site)<=approachRadius) steps++;
            foreach(var r in impacts[team]) if(Vector2.Distance(r.position,site)<=siteRadius) landings++;
            return steps>=3||landings>=2;
        }
        public readonly int[] Emitted=new int[7];
        public HeardSound Heard(int i) { return heard[i]; }
        public int TeamCues(int team) { return shared[team].Count; }
        public void Emit(int source,Vector2 position,SoundKind kind) { pending.Add(new SoundPulse{source=source,position=position,kind=kind}); Emitted[(int)kind]++; }
        public static float Range(SoundKind kind) { return kind==SoundKind.Gunshot?55:kind==SoundKind.Beep?40:kind==SoundKind.Reload?12:kind==SoundKind.Plant||kind==SoundKind.Defuse?16:18; }
        public void Tick(float dt,Vector2[] positions,int[] teams,bool[] alive,DeploymentNavigation navigation,VisionSystem vision)
        {
            for(int team=0;team<2;team++)
                for(int j=shared[team].Count-1;j>=0;j--)
                {
                    var record=shared[team][j]; record.age+=dt;
                    if(record.age>4) shared[team].RemoveAt(j); else shared[team][j]=record;
                }
            for(int team=0;team<2;team++) { Age(footTracks[team],dt,1.2f); Age(impacts[team],dt,3); }
            foreach(var pulse in pending)
            {
            int reported=0;
            for(int listener=0;listener<10;listener++)
            {
                if(!alive[listener]||listener==pulse.source) continue;
                if(pulse.source>=0&&teams[listener]==teams[pulse.source]) continue;
                float range=Range(pulse.kind)*(navigation.SightClear(positions[listener],pulse.position)?1:.7f);
                if(Vector2.Distance(positions[listener],pulse.position)>range) continue;
                // Coarse 4-unit acoustic region rather than a precise tracked position.
                Vector2 estimate=new Vector2(Mathf.Floor(pulse.position.x/4)*4+2,Mathf.Floor(pulse.position.y/4)*4+2);
                int listenerTeam=teams[listener];
                if((reported&(1<<listenerTeam))==0)
                {
                    if(pulse.impact) impacts[listenerTeam].Add(new HeardSound{known=true,position=estimate});
                    else if(pulse.kind==SoundKind.Footstep) FootTrack(listenerTeam,estimate);
                    reported|=1<<listenerTeam;
                }
                // Impact information reports a landing, never the thrower's position.
                if(pulse.impact) continue;
                Record(teams[listener],estimate,pulse.kind);
                if(pulse.source>=0) vision.ReportSound(teams[listener],pulse.source,listener,estimate);
            }
            }
            pending.Clear();
            // Each living player reads the team pool and takes the cue nearest to them.
            // A dead player keeps whatever they last held and stops updating.
            for(int i=0;i<10;i++)
            {
                if(!alive[i]) continue;
                var best=new HeardSound(); float distance=float.MaxValue;
                foreach(var record in shared[teams[i]])
                {
                    float candidate=Vector2.Distance(positions[i],record.position);
                    if(candidate>=distance) continue;
                    distance=candidate; best=record;
                }
                heard[i]=best;
            }
        }
        void Record(int team,Vector2 estimate,SoundKind kind)
        {
            for(int j=0;j<shared[team].Count;j++)
                if(Vector2.Distance(shared[team][j].position,estimate)<.01f)
                { shared[team][j]=new HeardSound{known=true,position=estimate,kind=kind,age=0}; return; }
            shared[team].Add(new HeardSound{known=true,position=estimate,kind=kind,age=0});
        }
    }

    public sealed partial class PlayerAutonomy
    {
        public readonly MatchSounds Sounds=new MatchSounds();
        public readonly bool[] Walking=new bool[10];
        public readonly bool[] Blinded=new bool[10];
        readonly float[] think=new float[10],steps=new float[10],blind=new float[10],utilityCooldown=new float[10];
        readonly Vector2[] offset=new Vector2[10];
        readonly float[] coverUntil=new float[10];
        readonly DeterministicRandom[] random=new DeterministicRandom[10];
        public struct Utility { public int owner; public Vector2 origin,position; public bool smoke; public float fuse,life,originHeight,landingHeight; }
        public int UtilityCount { get { return utilities.Count; } }
        public Utility UtilityAt(int index) { return utilities[index]; }
        public float BlindRemaining(int player) { return blind[player]; }
        readonly List<Utility> utilities=new List<Utility>();
        readonly DeploymentNavigation navigation;
        public int Investigations,Patrols,SilentMoves,Throws;
        public string Radio { get; private set; }
        float radioAge,bombPulse;
        public PlayerAutonomy(int seed,DeploymentNavigation navigation)
        { this.navigation=navigation; for(int i=0;i<10;i++) random[i]=new DeterministicRandom(unchecked(seed^(i+1)*19349663)); }
        // One smoke per angle. Without this every player on a push threw their own onto
        // the same spot and the corridor stayed blind for the rest of the round.
        bool SmokeNear(Vector2 point)
        {
            foreach(var u in utilities) if(u.smoke&&u.life>0&&Vector2.Distance(u.position,point)<8f) return true;
            return false;
        }
        public Func<int,float> ThrowerHeight;
        public Func<int,Vector2,float> LandingHeight;
        public Func<Vector2,float,Vector2,float,bool> HeightRay;
        public bool ClearSight3D(Vector2 a,float ay,Vector2 b,float by)
        {
            foreach(var u in utilities)if(u.smoke&&u.fuse<=0&&u.life>0){var d=b-a;float t=d.sqrMagnitude<.001f?0:Mathf.Clamp01(Vector2.Dot(u.position-a,d)/d.sqrMagnitude);float y=ay+(by-ay)*t;if(Vector2.Distance(a+d*t,u.position)<5&&y>=u.landingHeight&&y<=u.landingHeight+6)return false;}return true;
        }
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
            for(int i=0;i<10;i++) { think[i]-=dt; coverUntil[i]=Mathf.Max(0,coverUntil[i]-dt); utilityCooldown[i]-=dt; blind[i]=Mathf.Max(0,blind[i]-dt); Blinded[i]=blind[i]>0; }
            for(int j=utilities.Count-1;j>=0;j--)
            {
                var u=utilities[j]; float previous=u.fuse; u.fuse-=dt;
                if(previous>0&&u.fuse<=0) Sounds.EmitImpact(u.owner,u.position);
                if(previous>0&&u.fuse<=0&&!u.smoke)
                {
                    for(int i=0;i<10;i++) if(alive[i]&&Vector2.Distance(positions[i],u.position)<15&&(HeightRay!=null?HeightRay(positions[i],ThrowerHeight(i)+1.65f,u.position,u.landingHeight+1.5f):navigation.SightClear(positions[i],u.position)))
                    {
                        bool looking=Vector2.Dot(facing[i].normalized,(u.position-positions[i]).normalized)>.3f;
                        blind[i]=Mathf.Max(blind[i],looking?2.2f:.35f); Blinded[i]=true;
                    }
                }
                if(u.fuse<=0) u.life-=dt;
                if(u.life<=0&&(u.smoke||u.fuse<=-.3f)) utilities.RemoveAt(j); else utilities[j]=u;
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
            Walking[i]=!urgent&&!combat.Engaging(i)&&((noise.known&&Vector2.Distance(position,noise.position)<20)||(order.cautious&&Vector2.Distance(position,order.destination)<20));
            if(!urgent&&order.hasCover&&(order.task==PlayerTask.DefendSite||order.task==PlayerTask.HoldSite))
                if(combat.Reloading(i)||Blinded[i]||combat.Health(i)<40) coverUntil[i]=.75f;
            if(think[i]>0) { return ApplyPosition(i,order,urgent); }
            think[i]=.6f+random[i].Next01()*.7f;
            bool visible=false; Vector2 contact=position;
            for(int enemy=0;enemy<10;enemy++)
            {
                var known=vision.Knowledge(team,enemy);
                if(vision.Sees(i,enemy)) { visible=true; contact=known.lastKnownPosition; break; }
            }
            bool nearbyThreat=visible||(noise.known&&noise.age<2&&Vector2.Distance(position,noise.position)<20);
            if(combat.ShouldReload(i,nearbyThreat)) combat.RequestReload(i);
            PlanUtility(i,order,position,player,inventory,combat,vision,team,isCt);
            // Hold the angle. There used to be a random 2.5 unit wander here every second;
            // it kept guards away from their holding position. Navigation and aim are
            // now independent, but guards still need a reason to leave their cover.
            if(!urgent&&(order.task==PlayerTask.DefendSite||order.task==PlayerTask.HoldSite))
            {
                if(noise.known&&Vector2.Distance(order.destination,noise.position)<22)
                {
                    var candidate=Vector2.MoveTowards(order.destination,noise.position,3);
                    order.watch=noise.position-position; Investigations++;
                    offset[i]=navigation.Clear(candidate,candidate)&&navigation.Clear(order.destination,candidate)?candidate-order.destination:Vector2.zero;
                }
                else offset[i]=Vector2.zero;



            }
            return ApplyPosition(i,order,urgent);
        }
        PlayerObjective ApplyPosition(int i,PlayerObjective order,bool urgent)
        {
            if(!urgent&&(order.task==PlayerTask.DefendSite||order.task==PlayerTask.HoldSite))
                order.destination=order.hasCover&&coverUntil[i]>0?order.cover:order.destination+offset[i];
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
