using System;
using System.Text;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;

// Regression checks for the minimal engagement stage. Run with:
//   Unity.exe -batchmode -nographics -projectPath <project> -executeMethod CombatChecks.Run -quit -logFile <log>
// Success marker: COMBAT_ALL_OK
public static class CombatChecks
{
    const int Count = 2;
    static readonly int[] Teams = { 0, 1 };
    static readonly int[] Aim = { 85, 85 };

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var game = new GameObject("CombatCheck").AddComponent<Prototype>();
        game.Initialize();
        var navigation = game.Navigation;

        // 1. A face-off in the open ends with one side down, in bounded time.
        var result = FaceOff(navigation, 1, 30f);
        if (!result.decided) throw new Exception("A face-off did not resolve within the time limit");
        if (result.shots == 0) throw new Exception("No shot was fired in a face-off");
        Debug.Log("COMBAT_CASE_OK resolution shots=" + result.shots + " hits=" + result.hits + " seconds=" + result.seconds.ToString("F2"));

        // 2. The same seed replays exactly.
        if (FaceOff(navigation, 7, 30f).log != FaceOff(navigation, 7, 30f).log)
            throw new Exception("The same seed produced different rounds");
        Debug.Log("COMBAT_CASE_OK determinism");

        // 3. Different seeds must be able to differ, otherwise the draw is not being used.
        bool varied = false;
        string baseline = FaceOff(navigation, 1, 30f).log;
        for (int seed = 2; seed < 12 && !varied; seed++) varied = FaceOff(navigation, seed, 30f).log != baseline;
        if (!varied) throw new Exception("Seeds 1-11 all produced identical rounds; the draw is not being consumed");
        Debug.Log("COMBAT_CASE_OK seed-sensitivity");

        // 4. Nothing is hit through geometry. The pair is placed either side of a wall
        //    found on the live map, so the check survives map changes.
        Vector2 near = Vector2.zero, farSide = Vector2.zero;
        bool found = false;
        for (int x = 2; x < 98 && !found; x++)
            for (int y = 2; y < 90 && !found; y++)
            {
                Vector2 s = new Vector2(x + .5f, y + .5f), t = new Vector2(x + .5f, y + 8.5f);
                if (navigation.Clear(s, s) && navigation.Clear(t, t) && !navigation.SightClear(s, t)) { near = s; farSide = t; found = true; }
            }
        if (!found) throw new Exception("No wall separated pair was found on the map");
        var blocked = Engage(navigation, 3, 10f, near, farSide);
        if (blocked.shots != 0) throw new Exception("A shot was fired through geometry");
        if (blocked.health0 < 100f || blocked.health1 < 100f) throw new Exception("Damage was dealt through geometry");
        Debug.Log("COMBAT_CASE_OK obstruction");

        // 5. Out of range, in the open, nothing happens either.
        Vector2 open = new Vector2(14, 20);
        var distant = Engage(navigation, 3, 10f, open, open + new Vector2(70, 0));
        if (distant.shots != 0) throw new Exception("A shot was fired beyond detection range");
        Debug.Log("COMBAT_CASE_OK range");

        // 6. A round with no contact leaves everyone untouched: the real map still has no
        //    mutually visible deployment zones, so a normal deployment must stay bloodless.
        game.PlaceTeams();
        for (int i = 0; i < 10; i++) if (game.IsAlliedPlayer(i)) game.AssignZone(i, i % 3);
        game.BeginDeployment();
        for (int tick = 0; tick < 1800 && !game.DeploymentComplete; tick++) game.SimulateMovement(.05f);
        if (!game.DeploymentComplete) throw new Exception("Movement did not finish during the combat check");
        for (int i = 0; i < 10; i++)
        {
            if (!game.IsAlive(i)) throw new Exception("A player died without any contact: " + i);
            if (game.Combat.Health(i) < game.Combat.Settings.health) throw new Exception("A player took damage without any contact: " + i);
        }
        if (game.Combat.Shots != 0) throw new Exception("Shots were fired without any contact");
        Debug.Log("COMBAT_CASE_OK no-contact-no-damage");

        // 7. Equal players must win equally often. This failed twice during development:
        //    once because a fixed reaction time made array order decide every duel, and
        //    once because Fire overwrote the sub-tick cooldown that ordered shots within
        //    a tick. Both looked correct in a single round and only showed up in bulk.
        Vector2 close = new Vector2(14, 20), closeFoe = new Vector2(30, 20);
        float evenRate = WinRate(navigation, close, closeFoe, 85, 85, 200);
        if (evenRate < 42f || evenRate > 58f)
            throw new Exception("Equal players are not evenly matched: first player won " + evenRate.ToString("F1") + "% (expected about 50)");
        Debug.Log("COMBAT_CASE_OK fairness firstPlayerWins=" + evenRate.ToString("F1") + "%");

        // 8. Aim must decide long duels. If the error cone is small next to the target
        //    width every shot hits and the stat does nothing.
        Vector2 longFrom = new Vector2(17.5f, 81.5f), longTo = new Vector2(71.5f, 91.5f);
        if (!LongLine(navigation, ref longFrom, ref longTo))
            Debug.Log("COMBAT_NOTE no sight line of 40 units or more on this map; aim-at-range check skipped");
        else
        {
            float skilledRate = WinRate(navigation, longFrom, longTo, 98, 72, 200);
            if (skilledRate <= 58f)
                throw new Exception("Aim does not decide long duels: aim 98 won only " + skilledRate.ToString("F1") + "% against aim 72 at "
                    + Vector2.Distance(longFrom, longTo).ToString("F1") + " units");
            Debug.Log("COMBAT_CASE_OK aim-at-range aim98Wins=" + skilledRate.ToString("F1") + "% at "
                + Vector2.Distance(longFrom, longTo).ToString("F1") + " units");
        }

        Debug.Log("COMBAT_ALL_OK: resolution, seed replay, seed sensitivity, obstruction, range, no contact means no damage, fairness, aim at range.");
    }

    static float WinRate(DeploymentNavigation navigation, Vector2 a, Vector2 b, int aimA, int aimB, int rounds)
    {
        int wins = 0, decided = 0;
        for (int seed = 1; seed <= rounds; seed++)
        {
            int winner = Winner(navigation, seed, a, b, aimA, aimB);
            if (winner < 0) continue;
            decided++; if (winner == 0) wins++;
        }
        if (decided == 0) throw new Exception("No duel resolved while measuring win rate");
        return 100f * wins / decided;
    }

    static int Winner(DeploymentNavigation navigation, int seed, Vector2 a, Vector2 b, int aimA, int aimB)
    {
        var vision = new VisionSystem(navigation, new VisionSettings(), Count);
        var combat = new CombatSystem(new CombatSettings(), new WeaponProfile(), Count);
        combat.Reset(seed);
        Vector2[] positions = { a, b };
        Vector2[] facing = { (b - a).normalized, (a - b).normalized };
        bool[] arrived = { true, true };
        bool[] alive = new bool[Count];
        int[] aim = { aimA, aimB };
        for (int tick = 0; tick < 1200; tick++)
        {
            combat.FillAlive(alive);
            vision.Tick(.05f, positions, facing, Teams, alive);
            combat.Tick(.05f, positions, facing, Teams, arrived, aim, vision);
            if (!combat.Alive(0) || !combat.Alive(1)) return combat.Alive(0) ? 0 : 1;
        }
        return -1;
    }

    // Keeps the suggested pair when the map still supports it, otherwise looks for any
    // clear line of at least 40 units so the check survives map edits.
    static bool LongLine(DeploymentNavigation navigation, ref Vector2 from, ref Vector2 to)
    {
        if (navigation.Clear(from, from) && navigation.Clear(to, to) && navigation.SightClear(from, to)) return true;
        for (int x = 1; x < 100; x += 3)
            for (int y = 1; y < 100; y += 3)
            {
                Vector2 p = new Vector2(x + .5f, y + .5f);
                if (!navigation.Clear(p, p)) continue;
                for (int x2 = 1; x2 < 100; x2 += 3)
                    for (int y2 = 1; y2 < 100; y2 += 3)
                    {
                        Vector2 q = new Vector2(x2 + .5f, y2 + .5f);
                        float distance = Vector2.Distance(p, q);
                        if (distance < 40f || distance > 55f) continue;
                        if (!navigation.Clear(q, q) || !navigation.SightClear(p, q)) continue;
                        from = p; to = q; return true;
                    }
            }
        return false;
    }

    struct Result { public bool decided; public int shots, hits; public float seconds, health0, health1; public string log; }

    static Result FaceOff(DeploymentNavigation navigation, int seed, float limit)
    {
        return Engage(navigation, seed, limit, new Vector2(14, 20), new Vector2(30, 20));
    }

    // Two stationary players facing each other. Friendly fire is impossible with one
    // player per side, so case 6 covers the team case through the full prototype.
    static Result Engage(DeploymentNavigation navigation, int seed, float limit, Vector2 a, Vector2 b)
    {
        var vision = new VisionSystem(navigation, new VisionSettings(), Count);
        var combat = new CombatSystem(new CombatSettings(), new WeaponProfile(), Count);
        combat.Reset(seed);
        Vector2[] positions = { a, b };
        Vector2[] facing = { (b - a).normalized, (a - b).normalized };
        bool[] arrived = { true, true };
        bool[] alive = new bool[Count];
        var log = new StringBuilder();
        var result = new Result();
        float clock = 0f;
        while (clock < limit)
        {
            combat.FillAlive(alive);
            vision.Tick(.05f, positions, facing, Teams, alive);
            combat.Tick(.05f, positions, facing, Teams, arrived, Aim, vision);
            clock += .05f;
            log.Append(Mathf.RoundToInt(combat.Health(0))).Append(',').Append(Mathf.RoundToInt(combat.Health(1))).Append('|');
            if (combat.Alive(0) && combat.Alive(1)) continue;
            result.decided = true;
            break;
        }
        // A dead player must stop acting: keep ticking and confirm nothing changes.
        if (result.decided)
        {
            int shotsAtDeath = combat.Shots;
            int down = combat.Alive(0) ? 1 : 0, survivor = 1 - down;
            float survivorHealth = combat.Health(survivor);
            for (int i = 0; i < 40; i++)
            {
                combat.FillAlive(alive);
                vision.Tick(.05f, positions, facing, Teams, alive);
                combat.Tick(.05f, positions, facing, Teams, arrived, Aim, vision);
            }
            if (combat.Alive(down)) throw new Exception("A dead player came back");
            if (combat.Health(down) != 0f) throw new Exception("A dead player's health changed after death");
            if (vision.Knowledge(1 - Teams[down], down).known) throw new Exception("A dead player is still a live contact");
            if (combat.Shots != shotsAtDeath) throw new Exception("Shots continued after the last opponent was down");
            if (combat.Health(survivor) != survivorHealth) throw new Exception("The survivor took damage from a dead opponent");
        }
        result.shots = combat.Shots; result.hits = combat.Hits; result.seconds = clock;
        result.health0 = combat.Health(0); result.health1 = combat.Health(1);
        result.log = log.ToString();
        return result;
    }
}
