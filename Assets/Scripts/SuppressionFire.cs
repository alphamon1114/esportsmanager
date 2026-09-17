using UnityEngine;
namespace FpsManager
{
    // Optional round-only suppression. Decisions read remembered points, never enemy transforms.
    public sealed partial class CombatSystem
    {
        PlayerAutonomy spamAutonomy;
        DeploymentNavigation spamNavigation;
        public readonly bool[] SpamAllowed=new bool[10];
        readonly bool[] spamShot=new bool[10];
        readonly int[] spamLeft=new int[10];
        readonly float[] spamWait=new float[10],spamLife=new float[10];
        readonly Vector2[] spamPoint=new Vector2[10];
        readonly DeterministicRandom[] spamRandom=new DeterministicRandom[10];
        public int SpamShots { get; private set; }
        public bool Spamming(int i) { return spamAutonomy!=null&&spamLeft[i]>0&&spamLife[i]>0&&SpamAllowed[i]; }
        public Vector2 SuppressionPoint(int i) { return spamPoint[i]; }
        public void ConfigureSpam(PlayerAutonomy autonomy,DeploymentNavigation navigation)
        { spamAutonomy=autonomy; spamNavigation=navigation; }
        void ResetSpam(int seed)
        {
            SpamShots=0; spamAutonomy=null; spamNavigation=null;
            for(int i=0;i<10;i++)
            {
                spamLeft[i]=0; spamWait[i]=spamLife[i]=0; spamShot[i]=SpamAllowed[i]=false; spamPoint[i]=Vector2.zero;
                spamRandom[i]=new DeterministicRandom(unchecked(seed^(i+1)*67867967));
            }
        }
        bool PrepareSpam(int i,float dt,Vector2[] positions,Vector2[] facing,int[] team,VisionSystem vision)
        {
            if(spamAutonomy==null||spamNavigation==null||!SpamAllowed[i]||fireHold[i]>0) { spamLeft[i]=0; return false; }
            bool blind=spamAutonomy.Blinded[i];
            if(spamLeft[i]>0&&(!blind&&spamAutonomy.ClearSight(positions[i],spamPoint[i]))) spamLeft[i]=0;
            if(spamLife[i]<=0) spamLeft[i]=0;
            if(spamLeft[i]<=0)
            {
                if(spamWait[i]>0) return false;
                spamWait[i]=2+spamRandom[i].Next01()*2;
                Vector2 point=positions[i]+facing[i]*18;
                bool evidence=false;
                var sound=spamAutonomy.Sounds.Heard(i);
                if(sound.known&&sound.age<=1.5f&&sound.kind!=SoundKind.Beep)
                { point=sound.position; evidence=true; }
                else
                {
                    float newest=2.5f;
                    for(int enemy=0;enemy<count;enemy++)
                    {
                        if(team[enemy]==team[i]) continue;
                        var known=vision.Knowledge(team[i],enemy);
                        if(!known.known||known.anonymous||known.age>=newest) continue;
                        if(Vector2.Distance(positions[i],known.lastKnownPosition)>45||!spamNavigation.SightClear(positions[i],known.lastKnownPosition)) continue;
                        if(!blind&&spamAutonomy.ClearSight(positions[i],known.lastKnownPosition)) continue;
                        newest=known.age; point=known.lastKnownPosition; evidence=true;
                    }
                }
                // Without a cue, only probe the smoke-covered angle already being watched.
                if(!blind&&spamAutonomy.ClearSight(positions[i],point)) return false;
                if(blind&&!evidence) return false;
                if(Vector2.Distance(positions[i],point)>45||!spamNavigation.SightClear(positions[i],point)) return false;
                if(spamRandom[i].Next01()>(evidence?.55f:.2f)) return false;
                float uncertainty=blind?3:1.8f;
                spamPoint[i]=point+new Vector2(spamRandom[i].NextSigned(),spamRandom[i].NextSigned())*uncertainty;
                spamLeft[i]=WeaponFor(i).fireInterval>=.4f?1:3+(int)(spamRandom[i].Next01()*4);
                spamLife[i]=1.2f;
                states[i].reaction=.2f+spamRandom[i].Next01()*.2f;
            }
            Vector2 line=spamPoint[i]-positions[i];
            float offset=TurnTowards(ref facing[i],line,settings.turnDegreesPerSecond*dt);
            float ready=Mathf.Max(states[i].reaction,states[i].cooldown);
            states[i].reaction=Mathf.Max(-dt,states[i].reaction-dt);
            // Hold fire for known friendly bodies, including beyond the guessed point.
            for(int mate=0;mate<count;mate++)
                if(mate!=i&&Alive(mate)&&team[mate]==team[i]&&Vector2.Distance(positions[mate],positions[i])<55
                    &&Mathf.Abs(SignedAngle(facing[i],positions[mate]-positions[i]))<settings.friendlyBlockDegrees) return false;
            if(ready>=dt||Mathf.Abs(offset)>settings.fireAlignmentDegrees||line.magnitude<1) return false;
            shotTime[i]=Mathf.Max(0,ready); spamShot[i]=true; return true;
        }
        void FireSpam(int shooter,Vector2[] positions,Vector2[] facing,int[] team,int aim)
        {
            float horizontal,baseHeight,slope;
            SampleShot(shooter,aim,out horizontal,out baseHeight,out slope);
            SpamShots++; spamLeft[shooter]--;
            float angle=Mathf.Atan2(facing[shooter].y,facing[shooter].x)+horizontal*Mathf.Deg2Rad;
            Vector2 ray=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
            int victim=-1; float closest=55; HitRegion hit=HitRegion.Miss;
            // Physical collision only: actual positions never feed the suppression aim point.
            for(int target=0;target<count;target++)
            {
                if(target==shooter||!Alive(target)) continue;
                Vector2 relative=positions[target]-positions[shooter];
                float distance=Vector2.Dot(relative,ray);
                if(distance<=0||distance>=closest) continue;
                float lateral=Mathf.Abs(relative.x*ray.y-relative.y*ray.x);
                float hitHeight=baseHeight+slope*distance+ElevationError(shooter,target,distance);
                var region=ResolveRegion(lateral,hitHeight,settings.targetRadius);
                if(region==HitRegion.Miss||!(HeightShotClear!=null?HeightShotClear(shooter,target,hitHeight):spamNavigation.SightClear(positions[shooter],positions[shooter]+ray*distance))) continue;
                closest=distance; victim=target; hit=region;
            }
            if(victim<0||team[victim]==team[shooter]) ApplyHit(shooter,-1,HitRegion.Miss);
            else ApplyHit(shooter,victim,hit);
        }
    }
}