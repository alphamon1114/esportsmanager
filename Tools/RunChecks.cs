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
            ("BackupChecks", BackupChecks.Run),
            ("HitZoneChecks", HitZoneChecks.Run),
            ("RecoilChecks", RecoilChecks.Run),
            ("MatchHudChecks", MatchHudChecks.Run),
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
