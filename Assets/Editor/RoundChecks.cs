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

        // 8. Defenders have to be looking at the way in. This is measured rather than
        //    asserted structurally, because it broke twice from unrelated changes: once
        //    when the watch direction degenerated, and once when an idle patrol kept
        //    guards walking so their facing followed their feet instead of their angle.
        //    Both times the round still finished and nothing else complained.
        var facing = DefenderFacingAtDeath(game);
        if (facing.deaths < 40) throw new Exception("Not enough defender deaths to judge facing: " + facing.deaths);
        if (facing.Share < 55f)
            throw new Exception("Defenders are dying to enemies they never faced: only "
                + facing.Share.ToString("F0") + "% of killers were inside the victim's field of view (want 55% or more)");
        Debug.Log("ROUND_CASE_OK defender-facing " + facing.Share.ToString("F0") + "% of killers were in front of the defender");

        // 9. Holding from cover and trading a dead team mate both have to happen.
        if (!facing.ducked) throw new Exception("No defender ever stepped off their angle into cover");
        if (!facing.traded) throw new Exception("No attacker ever traded a team mate");
        if (!facing.threwEntering) throw new Exception("No attacker ever used utility while taking a site");
        Debug.Log("ROUND_CASE_OK cover-trade-entry-utility");

        // 10. Enemies with a clear line between them have to end up fighting. Two separate
        //     changes have broken this without breaking anything else: view locked to the
        //     direction of travel, so crossing paths never looked at each other, and smoke
        //     thrown at every noise, which put a third of all sight lines behind a wall of
        //     it. Both times rounds still finished and every other check passed.
        var contact = ContactRate(game);
        if (contact.Seen < 25f)
            throw new Exception("Enemies in plain sight of each other are not engaging: only "
                + contact.Seen.ToString("F0") + "% of clear sight lines were actually seen (want 25% or more)");
        if (contact.Smoked > 25f)
            throw new Exception("Smoke is blanketing the map: " + contact.Smoked.ToString("F0")
                + "% of clear sight lines run through it (want under 25%)");
        Debug.Log("ROUND_CASE_OK engagement " + contact.Seen.ToString("F0") + "% of clear sight lines seen, "
            + contact.Smoked.ToString("F0") + "% behind smoke");

        Debug.Log("ROUND_ALL_OK: termination, determinism, outcome variety, plant and fall back and rotate and retake all occur, observed information only, bomb explodes, bomb defused, defenders face the approach, cover and trade and entry utility all occur, enemies in sight engage.");
    }

    struct Contact
    {
        public long clear, seen, smoked;
        public float Seen { get { return clear == 0 ? 0f : 100f * seen / clear; } }
        public float Smoked { get { return clear == 0 ? 0f : 100f * smoked / clear; } }
    }

    // How often a clear geometric line between two enemies turns into one of them actually
    // seeing the other, and how much of the rest is behind the players' own smoke.
    static Contact ContactRate(Prototype game)
    {
        var result = new Contact();
        var range = new VisionSettings().maxRange;
        for (int seed = 1; seed <= 10; seed++)
        {
            game.SetRoundSeed(seed); game.PlaceTeams(); game.PrepareIglOrders(); game.BeginRound();
            var director = game.Director;
            for (int tick = 0; tick < TickBudget && director.Phase != RoundPhase.Ended; tick++)
            {
                game.SimulateMovement(.05f);
                for (int a = 0; a < 10; a++)
                    for (int b = 0; b < 10; b++)
                    {
                        if (a == b || game.TeamIndexOf(a) == game.TeamIndexOf(b)) continue;
                        if (!game.IsAlive(a) || !game.IsAlive(b)) continue;
                        Vector2 from = game.MapPosition(a), to = game.MapPosition(b);
                        if (Vector2.Distance(from, to) > range || !game.Navigation.SightClear(from, to)) continue;
                        result.clear++;
                        if (game.Vision.Sees(a, b)) result.seen++;
                        else if (!game.Autonomy.ClearSight(from, to)) result.smoked++;
                    }
            }
        }
        if (result.clear == 0) throw new Exception("No enemy pair ever had a clear line between them");
        return result;
    }

    struct Facing
    {
        public int deaths, faced;
        public bool ducked, traded, threwEntering;
        public float Share { get { return deaths == 0 ? 0f : 100f * faced / deaths; } }
    }

    // Replays rounds and records, for every defender killed, the angle between the way
    // they were looking and whoever shot them.
    static Facing DefenderFacingAtDeath(Prototype game)
    {
        var result = new Facing();
        var standing = new bool[10];
        var facing = new Vector2[10];
        var positions = new Vector2[10];
        for (int seed = 1; seed <= 20; seed++)
        {
            game.SetRoundSeed(seed); game.PlaceTeams(); game.PrepareIglOrders(); game.BeginRound();
            var director = game.Director;
            int defenders = game.CounterTerroristTeam;
            for (int i = 0; i < 10; i++) standing[i] = true;
            int throwsSoFar = 0;
            for (int tick = 0; tick < TickBudget && director.Phase != RoundPhase.Ended; tick++)
            {
                for (int i = 0; i < 10; i++) { positions[i] = game.MapPosition(i); facing[i] = game.MapFacing(i); }
                game.SimulateMovement(.05f);
                for (int i = 0; i < 10; i++)
                {
                    if (game.IsAlive(i))
                    {
                        var objective = director.Objective(i);
                        if (objective.task == PlayerTask.Trade) result.traded = true;
                        if (objective.task == PlayerTask.DefendSite && objective.hasCover
                            && Vector2.Distance(objective.cover, objective.destination) > 1f
                            && Vector2.Distance(game.MapPosition(i), objective.cover) < 2f) result.ducked = true;
                        continue;
                    }
                    if (!standing[i]) continue;
                    standing[i] = false;
                    if (game.TeamIndexOf(i) != defenders) continue;
                    int killer = game.Combat.KilledBy(i);
                    if (killer < 0) continue;
                    result.deaths++;
                    float angle = Mathf.Abs(CombatSystem.SignedAngle(facing[i], positions[killer] - positions[i]));
                    if (angle <= 50f) result.faced++;
                }
                if (game.Autonomy.Throws > throwsSoFar)
                {
                    throwsSoFar = game.Autonomy.Throws;
                    if (director.Phase == RoundPhase.Execute) result.threwEntering = true;
                }
            }
        }
        return result;
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
