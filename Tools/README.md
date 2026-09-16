# Headless check runner

Runs the project's automated checks without opening Unity. It compiles the game
logic against a small stand-in for the Unity API and calls each check entry point
directly. The whole suite takes three to four minutes, most of it the round replays that
measure behaviour over many seeds. That is still well under a batch-mode Unity
run, so this is the loop to use while iterating.

**It is not a replacement for the Unity batch checks.** It verifies logic only:
no rendering, no asset import, no scene serialisation, no `.meta` handling. Run
the Unity commands in README section 6 before committing.

## Files

| File | Role |
|---|---|
| `HeadlessStub.cs` | Minimal `UnityEngine` / `UnityEditor` stand-in. Vector and quaternion maths are real; `GameObject`, `Transform` and `Renderer` are functional; `Resources.Load` reads `Assets/Resources/*.json`; `JsonUtility` binds public fields like Unity's does. Rendering and GUI calls are accepted and ignored. |
| `RunChecks.cs` | Entry point. Runs every check suite and reports which ones failed. |

Neither file is under `Assets/`, so Unity never compiles them and they cannot
affect a build.

## Running

Needs the Mono C# compiler (`mcs`) and `mono`. On Debian or Ubuntu:
`apt-get install mono-mcs mono-runtime`.

```bash
cd <repository root>
mcs -out:/tmp/checks.exe -nowarn:0169,0414,0649,0108,0219,0660,0661 \
    Tools/HeadlessStub.cs Tools/RunChecks.cs Assets/Scripts/*.cs Assets/Editor/*.cs
mono /tmp/checks.exe
```

Expected tail:

```
### suites failed: 0
```

Each suite also prints its own success marker: `DEPLOYMENT_ALL_OK`,
`VISION_ALL_OK`, `COMBAT_ALL_OK`, `ROUND_ALL_OK`, `IGL_ALL_OK`, `RESET_ALL_OK`,
`AI_ALL_OK`.

## When a new Unity API is used

The stub only covers what the project calls today. Using something new fails at
compile time with a clear `does not contain a definition for` error; add the
member to `HeadlessStub.cs`. Keep the additions behavioural where the checks
depend on them (real maths) and inert where they do not (drawing).

## Windows (Unity bundled Mono)

Run "powershell -File Tools/RunChecks.ps1" for checks or add "-Mode Balance" for 60 paired-side trials. No extra Mono install is required when the installed Unity Editor includes MonoBleedingEdge. Use -UnityEditorPath to override the Editor folder. Balance CSV is written to the system temp folder as esportsmanager-balance-60.csv. This uses the headless stub; it does not verify rendered graphics.
