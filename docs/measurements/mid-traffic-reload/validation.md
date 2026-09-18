# Mid entry, Mirage traffic and covered reload validation

2026-09-18, Unity 6000.6.0f1 / bundled Mono headless harness.

- MidTrafficChecks: all five public maps, CT and T entrance boundaries, gun inside mid, knife eligibility at spawn.
- CtOpeningChecks: Nuke CT opening and independent forward approaches at 60 and 144 FPS, damage interrupt, no 35-second expiry.
- CombatChecks: all pass, including fairness (50.5%) and aim-at-range (77.5%).
- RoundChecks: all pass, including behaviour-present, termination, observed-information-only and bomb/retake behavior.
- MagazineChecks: all 18 weapons; discard, consumption, timing, exhaustion and reset pass.
- EquipmentExchangeChecks: existing urgent/wounded-target pistol switching, slots and ammo preservation pass.
- SafeReloadChecks: exposed requests do not consume a magazine; actor moves behind a real blocker, reloads once and stays covered; quiet reload and reset pass.
- Final movement regression output is in movement.log. Body-distance tolerance matches PlayerTrafficChecks (0.995m for a nominal 1m diameter).
- Unity compilation log is in unity.log (exit code 0).

The full 53-suite runner was not rerun for this change. Relevant suites above were run independently. Earlier iterations exposed Nuke queue regressions and a Mirage final-waypoint deadlock; the final movement checks were rerun after corrections.
