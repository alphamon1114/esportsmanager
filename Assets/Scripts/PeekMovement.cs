using UnityEngine;
namespace FpsManager
{
    // Local geometry and caller-provided knowledge only; never reads enemy transforms.
    public sealed class PeekMovement
    {
        readonly DeploymentNavigation nav;
        readonly DeterministicRandom rng;
        readonly float skill;
        float missWindow,evadeCooldown,reaction;
        bool pendingMiss;
        Vector2 home,edge,look,goal;
        int phase,pass;
        float timer,total,cooldown;
        public int Completed { get; private set; }
        public bool Active { get { return phase!=0; } }
                public int Evasions { get; private set; }
        public bool ReadyToFire { get { return phase!=5&&phase!=6; } }
        public PeekMovement(int seed,DeploymentNavigation navigation,int movement=50)
        { nav=navigation; rng=new DeterministicRandom(seed); skill=Mathf.Clamp01(movement/100f); }
        public void RequestRetap()
        {
            pendingMiss=true; reaction=.18f-.1f*skill; missWindow=.6f;
        }
        public void OnMiss()
        {
            if(pendingMiss||evadeCooldown>0) return;
            evadeCooldown=1.5f;
            if(rng.Next01()>.15f+.6f*skill) return;
            pendingMiss=true; reaction=.28f-.18f*skill; missWindow=.6f;
        }
        public bool Step(float dt,Vector2 position,PlayerObjective order,Vector2 probe,bool contact,out Vector2 target,out Vector2 watch)
        {
            target=position; watch=probe-position; cooldown-=dt; evadeCooldown-=dt;
            missWindow-=dt; reaction-=dt; if(missWindow<=0) pendingMiss=false;
            bool allowed=order.valid&&!order.disengage&&(order.task==PlayerTask.Lurk||order.task==PlayerTask.Patrol||order.task==PlayerTask.MoveToLane||order.task==PlayerTask.PushSite||order.task==PlayerTask.DefendSite||order.task==PlayerTask.HoldSite);
            if(!allowed|| (Active&&Vector2.Distance(goal,order.destination)>5)) { phase=0; pendingMiss=false; cooldown=1; return false; }
            if(pendingMiss&&reaction<=0&&contact)
            {
                pendingMiss=false;
                if(phase==1||phase==2) { phase=3; timer=0; Evasions++; }
                else if(!Active)
                {
                    Vector2 forward=(probe-position).normalized;
                    Vector2 lateral=new Vector2(-forward.y,forward.x);
                    int sign=rng.Next01()<.5f?-1:1;
                    for(int side=0;side<2;side++)
                    {
                        Vector2 candidate=position+lateral*(sign*(side==0?1:-1)*1.2f);
                        if(forward.sqrMagnitude<.5f||!nav.Clear(position,candidate)) continue;
                        edge=candidate; home=position; look=probe; goal=order.destination;
                        phase=5; total=0; Evasions++; break;
                    }
                }
            }
            if(!Active)
            {
                if(cooldown>0||(!contact&&order.task!=PlayerTask.PushSite)) return false;
                cooldown=.45f+rng.Next01()*.3f;
                Vector2 forward=(probe-position).normalized;
                if(forward.sqrMagnitude<.5f) return false;
                Vector2 lateral=new Vector2(-forward.y,forward.x);
                bool found=false;
                // Pair an occluded position with an exposed one across a real edge.
                for(int side=-1;side<=1&&!found;side+=2)
                    for(float reach=1.2f;reach<=3.61f&&!found;reach+=.8f)
                    {
                        Vector2 candidate=position+lateral*(side*reach);
                        if(!nav.Clear(position,candidate)) continue;
                        bool here=nav.SightClear(position,probe), there=nav.SightClear(candidate,probe);
                        if(here==there) continue;
                        home=here?candidate:position; edge=here?position:candidate;
                        found=true;
                    }
                if(!found) return false;
                look=probe; goal=order.destination; pass=0; total=0;
                // Visible combat starts by ducking. Unseen entry starts with a short peek.
                phase=contact?3:1; timer=0;
            }
            if(contact) look=probe; // Follow new team observations, never a stale fixed aim point.
            total+=dt;
            if(total>8) { phase=0; cooldown=2; return false; }
            watch=look-position;
            if(phase==5||phase==6)
            {
                target=edge;
                if(!nav.Clear(position,target)) { phase=0; cooldown=1; return false; }
                if(Vector2.Distance(position,target)>.12f) return true;
                if(phase==5) { phase=6; timer=.2f-.12f*skill; }
                else { timer-=dt; if(timer<=0) { phase=0; cooldown=1; return false; } }
                return true;
            }
            Vector2 exposed=Vector2.MoveTowards(home,edge,Vector2.Distance(home,edge)*(pass==0?.8f:1f));
            target=phase==1||phase==2?exposed:home;
            if(!nav.Clear(position,target)) { phase=0; pendingMiss=false; cooldown=1; return false; }
            if(Vector2.Distance(position,target)>.12f) return true;
            if(phase==1) { phase=2; timer=.65f+rng.Next01()*.3f; }
            else if(phase==3) { phase=4; timer=.25f+rng.Next01()*(.2f+.4f*skill); }
            else
            {
                timer-=dt;
                if(timer<=0)
                {
                    if(phase==2) phase=3;
                    else if(++pass<2) phase=1;
                    else { phase=0; Completed++; cooldown=7+rng.Next01()*3; return false; }
                }
            }
            return true;
        }
    }
}