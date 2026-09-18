using UnityEngine;
namespace FpsManager
{
    // Chooses a world-space aim point from team knowledge, independently of velocity.
    // No enemy transforms or movement statistics are inputs.
    public sealed class MovementAim
    {
        int focus=-1; public bool LocalThreatPriority;
        public Vector2 Point { get; private set; }
        public bool HasContact { get; private set; }
        public Vector2 Choose(int player,Vector2 position,Vector2 fallback,int[] teams,VisionSystem vision)
        {
            int best=-1;float score=float.MinValue;Vector2 point=fallback;
            for(int enemy=0;enemy<teams.Length;enemy++)
            {
                if(teams[enemy]==teams[player]) continue;
                var known=vision.Knowledge(teams[player],enemy);
                if(!known.known||known.age>2.5f) continue;
                float distance=Vector2.Distance(position,known.lastKnownPosition);
                if(distance>vision.Settings.maxRange) continue;
                float priority=(vision.Sees(player,enemy)?100:known.visible?60:known.anonymous?0:20)-known.age*6-distance*.3f;
                if(LocalThreatPriority)priority=ThreatRules.Priority(vision.Sees(player,enemy),distance<22, distance,known.age);
                if(enemy==focus) priority+=8; // Avoid switching between similar contacts every tick.
                if(priority<=score) continue;
                best=enemy;score=priority;point=known.lastKnownPosition;
            }
            focus=best;HasContact=best>=0;Point=point;
            return point-position;
        }
    }
}
