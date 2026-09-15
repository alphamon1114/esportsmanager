using System;
using System.Text;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;

// Regression checks for the detection stage. Run with:
//   Unity.exe -batchmode -nographics -projectPath <project> -executeMethod VisionChecks.Run -quit -logFile <log>
// Success marker: VISION_ALL_OK
public static class VisionChecks
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var game = new GameObject("VisionCheck").AddComponent<Prototype>();
        game.Initialize();
        var navigation = game.Navigation;
        var settings = new VisionSettings();
        var probe = new VisionSystem(navigation, settings, 2);

        // 1. Open ground, observer facing the target. Placed beyond peripheralRange on
        //    purpose: inside it the cone is deliberately ignored, so a closer pair would
        //    not test the cone at all.
        Vector2 a = new Vector2(14, 20), b = new Vector2(34, 20);
        if (Vector2.Distance(a, b) <= settings.peripheralRange)
            throw new Exception("The cone cases must be measured outside peripheral range");
        if (!navigation.Clear(a, a) || !navigation.Clear(b, b)) throw new Exception("Vision probe points are not in walkable space");
        if (!navigation.SightClear(a, b)) throw new Exception("Vision probe points are not in open ground");
        if (!probe.LineOfSight(a, b - a, b)) throw new Exception("Frontal open sight line was blocked");
        Debug.Log("VISION_CASE_OK frontal");

        // 2. Same pair, observer facing away. The cone must reject it.
        if (probe.LineOfSight(a, a - b, b)) throw new Exception("A target behind the observer was seen");
        Debug.Log("VISION_CASE_OK field-of-view");

        // 3. Inside peripheral range the cone is ignored on purpose.
        Vector2 behind = a - new Vector2(settings.peripheralRange * .5f, 0);
        if (!probe.LineOfSight(a, new Vector2(1, 0), behind)) throw new Exception("Peripheral awareness did not apply at close range");
        Debug.Log("VISION_CASE_OK peripheral");

        // 4. Beyond max range nothing is seen, however clear the line is.
        if (probe.LineOfSight(a, new Vector2(1, 0), a + new Vector2(settings.maxRange + 5f, 0)))
            throw new Exception("A target beyond max range was seen");
        Debug.Log("VISION_CASE_OK range");

        // 5. Geometry blocks sight. The pair is located on the live map so the check
        //    keeps working when the map changes.
        Vector2 near = Vector2.zero, farSide = Vector2.zero;
        bool found = false;
        for (int x = 2; x < 98 && !found; x++)
            for (int y = 2; y < 90 && !found; y++)
            {
                Vector2 s = new Vector2(x + .5f, y + .5f), t = new Vector2(x + .5f, y + 8.5f);
                if (navigation.Clear(s, s) && navigation.Clear(t, t) && !navigation.SightClear(s, t)) { near = s; farSide = t; found = true; }
            }
        if (!found) throw new Exception("No wall separated pair was found on the map");
        if (probe.LineOfSight(near, farSide - near, farSide)) throw new Exception("A sight line passed through geometry");
        Debug.Log("VISION_CASE_OK obstruction");

        // 6. Exposure time, memory of a lost contact, and expiry.
        probe.Reset();
        Vector2[] positions = { a, b };
        Vector2[] facing = { b - a, a - b };
        int[] teams = { 0, 1 };
        probe.Tick(.05f, positions, facing, teams);
        if (probe.Knowledge(0, 1).visible) throw new Exception("A contact registered before the recognition time elapsed");
        for (int i = 0; i < 20; i++) probe.Tick(.05f, positions, facing, teams);
        if (!probe.Knowledge(0, 1).visible) throw new Exception("A contact did not register after one second of exposure");
        if (probe.KnownCount(1) != 1) throw new Exception("The opposing observer did not register the same pair");
        Debug.Log("VISION_CASE_OK recognition");

        positions[1] = a + new Vector2(settings.maxRange + 10f, 0);
        probe.Tick(.5f, positions, facing, teams);
        var lost = probe.Knowledge(0, 1);
        if (lost.visible) throw new Exception("A lost contact is still reported as visible");
        if (!lost.known) throw new Exception("A lost contact was forgotten immediately");
        if (Vector2.Distance(lost.lastKnownPosition, b) > .001f) throw new Exception("The last known position was not preserved");
        for (int i = 0; i < (int)(settings.memoryDuration / .5f) + 2; i++) probe.Tick(.5f, positions, facing, teams);
        if (probe.Knowledge(0, 1).known) throw new Exception("Contact memory did not expire");
        Debug.Log("VISION_CASE_OK memory");

        // 7. Information separation: at spawn neither side knows where the other is.
        game.PlaceTeams();
        for (int i = 0; i < 40; i++) game.SimulateMovement(.05f);
        for (int team = 0; team < 2; team++)
            if (game.Vision.KnownCount(team) != 0) throw new Exception("Enemies were known at spawn: team " + team);
        Debug.Log("VISION_CASE_OK information-separation");

        // 8. The same input must produce the same contacts. Checked on a scripted
        //    approach that is known to generate contacts, and on a full deployment.
        string firstApproach = RunApproach(navigation, settings), secondApproach = RunApproach(navigation, settings);
        if (firstApproach.Length == 0) throw new Exception("The determinism scenario produced no contacts to compare");
        if (firstApproach != secondApproach) throw new Exception("Detection is not deterministic");
        if (RunDeployment(game) != RunDeployment(game)) throw new Exception("Deployment detection is not deterministic");
        Debug.Log("VISION_CASE_OK determinism");

        // Diagnostic, not an assertion: which deployment zones can see each other on the
        // current map. All false means a round can start without either side ever making
        // contact, which is a map problem rather than a detection problem.
        int pairs = 0;
        for (int ct = 0; ct < 3; ct++)
            for (int t = 0; t < 3; t++)
            {
                Vector2 from = game.DeploymentZone(true, ct), to = game.DeploymentZone(false, t);
                bool clear = Vector2.Distance(from, to) <= settings.maxRange && navigation.SightClear(from, to);
                if (clear) pairs++;
                Debug.Log("VISION_ZONE_SIGHT " + Prototype.CtZoneNames[ct] + " -> " + Prototype.TZoneNames[t]
                    + " distance=" + Vector2.Distance(from, to).ToString("F1") + " sight=" + clear);
            }
        Debug.Log("VISION_ZONE_SIGHT_PAIRS " + pairs + " of 9");

        Debug.Log("VISION_ALL_OK: cone, peripheral range, max range, obstruction, recognition time, memory expiry, spawn information separation, determinism.");
    }

    // Two players closing on each other down a corridor, then losing the line again.
    static string RunApproach(DeploymentNavigation navigation, VisionSettings settings)
    {
        var probe = new VisionSystem(navigation, settings, 2);
        Vector2[] positions = { new Vector2(10.5f, 20.5f), new Vector2(27.5f, 20.5f) };
        Vector2[] facing = { new Vector2(1, 0), new Vector2(-1, 0) };
        int[] teams = { 0, 1 };
        var log = new StringBuilder();
        for (int tick = 0; tick < 60; tick++)
        {
            if (tick == 30) facing[0] = new Vector2(-1, 0);   // observer turns away
            probe.Tick(.05f, positions, facing, teams);
            var contact = probe.Knowledge(0, 1);
            if (contact.known)
                log.Append(tick).Append(':').Append(contact.visible ? 1 : 0).Append(':')
                   .Append(Mathf.RoundToInt(contact.age * 100f)).Append('|');
        }
        return log.ToString();
    }

    static string RunDeployment(Prototype game)
    {
        game.PlaceTeams();
        for (int i = 0; i < 10; i++) if (game.IsAlliedPlayer(i)) game.AssignZone(i, i % 3);
        game.BeginDeployment();
        var log = new StringBuilder();
        for (int tick = 0; tick < 1800 && !game.DeploymentComplete; tick++)
        {
            game.SimulateMovement(.05f);
            if (tick % 20 != 0) continue;
            for (int team = 0; team < 2; team++)
                for (int player = 0; player < 10; player++)
                {
                    var contact = game.Vision.Knowledge(team, player);
                    if (game.TeamIndexOf(player) == team || !contact.known) continue;
                    log.Append(tick).Append(':').Append(team).Append(':').Append(player).Append(':')
                       .Append(contact.visible ? 1 : 0).Append(':')
                       .Append(Mathf.RoundToInt(contact.lastKnownPosition.x * 10f)).Append(':')
                       .Append(Mathf.RoundToInt(contact.lastKnownPosition.y * 10f)).Append('|');
                }
        }
        if (!game.DeploymentComplete) throw new Exception("Movement did not finish during the vision check");
        return log.ToString();
    }
}
