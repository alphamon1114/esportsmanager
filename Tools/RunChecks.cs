// Headless entry point for the automated checks. See Tools/README.md.
// Compiled together with Tools/HeadlessStub.cs and the project's own scripts;
// it is outside Assets/ so Unity never builds it.
using System;
public static class Harness
{
    public static int Main(string[] args)
    {
        var suites = new (string, Action)[] {
            ("DeploymentChecks", DeploymentChecks.Run),
            ("VisionChecks", VisionChecks.Run),
            ("CombatChecks", CombatChecks.Run),
            ("RoundChecks", RoundChecks.Run),
            ("IglOrderChecks", IglOrderChecks.Run),
            ("RoundResetChecks", RoundResetChecks.Run),
            ("AutonomyChecks", AutonomyChecks.Run),
            ("MagazineChecks", MagazineChecks.Run),
            ("ArmorChecks", ArmorChecks.Run),
            ("EconomyChecks", EconomyChecks.Run),
            ("ConditionChecks", ConditionChecks.Run),
            ("EquipmentSaveChecks", EquipmentSaveChecks.Run),
            ("WeaponDropChecks", WeaponDropChecks.Run),
            ("BombEquipmentChecks", BombEquipmentChecks.Run),
            ("TrustedDefuseChecks", TrustedDefuseChecks.Run),
            ("GunfireChecks", GunfireChecks.Run),
            ("MatchStatisticsChecks", MatchStatisticsChecks.Run),
            ("ElevationChecks", ElevationChecks.Run),
            ("UtilityDecisionChecks", UtilityDecisionChecks.Run),
            ("MovementAimChecks", MovementAimChecks.Run),
            ("EngagementFocusChecks", EngagementFocusChecks.Run),
            ("ClutchChecks", ClutchChecks.Run),
            ("ThreatChecks", ThreatChecks.Run),
            ("SideTacticChecks", SideTacticChecks.Run),
            ("AiCoachChecks", AiCoachChecks.Run),
            ("TournamentChecks", TournamentChecks.Run),
            ("FrontMenuChecks", FrontMenuChecks.Run),
            ("PauseMenuChecks", PauseMenuChecks.Run),
            ("FastForwardChecks", FastForwardChecks.Run),
            ("StatDistributionChecks", StatDistributionChecks.Run),
            ("SourceMapChecks", SourceMapChecks.Run),
            ("WebMapDataChecks", WebMapDataChecks.Run),
            ("MapSeriesChecks", MapSeriesChecks.Run),
            ("SourceGameplayChecks", SourceGameplayChecks.Run),
            ("PreAimChecks", PreAimChecks.Run),
            ("ForwardAimChecks", ForwardAimChecks.Run),
            ("AwperChecks", AwperChecks.Run),
            ("EquipmentExchangeChecks", EquipmentExchangeChecks.Run),
            ("TravelWeaponChecks", TravelWeaponChecks.Run),
            ("BackupChecks", BackupChecks.Run),
            ("TacticalMovementChecks", TacticalMovementChecks.Run),
            ("HitZoneChecks", HitZoneChecks.Run),
            ("RecoilChecks", RecoilChecks.Run),
            ("SustainedFireChecks", SustainedFireChecks.Run),
            ("PhysicalCombatChecks", PhysicalCombatChecks.Run),
            ("HumanAimChecks", HumanAimChecks.Run),
            ("PlayerTrafficChecks", PlayerTrafficChecks.Run),
            ("MovementCooperationChecks", MovementCooperationChecks.Run),
            ("CtOpeningChecks", CtOpeningChecks.Run),
            ("MidTrafficChecks", MidTrafficChecks.Run),
            ("SafeReloadChecks", SafeReloadChecks.Run),
            ("ResultHistoryChecks", ResultHistoryChecks.Run),
            ("MatchHudChecks", MatchHudChecks.Run),
            ("MatchFlowChecks", MatchFlowChecks.Run),
            ("SprayPatternChecks", SprayPatternChecks.Run),
            ("SuppressionChecks", SuppressionChecks.Run),
        };
        int failed = 0;
        foreach (var s in suites) {
            try { s.Item2(); }
            catch (Exception e) { Console.WriteLine("### " + s.Item1 + " FAILED: " + e.Message); failed++; }
        }
        Console.WriteLine("### suites failed: " + failed);
        return failed;
    }
}
