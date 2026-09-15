using System;
using UnityEngine;

namespace FpsManager
{
    // Detection tuning. These are test starting values, not balanced numbers.
    // Distances are in map units; the map is roughly 100 x 100.
    [Serializable]
    public class VisionSettings
    {
        public float fieldOfViewDegrees = 100f;   // total horizontal cone, centred on the facing direction
        public float maxRange = 55f;              // hard sight limit
        public float peripheralRange = 5f;        // inside this radius the cone is ignored
        public float recognitionBase = .15f;      // exposure needed before a target registers
        public float recognitionPerUnit = .004f;  // extra exposure per unit of distance
        public float exposureDecay = .5f;         // share of delta removed once exposure breaks
        public float memoryDuration = 6f;         // how long a lost contact stays in team knowledge
        // Hearing. A moving enemy inside this radius is picked up through walls and from
        // behind, because sound is what gives a defender any warning at all: seeing an
        // attacker and being shot by them happen within the same second. A heard contact
        // is known but never visible, so it informs decisions and cannot be shot at.
        // Set to zero to switch hearing off.
        public float hearingRange = 18f;
        public float hearingSpeed = .5f;          // units per second before a player is audible
    }

    // What one team knows about one player. Teams share contacts with each other,
    // which stands in for voice communication until a radio system exists.
    public struct ContactInfo
    {
        public bool visible;              // a team mate has line of sight right now
        public bool heard;                // picked up by sound this tick, without being seen
        public bool known;                // visible, heard, or remembered within memoryDuration
        public Vector2 lastKnownPosition; // map coordinates of the last confirmed sighting
        public float age;                 // seconds since the last sighting; 0 while visible
        public int spotter;               // player index that holds the sighting, -1 if none
    }

    // Line-of-sight detection and per-team knowledge.
    // The system is deterministic: identical input sequences produce identical contacts.
    public sealed class VisionSystem
    {
        readonly DeploymentNavigation navigation;
        readonly VisionSettings settings;
        readonly int count;
        readonly float[] exposure;          // observer * count + target
        readonly bool[] direct;             // observer * count + target
        readonly ContactInfo[] knowledge;   // viewerTeam * count + target
        readonly int[] lastTeam;
        readonly Vector2[] previous;
        readonly float cosHalfAngle;
        bool hasPrevious;

        public VisionSystem(DeploymentNavigation navigation, VisionSettings settings, int playerCount)
        {
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (playerCount <= 0) throw new ArgumentOutOfRangeException("playerCount");
            this.navigation = navigation;
            this.settings = settings ?? new VisionSettings();
            count = playerCount;
            exposure = new float[count * count];
            direct = new bool[count * count];
            knowledge = new ContactInfo[2 * count];
            lastTeam = new int[count];
            previous = new Vector2[count];
            cosHalfAngle = Mathf.Cos(this.settings.fieldOfViewDegrees * .5f * Mathf.Deg2Rad);
            Reset();
        }

        public VisionSettings Settings { get { return settings; } }

        public void Reset()
        {
            Array.Clear(exposure, 0, exposure.Length);
            Array.Clear(direct, 0, direct.Length);
            Array.Clear(lastTeam, 0, lastTeam.Length);
            Array.Clear(previous, 0, previous.Length);
            hasPrevious = false;
            for (int i = 0; i < knowledge.Length; i++)
            {
                knowledge[i] = new ContactInfo();
                knowledge[i].spotter = -1;
            }
        }

        // Direct line of sight from one player to another as of the last tick.
        public bool Sees(int observer, int target)
        {
            if (observer < 0 || observer >= count || target < 0 || target >= count) return false;
            return direct[observer * count + target];
        }

        public ContactInfo Knowledge(int viewerTeam, int target)
        {
            if (viewerTeam < 0 || viewerTeam > 1 || target < 0 || target >= count) return new ContactInfo();
            return knowledge[viewerTeam * count + target];
        }

        // Number of opposing players the team currently has a contact on.
        public int KnownCount(int viewerTeam)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
                if (lastTeam[i] != viewerTeam && knowledge[viewerTeam * count + i].known) total++;
            return total;
        }

        public float RecognitionTime(float distance)
        {
            return settings.recognitionBase + settings.recognitionPerUnit * distance;
        }

        // Sound ignores the cone and the geometry, and only carries while the target is
        // actually moving. Returns the team mate that hears them, or -1.
        int Listener(int viewerTeam, int target, float delta, Vector2[] positions, int[] team, bool[] alive)
        {
            if (settings.hearingRange <= 0f || !hasPrevious || delta <= 0f) return -1;
            if (Vector2.Distance(positions[target], previous[target]) / delta < settings.hearingSpeed) return -1;
            for (int observer = 0; observer < count; observer++)
            {
                if (team[observer] != viewerTeam) continue;
                if (alive != null && !alive[observer]) continue;
                if (Vector2.Distance(positions[observer], positions[target]) <= settings.hearingRange) return observer;
            }
            return -1;
        }

        // Geometry test only: range, cone and obstruction. No exposure time.
        public bool LineOfSight(Vector2 eye, Vector2 facing, Vector2 target)
        {
            Vector2 delta = target - eye;
            float distance = delta.magnitude;
            if (distance > settings.maxRange) return false;
            if (distance > settings.peripheralRange)
            {
                if (facing.sqrMagnitude < .000001f) return false;
                if (Vector2.Dot(delta / Mathf.Max(distance, .000001f), facing.normalized) < cosHalfAngle) return false;
            }
            return navigation.SightClear(eye, target);
        }

        public void Tick(float delta, Vector2[] positions, Vector2[] facing, int[] team)
        {
            Tick(delta, positions, facing, team, null);
        }

        // positions and facing are map-space; facing does not need to be normalised.
        // team holds the roster team index (0 or 1) of each player.
        // alive may be null, meaning everyone is alive. A dead player neither observes
        // nor is observed, and is dropped from team knowledge at once rather than
        // lingering as a remembered contact.
        public void Tick(float delta, Vector2[] positions, Vector2[] facing, int[] team, bool[] alive)
        {
            if (positions == null || facing == null || team == null) throw new ArgumentNullException("positions");
            if (positions.Length != count || facing.Length != count || team.Length != count
                || (alive != null && alive.Length != count))
                throw new ArgumentException("Vision input length does not match the player count.");
            Array.Copy(team, lastTeam, count);

            for (int observer = 0; observer < count; observer++)
                for (int target = 0; target < count; target++)
                {
                    int i = observer * count + target;
                    bool participating = (alive == null || (alive[observer] && alive[target]));
                    if (observer == target || team[observer] == team[target] || !participating) { direct[i] = false; exposure[i] = 0; continue; }
                    float distance = Vector2.Distance(positions[observer], positions[target]);
                    float required = RecognitionTime(distance);
                    bool line = LineOfSight(positions[observer], facing[observer], positions[target]);
                    if (line) exposure[i] = Mathf.Min(exposure[i] + delta, required + 1f);
                    else exposure[i] = Mathf.Max(0f, exposure[i] - delta * settings.exposureDecay);
                    // Exposure only gates how long a visible target takes to register.
                    // Once the line breaks the contact stops being current and moves to memory.
                    direct[i] = line && exposure[i] >= required;
                }

            for (int viewerTeam = 0; viewerTeam < 2; viewerTeam++)
                for (int target = 0; target < count; target++)
                {
                    int k = viewerTeam * count + target;
                    if (alive != null && !alive[target])
                    {
                        knowledge[k].visible = false;
                        knowledge[k].known = false;
                        knowledge[k].age = 0f;
                        knowledge[k].spotter = -1;
                        continue;
                    }
                    if (team[target] == viewerTeam)
                    {
                        // Own players are always known; keeps the lookup uniform for the UI.
                        knowledge[k].visible = true;
                        knowledge[k].known = true;
                        knowledge[k].lastKnownPosition = positions[target];
                        knowledge[k].age = 0f;
                        knowledge[k].spotter = target;
                        continue;
                    }
                    int spotter = -1;
                    for (int observer = 0; observer < count; observer++)
                        if (team[observer] == viewerTeam && direct[observer * count + target]) { spotter = observer; break; }
                    int listener = spotter >= 0 ? -1 : Listener(viewerTeam, target, delta, positions, team, alive);
                    if (spotter >= 0 || listener >= 0)
                    {
                        knowledge[k].visible = spotter >= 0;
                        knowledge[k].heard = spotter < 0;
                        knowledge[k].known = true;
                        knowledge[k].lastKnownPosition = positions[target];
                        knowledge[k].age = 0f;
                        knowledge[k].spotter = spotter >= 0 ? spotter : listener;
                    }
                    else
                    {
                        knowledge[k].visible = false;
                        knowledge[k].heard = false;
                        if (!knowledge[k].known) continue;
                        knowledge[k].age += delta;
                        if (knowledge[k].age > settings.memoryDuration)
                        {
                            knowledge[k].known = false;
                            knowledge[k].spotter = -1;
                        }
                    }
                }

            Array.Copy(positions, previous, count);
            hasPrevious = true;
        }
    }
}
