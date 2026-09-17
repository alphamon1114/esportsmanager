using System.Collections.Generic;
using UnityEngine;
namespace FpsManager
{
    public sealed partial class PlayerAutonomy
    {
        int[] utilityTeams;
        Vector2[] utilityPositions;
        // Only allies' positions and public team survival are read from this context.
        public void SetUtilityContext(int[] teams,Vector2[] positions)
        { utilityTeams=teams;utilityPositions=positions; }
        bool FlashSafe(Vector2 point,int team,CombatSystem combat)
        {
            for(int ally=0;ally<utilityTeams.Length;ally++)
            {
                if(utilityTeams[ally]!=team||!combat.Alive(ally)) continue;
                // Fuse is one second. Allow a full running step into the 15-unit blast radius.
                if(Vector2.Distance(utilityPositions[ally],point)<20.1f&&navigation.SightClear(utilityPositions[ally],point)) return false;
            }
            return true;
        }
        bool PendingNear(Vector2 point,bool smoke)
        {
            foreach(var u in utilities) if(u.smoke==smoke&&u.life>0&&Vector2.Distance(u.position,point)<(smoke?8:12)) return true;
            return false;
        }
        static float SegmentDistance(Vector2 point,Vector2 a,Vector2 b)
        {
            Vector2 line=b-a;float t=line.sqrMagnitude<.001f?0:Mathf.Clamp01(Vector2.Dot(point-a,line)/line.sqrMagnitude);
            return Vector2.Distance(point,a+line*t);
        }
        void PlanUtility(int i,PlayerObjective order,Vector2 position,PlayerData player,PlayerMatchState inventory,CombatSystem combat,VisionSystem vision,int team,bool isCt)
        {
            if(utilityTeams==null||utilityPositions==null||!order.valid||!combat.Alive(i)||Blinded[i]||combat.Reloading(i)||utilityCooldown[i]>0) return;
            // A planted bomb can keep the round running after the opposing side is gone.
            // Old footstep/throw sounds must not turn that period into friendly flashes.
            if(combat.LivingCount(utilityTeams,1-team)==0) return;
            if(order.task==PlayerTask.PlantBomb||order.task==PlayerTask.Defuse||order.task==PlayerTask.RecoverBomb) return;
            bool evidence=false;Vector2 threat=position;float best=float.MaxValue;
            for(int enemy=0;enemy<utilityTeams.Length;enemy++)
            {
                if(utilityTeams[enemy]==team||!combat.Alive(enemy)) continue;
                var known=vision.Knowledge(team,enemy);
                if(!known.known||known.anonymous||known.age>2.5f) continue;
                float score=Vector2.Distance(position,known.lastKnownPosition)+(known.visible?0:10);
                if(score>=best) continue;best=score;threat=known.lastKnownPosition;evidence=true;
            }
            var sound=Sounds.Heard(i);
            if(!evidence&&sound.known&&sound.age<=1.2f&&sound.kind!=SoundKind.Beep&&sound.kind!=SoundKind.Throw)
            { evidence=true;threat=sound.position; }
            if(!evidence) return;
            float reach=Vector2.Distance(position,threat);if(reach<5||reach>30) return;
            Vector2 toward=(threat-position).normalized, travel=order.destination-position;
            bool retreat=order.disengage||order.task==PlayerTask.FallBack;
            bool defend=order.task==PlayerTask.DefendSite||order.task==PlayerTask.HoldSite;
            int attackers=0;
            for(int enemy=0;enemy<utilityTeams.Length;enemy++)
            {
                if(utilityTeams[enemy]==team||!combat.Alive(enemy))continue;
                var known=vision.Knowledge(team,enemy);
                if(known.known&&!known.anonymous&&known.age<=2.5f&&Vector2.Distance(known.lastKnownPosition,threat)<12)attackers++;
            }
            bool pressure=attackers>=2||Sounds.AttackSignal(team,threat,14,10)||combat.Health(i)<45;
            bool entry=order.task==PlayerTask.PushSite||order.task==PlayerTask.Retake||order.task==PlayerTask.Trade;
            bool crossing=travel.magnitude>4&&Mathf.Abs(CombatSystem.SignedAngle(toward,travel))>40;
            var equipment=new List<string>(inventory.equipment);
            float error=(100-Mathf.Clamp(player.stats.utility,0,100))/100f*2;
            // Smoke blocks the approach or a hostile crossfire while we change space.
            if(equipment.Contains("smoke")&&(retreat||(defend&&pressure)||(entry&&crossing&&reach>18)))
            {
                Vector2 landing=position+toward*Mathf.Min(14,Mathf.Max(6,reach*.65f));
                landing+=new Vector2(random[i].NextSigned(),random[i].NextSigned())*error;
                bool blocks=SegmentDistance(landing,position,threat)<4.5f;
                bool routeSafe=retreat||defend||SegmentDistance(landing,position,order.destination)>5.5f;
                bool areaBusy=false; foreach(var u in utilities) if(u.smoke&&u.life>0&&utilityTeams[u.owner]==team&&Vector2.Distance(u.position,landing)<24) { areaBusy=true;break; }
                if(blocks&&routeSafe&&!areaBusy&&!SmokeNear(landing)&&navigation.Clear(landing,landing)&&navigation.SightClear(position,landing))
                { ThrowPlanned(i,position,landing,true,player,inventory,equipment);return; }
            }
            if(!entry||retreat||!equipment.Contains("flash")) return;
            // Put the flash on/beyond the likely enemy angle, outside friendly blast reach.
            // Straight throws only; do not fake a throw through a wall or into our feet.
            foreach(float distance in new[]{22f,24f,20.5f})
            {
                Vector2 landing=position+toward*distance;
                landing+=new Vector2(random[i].NextSigned(),random[i].NextSigned())*error;
                if(Vector2.Distance(landing,threat)>12||PendingNear(landing,false)) continue;
                if(!navigation.Clear(landing,landing)||!navigation.SightClear(position,landing)||!navigation.SightClear(threat,landing)) continue;
                if(!FlashSafe(landing,team,combat)) continue;
                ThrowPlanned(i,position,landing,false,player,inventory,equipment);return;
            }
        }
        void ThrowPlanned(int i,Vector2 origin,Vector2 landing,bool smoke,PlayerData player,PlayerMatchState inventory,List<string> equipment)
        {
            float originY=ThrowerHeight==null?0:ThrowerHeight(i),landingY=LandingHeight==null?0:LandingHeight(i,landing);
            if(HeightRay!=null){Vector2 prev=origin;float py=originY+1.5f;for(int sample=1;sample<=10;sample++){float t=sample/10f;Vector2 point=origin+(landing-origin)*t;float y=originY+(landingY-originY)*t+1.5f+Mathf.Sin(t*Mathf.PI)*2;if(!HeightRay(prev,py,point,y))return;prev=point;py=y;}}
            equipment.Remove(smoke?"smoke":"flash");inventory.equipment=equipment.ToArray();
            utilities.Add(new Utility{owner=i,origin=origin,position=landing,originHeight=originY,landingHeight=landingY,smoke=smoke,fuse=1,life=smoke?9:.1f});
            Sounds.Emit(i,origin,SoundKind.Throw);Throws++;utilityCooldown[i]=8;
            if(utilityTeams[i]==0) { Radio=player.handle+": "+(smoke?"Smoke out!":"Flash out!");radioAge=2.5f; }
        }
    }
}