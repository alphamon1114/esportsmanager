using System;
using System.Text;
using FpsManager;
using UnityEngine;
using UnityEditor.SceneManagement;

// Regression checks for the round stage. Run with:
//   Unity.exe -batchmode -nographics -projectPath <project> -executeMethod RoundChecks.Run -quit -logFile <log>
// Success marker: ROUND_ALL_OK
public static class RoundChecks
{
    const int Seeds = 12;
    const int TickBudget = 4000;   // 200 simulated seconds, well past the round and bomb clocks

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var game = new GameObject("RoundCheck").AddComponent<Prototype>();
        game.Initialize();

        // 1. Every round finishes with a decided outcome, and the bomb invariants hold
        //    on every tick of every round.
        var outcomes = new int[8];
        bool sawFallBack = false, sawRotate = false, sawRetake = false, sawPlant = false;
        for (int seed = 1; seed <= Seeds; seed++)
        {
            var report = PlayRound(game, seed);
            outcomes[(int)report.outcome]++;
            sawFallBack |= report.fellBack;
            sawRotate |= report.rotated;
            sawRetake |= report.retook;
            sawPlant |= report.planted;
        }
        if (outcomes[(int)RoundOutcome.None] > 0) throw new Exception("A round ended without an outcome");
        Debug.Log("ROUND_CASE_OK termination " + Seeds + " rounds");

        // 2. The same seed replays exactly.
        if (PlayRound(game, 3).log != PlayRound(game, 3).log) throw new Exception("The same seed produced different rounds");
        Debug.Log("ROUND_CASE_OK determinism");

        // 3. Seeds must be able to differ, otherwise the round is on rails.
        int distinct = 0;
        for (int i = 0; i < outcomes.Length; i++) if (outcomes[i] > 0) distinct++;
        if (distinct < 2) throw new Exception("Every seed produced the same outcome");
        Debug.Log("ROUND_CASE_OK outcome-variety " + distinct + " distinct outcomes");

        // 4. The behaviour that was asked for has to actually occur. Each of these was
        //    silently absent at some point during development while the round still
        //    finished and looked plausible from the outside.
        if (!sawPlant) throw new Exception("No round reached a bomb plant");
        if (!sawFallBack) throw new Exception("No defender ever gave up a site");
        if (!sawRotate) throw new Exception("No defender ever rotated to a threatened site");
        if (!sawRetake) throw new Exception("No defender ever moved to retake a planted bomb");
        Debug.Log("ROUND_CASE_OK behaviour-present plant, fall back, rotate, retake");

        // 5. The decisions may only use what a team observed. With sight and hearing
        //    switched off, attackers standing on a site must provoke no reaction at all.
        BlindDefendersDoNotReact(game);
        Debug.Log("ROUND_CASE_OK observed-information-only");

        // 6. A bomb nobody reaches explodes on its timer.
        var quiet = StaticScenario(game, 11, false);
        if (quiet.outcome != RoundOutcome.BombExploded)
            throw new Exception("An unattended bomb did not explode: " + quiet.outcome);
        Debug.Log("ROUND_CASE_OK bomb-explodes at " + quiet.seconds.ToString("F1") + "s");

        // 7. A defender reaching an unguarded bomb defuses it.
        var defused = StaticScenario(game, 11, true);
        if (defused.outcome != RoundOutcome.BombDefused)
            throw new Exception("An unguarded bomb was not defused: " + defused.outcome);
        Debug.Log("ROUND_CASE_OK bomb-defused at " + defused.seconds.ToString("F1") + "s");

        Debug.Log("ROUND_ALL_OK: termination, determinism, outcome variety, plant and fall back and rotate and retake all occur, observed information only, bomb explodes, bomb defused.");
    }

    struct Report
    {
        public RoundOutcome outcome;
        public bool fellBack, rotated, retook, planted;
        public string log;
        public float seconds;
    }

    static Report PlayRound(Prototype game, int seed)
    {
        game.SetRoundSeed(seed);
        game.PlaceTeams();
        for (int i = 0; i < 10; i++) if (game.IsAlliedPlayer(i)) game.AssignZone(i, i % 3);
        game.BeginRound();
        var director = game.Director;
        var log = new StringBuilder();
        var report = new Report();
        int tick = 0;
        while (tick < TickBudget && director.Phase != RoundPhase.Ended)
        {
            game.SimulateMovement(.05f);
            tick++;
            bool anyRecovering = false, anyAttackerAlive = false;
            for (int i = 0; i < 10; i++)
            {
                var task = director.Objective(i).task;
                if (task == PlayerTask.FallBack) report.fellBack = true;
                if (task == PlayerTask.Rotate) report.rotated = true;
                if (task == PlayerTask.Retake || task == PlayerTask.Defuse) report.retook = true;
                if (task == PlayerTask.RecoverBomb) anyRecovering = true;
                if (game.TeamIndexOf(i) != game.CounterTerroristTeam && game.IsAlive(i)) anyAttackerAlive = true;
            }
            if (director.PlantedSite >= 0)
            {
                report.planted = true;
                if (director.Carrier >= 0) throw new Exception("A planted bomb still has a carrier");
                if (Vector2.Distance(director.BombPosition, game.Layout.Sites[director.PlantedSite]) > .01f)
                    throw new Exception("A planted bomb is not on its site");
            }
            else if (director.BombDropped && anyAttackerAlive && !anyRecovering)
                throw new Exception("A dropped bomb is not being recovered by anyone");
            if (tick % 20 != 0) continue;
            log.Append(tick).Append(':').Append((int)director.Phase).Append(':')
               .Append(game.LivingCount(0)).Append(game.LivingCount(1)).Append('|');
        }
        if (director.Phase != RoundPhase.Ended)
            throw new Exception("Round " + seed + " did not finish within " + TickBudget + " ticks");
        report.outcome = director.Outcome;
        report.log = log.ToString();
        report.seconds = tick * .05f;
        return report;
    }

    // A director driven with a vision system that cannot see or hear anything. Attackers
    // stand on a site in the open; defenders must still behave as if the site were quiet.
    static void BlindDefendersDoNotReact(Prototype game)
    {
        var blindSettings = new VisionSettings();
        blindSettings.maxRange = 0f;
        blindSettings.peripheralRange = 0f;
        blindSettings.hearingRange = 0f;
        var vision = new VisionSystem(game.Navigation, blindSettings, 10);
        var combat = new CombatSystem(new CombatSettings(), new WeaponProfile(), 10);
        combat.Reset(1);
        var layout = game.Layout;
        var director = new RoundDirector(new RoundSettings(), layout, 10);
        var team = new int[10];
        var positions = new Vector2[10];
        var facing = new Vector2[10];
        var anchors = new Vector2[10];
        var composure = new int[10];
        var alive = new bool[10];
        for (int i = 0; i < 10; i++)
        {
            team[i] = i < 5 ? 0 : 1;
            composure[i] = 50;
            facing[i] = new Vector2(1, 0);
            // Defenders sit on site A; every attacker crowds the same site.
            positions[i] = layout.Sites[0] + new Vector2(i < 5 ? -1f : 1f, i * .4f);
            anchors[i] = i < 5 ? layout.Sites[0] : layout.Sites[0];
        }
        director.Begin(1, team, 0);
        for (int tick = 0; tick < 200; tick++)
        {
            combat.FillAlive(alive);
            vision.Tick(.05f, positions, facing, team, alive);
            director.Tick(.05f, positions, team, 0, anchors, composure, vision, combat);
            for (int i = 0; i < 5; i++)
            {
                var task = director.Objective(i).task;
                if (task == PlayerTask.FallBack || task == PlayerTask.Regroup || task == PlayerTask.Rotate)
                    throw new Exception("A blind defender reacted to enemies it never observed: " + task);
            }
            if (vision.KnownCount(0) != 0) throw new Exception("A blind team registered a contact");
        }
    }

    // One attacker plants on a site with nobody near, then leaves. defuse decides whether
    // a lone defender then walks in. Everyone else is parked far away so that no shooting
    // interferes with the bomb clock.
    static Report StaticScenario(Prototype game, int seed, bool defuse)
    {
        var vision = new VisionSystem(game.Navigation, new VisionSettings(), 10);
        var combat = new CombatSystem(new CombatSettings(), new WeaponProfile(), 10);
        combat.Reset(seed);
        var layout = game.Layout;
        var settings = new RoundSettings();
        var director = new RoundDirector(settings, layout, 10);
        var team = new int[10];
        var positions = new Vector2[10];
        var facing = new Vector2[10];
        var anchors = new Vector2[10];
        var composure = new int[10];
        var alive = new bool[10];
        for (int i = 0; i < 10; i++) team[i] = i < 5 ? 0 : 1;   // 0..4 defenders, 5..9 attackers
        director.Begin(seed, team, 0);
        int carrier = director.Carrier;
        if (carrier < 0) throw new Exception("No bomb carrier was assigned");
        // Everyone is parked far enough apart that nobody ever sees, hears or shoots
        // anyone, so the bomb clock is the only thing that decides the round.
        Vector2 site = layout.Sites[director.TargetSite];
        Vector2 away = layout.Sites[(director.TargetSite + 1) % layout.SiteCount];
        Vector2 corner = new Vector2(50, 95);
        for (int i = 0; i < 10; i++)
        {
            composure[i] = 50;
            facing[i] = new Vector2(1, 0);
            positions[i] = i < 5 ? corner + new Vector2(i * .6f, 0) : away + new Vector2(i * .6f, 0);
            anchors[i] = positions[i];
        }
        anchors[carrier] = site;
        var report = new Report();
        for (int tick = 0; tick < TickBudget; tick++)
        {
            // The carrier stands on the site until the bomb is down, then clears out.
            positions[carrier] = director.PlantedSite >= 0 ? away : site;
            if (defuse && director.PlantedSite >= 0) positions[0] = site;
            combat.FillAlive(alive);
            vision.Tick(.05f, positions, facing, team, alive);
            director.Tick(.05f, positions, team, 0, anchors, composure, vision, combat);
            if (director.Phase != RoundPhase.Ended) continue;
            report.outcome = director.Outcome;
            report.seconds = tick * .05f;
            return report;
        }
        throw new Exception("The static bomb scenario did not finish");
    }
}
