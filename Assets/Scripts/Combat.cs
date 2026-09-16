using System;
using UnityEngine;

namespace FpsManager
{
    // Seeded splitmix32. UnityEngine.Random is never used in simulation code, so a round
    // replays identically from its seed and the automated checks stay reproducible.
    // Counter based on purpose: a plain xorshift seeded with 1, 2, 3 ... produces streams
    // whose first draws are correlated across seeds, which showed up as a measurable
    // advantage for whichever player happened to draw first.
    public sealed class DeterministicRandom
    {
        uint state;
        public DeterministicRandom(int seed) { state = (uint)seed; }
        public uint NextUInt()
        {
            state += 0x9E3779B9u;
            uint z = state;
            z ^= z >> 16; z *= 0x21F0AAADu; z ^= z >> 15; z *= 0x735A2D97u; z ^= z >> 15;
            return z;
        }
        public float Next01() { return (NextUInt() & 0xFFFFFF) / 16777216f; }
        // Bounded, roughly bell shaped, in [-1, 1]. Bounded on purpose: a normal
        // distribution would produce occasional absurd shots that read as bugs.
        public float NextSigned() { return ((Next01() + Next01() + Next01()) / 3f - .5f) * 2f; }
    }

    // Unarmoured weapon damage. Legacy damage field is the body damage.
    [Serializable]
    public class WeaponProfile
    {
        public string id = "ak_47";
        public float fireInterval = .1f;        // seconds between shots
        public float recoilPerShot=.18f, recoilRecovery=2f, recoilDelay=.25f;
        public float headMultiplier=4f;
        public int magazineSize=30; public float reloadSeconds=2.2f;
        public float HeadDamage { get { return damage*headMultiplier; } }
        public float damage = 36f;              // 3 shots to kill an unarmoured target
        // Half width of the error cone before the aim factor. A target is 0.6 units wide,
        // so the cone must be comparable to atan(0.6 / distance) for aim to matter at all:
        // at 3 degrees a duel is near certain inside 10 units and clearly skill separated
        // past 25. A smaller cone makes every reachable duel a guaranteed hit.
        public float baseSpreadDegrees = 3f;
    }

    // Test starting values, not balanced numbers.
    [Serializable]
    public class CombatSettings
    {
        public float health = 100f;
        public float reactionTime = .25f;             // contact registered -> first shot allowed
        // Human reaction is not a constant. Without this every even duel is decided by
        // array order, because both sides would finish reacting on the same tick.
        public float reactionVariance = .4f;          // +-40% of reactionTime
        public float reactionMinimum = .05f;
        public float turnDegreesPerSecond = 360f;
        public float fireAlignmentDegrees = 5f;       // must be aimed this close to shoot
        public float targetRadius = .6f;              // capsule radius used as the hit width
        public float friendlyBlockDegrees = 5f;       // hold fire with a team mate this close to the line
        public float spreadFactorAtAimZero = 1.6f;
        public float spreadFactorAtAimHundred = .5f;
    }

    public struct CombatState
    {
        public bool alive;
        public float health;
        public int target;     // player index currently engaged, -1 if none
        public float reaction; // remaining reaction delay
        public float cooldown; // remaining time before the next shot
    }

    // Minimal engagement: aim, shoot, take damage, die. Only players that have finished
    // moving engage; stopping, taking cover and repositioning belong to the next step.
    public struct KillEvent { public int killer,victim; public string weapon; public HitRegion region; }
    public enum HitRegion { Miss, Body, Head }
    public enum FollowupStyle { Spray, EvadeAndTap }
    public sealed class CombatSystem
    {
        public bool AmmoEnabled;
        public Action<KillEvent> Killed;
        public Action<int,FollowupStyle> FollowupChosen;
        public int SprayChoices { get; private set; }
        public int EvadeChoices { get; private set; }
        public Action<int> ShotFired, ReloadStarted, ShotMissed;
        readonly int[] magazine=new int[10],reserve=new int[10];
        readonly float[] reload=new float[10];
        public int Magazine(int i) { return magazine[i]; }
        public bool Reloading(int i) { return reload[i]>0; }
        public void RequestReload(int i)
        {
            if(!AmmoEnabled||!Alive(i)||reload[i]>0||magazine[i]>=WeaponFor(i).magazineSize||reserve[i]<=0) return;
            reload[i]=WeaponFor(i).reloadSeconds; if(ReloadStarted!=null) ReloadStarted(i);
        }
        readonly CombatSettings settings;
        readonly WeaponProfile weapon;
        readonly int count;
        readonly CombatState[] states;
        readonly WeaponProfile[] equipped;
        readonly DeterministicRandom[] hitRandom;
        readonly HitRegion[] lastHit;
        readonly float[] recoil,sinceShot,fireHold;
        readonly int[] movementSkill,burstShots;
        readonly bool[] choseFollowup,retap;
        readonly DeterministicRandom[] decisionRandom;
        public float Recoil(int i) { return recoil[i]; }
        public void SetMovementSkill(int i,int value) { movementSkill[i]=value; }
        public static float SprayChance(int aim,int movement) { return Mathf.Clamp(.5f+(aim-movement)/150f,.15f,.85f); }
        public int Headshots { get; private set; }
        public int Bodyshots { get; private set; }
        public HitRegion LastHit(int i) { return lastHit[i]; }
        public WeaponProfile WeaponFor(int i) { return equipped[i]??weapon; }
        public void Equip(int i,WeaponProfile profile) { equipped[i]=profile; magazine[i]=WeaponFor(i).magazineSize; reload[i]=0; }
        readonly int[] killer;
        readonly float[] shotTime, shotOffset, shotDistance;
        DeterministicRandom random;

        public int Shots { get; private set; }
        public int Hits { get; private set; }
        public int Kills { get; private set; }
        public CombatSettings Settings { get { return settings; } }
        public WeaponProfile Weapon { get { return weapon; } }

        public CombatSystem(CombatSettings settings, WeaponProfile weapon, int playerCount)
        {
            this.settings = settings ?? new CombatSettings();
            this.weapon = weapon ?? new WeaponProfile();
            count = playerCount;
            recoil=new float[count]; sinceShot=new float[count]; fireHold=new float[count]; movementSkill=new int[count]; burstShots=new int[count]; choseFollowup=new bool[count]; retap=new bool[count]; decisionRandom=new DeterministicRandom[count];
            for(int i=0;i<count;i++) movementSkill[i]=50;
            states = new CombatState[count]; equipped=new WeaponProfile[count]; hitRandom=new DeterministicRandom[count]; lastHit=new HitRegion[count];
            killer = new int[count];
            shotTime = new float[count];
            shotOffset = new float[count];
            shotDistance = new float[count];
            Reset(0);
        }

        public void Reset(int seed)
        {
            random = new DeterministicRandom(seed);
            Shots = Hits = Kills = Headshots = Bodyshots = SprayChoices = EvadeChoices = 0;
            for (int i = 0; i < count; i++)
            {
                states[i].alive = true;
                states[i].health = settings.health;
                states[i].target = -1;
                states[i].reaction = 0f;
                states[i].cooldown = 0f;
                recoil[i]=fireHold[i]=0; sinceShot[i]=1; burstShots[i]=0; choseFollowup[i]=retap[i]=false; decisionRandom[i]=new DeterministicRandom(unchecked(seed^(i+1)*15485863));
                hitRandom[i]=new DeterministicRandom(unchecked(seed^(i+1)*83492791)); lastHit[i]=HitRegion.Miss;
                killer[i] = -1; magazine[i]=WeaponFor(i).magazineSize; reserve[i]=90; reload[i]=0;
            }
        }

        public CombatState State(int player) { return states[player]; }
        public bool Alive(int player) { return states[player].alive; }
        public float Health(int player) { return states[player].health; }
        public int KilledBy(int player) { return killer[player]; }
        public bool Engaging(int player) { return states[player].alive && states[player].target >= 0; }

        public void FillAlive(bool[] buffer)
        {
            for (int i = 0; i < count; i++) buffer[i] = states[i].alive;
        }

        public int LivingCount(int[] team, int teamIndex)
        {
            int total = 0;
            for (int i = 0; i < count; i++) if (states[i].alive && team[i] == teamIndex) total++;
            return total;
        }

        public float SpreadDegrees(int aimStat)
        {
            float t = Mathf.Clamp01(aimStat / 100f);
            float factor = settings.spreadFactorAtAimZero + (settings.spreadFactorAtAimHundred - settings.spreadFactorAtAimZero) * t;
            return weapon.baseSpreadDegrees * factor;
        }

        // facing is written back for every engaging player, so the caller can apply the
        // same direction to the rendered actor and to the next vision tick.
        public void Tick(float delta, Vector2[] positions, Vector2[] facing, int[] team, bool[] arrived, int[] aimStat, VisionSystem vision)
        {
            if (positions.Length != count || facing.Length != count || team.Length != count
                || arrived.Length != count || aimStat.Length != count)
                throw new ArgumentException("Combat input length does not match the player count.");

            // Pass one: pick targets, turn, and work out when inside this tick each player
            // would be ready to shoot. Reaction and cooldown are kept as continuous
            // remainders rather than whole ticks, so two players are almost never ready at
            // the exact same instant and array order stops deciding even duels.
            int candidates = 0;
            for (int i = 0; i < count; i++)
            {
                shotTime[i] = float.MaxValue;
                sinceShot[i]+=delta; fireHold[i]=Mathf.Max(0,fireHold[i]-delta);
                if(sinceShot[i]>WeaponFor(i).recoilDelay) recoil[i]=Mathf.Max(0,recoil[i]-WeaponFor(i).recoilRecovery*delta);
                if(recoil[i]<=0&&sinceShot[i]>.35f) { burstShots[i]=0; choseFollowup[i]=false; }
                if(WeaponFor(i).id=="unarmed") { states[i].target=-1; continue; }
                if(AmmoEnabled&&states[i].alive)
                {
                    if(reload[i]>0)
                    {
                        reload[i]-=delta;
                        if(reload[i]<=0) { int rounds=Math.Min(WeaponFor(i).magazineSize-magazine[i],reserve[i]); magazine[i]+=rounds; reserve[i]-=rounds; }
                        states[i].target=-1; continue;
                    }
                    if(magazine[i]<=0) { RequestReload(i); states[i].target=-1; continue; }
                }
                if (!states[i].alive) { states[i].target = -1; continue; }
                if (!arrived[i])
                {
                    states[i].target = -1; states[i].reaction = 0f;
                    states[i].cooldown = Mathf.Max(-delta, states[i].cooldown - delta);
                    continue;
                }

                int chosen = NearestSeen(i, positions, team, vision);
                if (chosen != states[i].target)
                {
                    states[i].target = chosen;
                    states[i].reaction = chosen < 0 ? 0f
                        : Mathf.Max(settings.reactionMinimum,
                            settings.reactionTime * (1f + random.NextSigned() * settings.reactionVariance));
                }
                if (chosen < 0)
                {
                    states[i].cooldown = Mathf.Max(-delta, states[i].cooldown - delta);
                    continue;
                }

                Vector2 toTarget = positions[chosen] - positions[i];
                float distance = toTarget.magnitude;
                float offset = distance < .001f ? 0f : TurnTowards(ref facing[i], toTarget, settings.turnDegreesPerSecond * delta);
                float ready = Mathf.Max(states[i].reaction, states[i].cooldown);
                bool fires = fireHold[i]<=0 && distance >= .001f && ready < delta
                    && Mathf.Abs(offset) <= settings.fireAlignmentDegrees
                    && !FriendlyInLine(i, chosen, positions, team, distance);
                if (fires) { shotTime[i] = Mathf.Max(0f, ready); shotOffset[i] = offset; shotDistance[i] = distance; candidates++; }
                else
                {
                    // Not clamped at zero: the sub-tick remainder is what orders shots
                    // inside a tick. Clamping it would make everyone fire at time zero and
                    // hand the duel back to array order. The floor at -delta only stops
                    // the value drifting while a player is blocked from firing.
                    states[i].reaction = Mathf.Max(-delta, states[i].reaction - delta);
                    states[i].cooldown = Mathf.Max(-delta, states[i].cooldown - delta);
                }
            }

            // Pass two: resolve the shots in the order they actually happen, so a player
            // killed early in the tick does not still get to fire.
            for (int fired = 0; fired < candidates; fired++)
            {
                int next = -1;
                for (int i = 0; i < count; i++)
                    if (shotTime[i] != float.MaxValue && (next < 0 || shotTime[i] < shotTime[next])) next = i;
                if (next < 0) break;
                float at = shotTime[next];
                shotTime[next] = float.MaxValue;
                int target = states[next].target;
                if (!states[next].alive || target < 0 || !states[target].alive) continue;
                states[next].reaction = 0f;
                states[next].cooldown = at + WeaponFor(next).fireInterval - delta;
                Fire(next, target, shotOffset[next], shotDistance[next], aimStat[next]);
                if(FollowupChosen!=null&&states[target].alive&&!choseFollowup[next])
                {
                    choseFollowup[next]=true;
                    bool spray=WeaponFor(next).fireInterval<.4f&&decisionRandom[next].Next01()<SprayChance(aimStat[next],movementSkill[next]);
                    if(spray) SprayChoices++;
                    else { EvadeChoices++; fireHold[next]=.65f; retap[next]=true; }
                    FollowupChosen(next,spray?FollowupStyle.Spray:FollowupStyle.EvadeAndTap);
                }
            }
        }

        void Fire(int shooter, int target, float aimOffsetDegrees, float distance, int aimStat)
        {
            // The caller owns cooldown: it carries the sub-tick remainder that orders
            // shots. Overwriting it here would flatten every shooter to the same schedule.
            Shots++; if(AmmoEnabled) magazine[shooter]--; if(ShotFired!=null) ShotFired(shooter);
            var gun=WeaponFor(shooter);
            float aim=Mathf.Clamp01(aimStat/100f);
            float compensation=1f-.8f*aim;
            float kick=recoil[shooter]*compensation;
            float spread=gun.baseSpreadDegrees*(settings.spreadFactorAtAimZero+(settings.spreadFactorAtAimHundred-settings.spreadFactorAtAimZero)*aim);
            float error = aimOffsetDegrees + random.NextSigned() * spread + Mathf.Sin(burstShots[shooter]*1.7f)*kick*.5f;
            float lateral = Mathf.Abs(Mathf.Tan(error * Mathf.Deg2Rad)) * distance;
            // Independent stream preserves shot/reaction randomness; narrow head zone is
            // resolved from a vertical aim point and angular error, not a dodge bonus.
            bool aimHead=hitRandom[shooter].Next01()<.1f+.25f*aim;
            if(retap[shooter]) { aimHead=true; retap[shooter]=false; }
            float height=(aimHead?.8f:0)+Mathf.Tan((hitRandom[shooter].NextSigned()*spread+kick)*Mathf.Deg2Rad)*distance;
            HitRegion region=ResolveRegion(lateral,height,settings.targetRadius);
            recoil[shooter]=Mathf.Min(3,recoil[shooter]+gun.recoilPerShot); sinceShot[shooter]=0; burstShots[shooter]++;
            lastHit[shooter]=region;
            if(region==HitRegion.Miss) { if(ShotMissed!=null) ShotMissed(shooter); return; }
            Hits++; if(region==HitRegion.Head) Headshots++; else Bodyshots++;
            states[target].health -= region==HitRegion.Head?gun.HeadDamage:gun.damage;
            if (states[target].health > 0f) return;
            states[target].health = 0f;
            states[target].alive = false;
            states[target].target = -1;
            killer[target] = shooter;
            Kills++;
            if(Killed!=null) Killed(new KillEvent{killer=shooter,victim=target,weapon=gun.id,region=region});
        }

        public static HitRegion ResolveRegion(float horizontal,float height,float bodyRadius)
        {
            if(Mathf.Abs(horizontal)<=.18f&&Mathf.Abs(height-.8f)<=.18f) return HitRegion.Head;
            if(Mathf.Abs(horizontal)<=bodyRadius&&height>=-.7f&&height<=.6f) return HitRegion.Body;
            return HitRegion.Miss;
        }
        int NearestSeen(int observer, Vector2[] positions, int[] team, VisionSystem vision)
        {
            int best = -1; float distance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!states[i].alive || team[i] == team[observer]) continue;
                if (!vision.Sees(observer, i)) continue;
                float candidate = (positions[i] - positions[observer]).sqrMagnitude;
                if (candidate < distance) { distance = candidate; best = i; }
            }
            return best;
        }

        bool FriendlyInLine(int shooter, int target, Vector2[] positions, int[] team, float targetDistance)
        {
            Vector2 line = positions[target] - positions[shooter];
            for (int i = 0; i < count; i++)
            {
                if (i == shooter || !states[i].alive || team[i] != team[shooter]) continue;
                Vector2 toMate = positions[i] - positions[shooter];
                if (toMate.magnitude >= targetDistance) continue;
                if (Mathf.Abs(SignedAngle(line, toMate)) <= settings.friendlyBlockDegrees) return true;
            }
            return false;
        }

        // Rotates facing toward the target by at most maxDegrees and returns the remaining
        // offset in degrees, signed.
        public static float TurnTowards(ref Vector2 facing, Vector2 toTarget, float maxDegrees)
        {
            if (facing.sqrMagnitude < .000001f) facing = new Vector2(1, 0);
            float offset = SignedAngle(facing, toTarget);
            float step = Mathf.Clamp(offset, -maxDegrees, maxDegrees);
            float current = Mathf.Atan2(facing.y, facing.x) + step * Mathf.Deg2Rad;
            facing = new Vector2(Mathf.Cos(current), Mathf.Sin(current));
            return offset - step;
        }

        // Degrees from a to b, in [-180, 180].
        public static float SignedAngle(Vector2 a, Vector2 b)
        {
            float angle = Mathf.Atan2(b.y, b.x) - Mathf.Atan2(a.y, a.x);
            while (angle > Mathf.PI) angle -= 2f * Mathf.PI;
            while (angle < -Mathf.PI) angle += 2f * Mathf.PI;
            return angle / Mathf.Deg2Rad;
        }
    }
}
