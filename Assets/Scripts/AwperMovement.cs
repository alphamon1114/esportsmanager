using UnityEngine;
namespace FpsManager
{
    // Post-shot movement uses the shooter's aim and map geometry, never hidden enemies.
    public sealed class AwperMovement
    {
        readonly DeploymentNavigation nav;
        readonly DeterministicRandom random;
        Vector2 threat,cover;
        float remaining,wait;
        bool pending,active;
        public bool Active { get { return pending||active; } }
        public int Retreats { get; private set; }
        public AwperMovement(int seed,DeploymentNavigation navigation) { nav=navigation;random=new DeterministicRandom(seed); }
        public void Shot(Vector2 position,Vector2 facing,float fireInterval)
        {
            threat=position+facing.normalized*20;
            pending=true;active=false;remaining=3;
            wait=Mathf.Max(.6f,fireInterval)+.15f+random.Next01()*.55f;
        }
        public void Cancel() { pending=active=false; }
        public bool Step(float dt,Vector2 position,PlayerObjective order,bool hasAwp,out Vector2 target,out Vector2 watch)
        {
            target=position;watch=threat-position;
            if(!hasAwp||!order.valid||order.disengage||order.task==PlayerTask.PlantBomb||order.task==PlayerTask.Defuse||order.task==PlayerTask.RecoverBomb||order.task==PlayerTask.Retake) { Cancel();return false; }
            if(!Active)return false;
            remaining-=dt;if(remaining<=0){Cancel();return false;}
            if(pending)
            {
                pending=false;
                var forward=(threat-position).normalized;var side=new Vector2(-forward.y,forward.x);
                float best=float.MaxValue;
                // Search short lateral/backward routes with a real line-of-sight break.
                for(int angle=-2;angle<=2;angle++)
                    for(float distance=1;distance<=4;distance+=.5f)
                    {
                        var direction=(side*angle-forward).normalized;
                        var candidate=position+direction*distance;
                        if(distance>=best||!nav.Clear(position,candidate)||nav.SightClear(candidate,threat))continue;
                        best=distance;cover=candidate;active=true;
                    }
                if(order.hasCover&&Vector2.Distance(position,order.cover)<=4&&nav.Clear(position,order.cover)&&!nav.SightClear(order.cover,threat))
                {cover=order.cover;active=true;}
                if(!active)return false; // No cover: keep normal combat, never dodge arbitrarily in the open.
                Retreats++;
            }
            target=cover;
            if(!nav.Clear(position,cover)){Cancel();return false;}
            if(Vector2.Distance(position,cover)<.12f)
            { wait-=dt;if(wait<=0){Cancel();return false;} }
            return true;
        }
    }
    public partial class Prototype
    {
        readonly AwperMovement[] awperMovement=new AwperMovement[10];
        readonly float[] awpCycle=new float[10];
        readonly bool[] awpSidearm=new bool[10];
        void UpdateAwperWeapon(int i,float tick)
        {
            if(!AutomaticMatch)return;
            awpCycle[i]=Mathf.Max(0,awpCycle[i]-tick);
            if(System.Array.IndexOf(matchState[i].equipment,"awp")<0){awpSidearm[i]=false;return;}
            bool close=false;
            for(int enemy=0;enemy<10;enemy++)
            {
                if(teamIndex[enemy]==teamIndex[i]||!vision.Sees(i,enemy))continue;
                var known=vision.Knowledge(teamIndex[i],enemy);
                if(known.known&&Vector2.Distance(MapPosition(i),known.lastKnownPosition)<=14){close=true;break;}
            }
            string held=combat.WeaponFor(i).id;
            if(held=="awp"&&awpCycle[i]>.1f&&close)
            {
                string pistol=null;int best=0;
                foreach(var id in matchState[i].equipment)
                {
                    int index=System.Array.IndexOf(WeaponCatalog.Ids,id);if(index<0||index>=6)continue;
                    WeaponAmmo ammo;
                    if(holsteredAmmo[i]!=null&&holsteredAmmo[i].TryGetValue(id,out ammo)&&ammo.rounds==0&&ammo.spares==0)continue;
                    if(WeaponDropRules.Strength(id)>best){best=WeaponDropRules.Strength(id);pistol=id;}
                }
                if(pistol!=null){SwitchAwperWeapon(i,pistol);awpSidearm[i]=true;awperMovement[i].Cancel();peeking[i].Cancel();}
            }
            else if(awpSidearm[i])
            {
                if(System.Array.IndexOf(WeaponCatalog.Ids,held)<0||System.Array.IndexOf(WeaponCatalog.Ids,held)>=6){awpSidearm[i]=false;return;}
                if(awpCycle[i]<=0&&(!close||(combat.Magazine(i)==0&&combat.SpareMagazines(i)==0)))
                {SwitchAwperWeapon(i,"awp");awpSidearm[i]=false;}
            }
        }
        void SwitchAwperWeapon(int i,string weapon)
        {
            if(holsteredAmmo[i]==null)holsteredAmmo[i]=new System.Collections.Generic.Dictionary<string,WeaponAmmo>();
            holsteredAmmo[i][combat.WeaponFor(i).id]=combat.CaptureAmmo(i);
            WeaponAmmo ammo;
            if(!holsteredAmmo[i].TryGetValue(weapon,out ammo))ammo=WeaponDropRules.Fresh(weapon);
            combat.SetKnife(i,false);combat.EquipSaved(i,weapon,ammo);
        }
        bool StepAwper(int i,PlayerObjective order,float tick)
        {
            if(!AutomaticMatch||awperMovement[i]==null)return false;
            bool hasAwp=combat.WeaponFor(i).id=="awp";
            if(Mathf.Abs(PlayerHeight(i)-MapFloor(MapPosition(i),PlayerHeight(i)))>.4f){awperMovement[i].Cancel();return false;}
            peeking[i].SniperMode=hasAwp;
            Vector2 target,watch;
            if(!awperMovement[i].Step(tick,MapPosition(i),order,hasAwp,out target,out watch))return false;
            var before=MapPosition(i);var next=Vector2.MoveTowards(before,target,tick*3.5f);
            if(Mathf.Abs(MapFloor(next,PlayerHeight(i))-MapFloor(before,PlayerHeight(i)))>.4f){awperMovement[i].Cancel();return false;}
            combat.SetKnife(i,false);
            peeking[i].Cancel();
            actors[i].transform.position=World(next);GroundActor(i);
            moving[i]=Vector2.Distance(before,next)>.001f;arrived[i]=false;
            autonomy.Footstep(i,next,Vector2.Distance(before,next));FaceWatch(i,watch,tick);
            routeValid[i]=false;repathDelay[i]=0;moveSpeed[i]=0;
            return true;
        }
    }
}
