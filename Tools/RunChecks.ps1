param(
    [ValidateSet('Checks','Balance')][string]$Mode='Checks',
    [string]$UnityEditorPath
)
$ErrorActionPreference='Stop'
$projectPath=Split-Path $PSScriptRoot -Parent
if(-not $UnityEditorPath) {
    $version=((Get-Content (Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt') | Select-String '^m_EditorVersion:').Line -split ': ')[1]
    $UnityEditorPath=Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor"
}
$monoRoot=Join-Path $UnityEditorPath 'Data\MonoBleedingEdge'
$monoExe=Join-Path $monoRoot 'bin\mono.exe'
$compiler=Join-Path $monoRoot 'lib\mono\4.5\mcs.exe'
if(-not (Test-Path $monoExe) -or -not (Test-Path $compiler)) { throw 'Mono compiler not found. Pass -UnityEditorPath for your installed Unity Editor folder.' }
$runDirectory=Join-Path ([IO.Path]::GetTempPath()) ('esportsmanager-check-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDirectory | Out-Null
Push-Location $projectPath
try {
    $entry=Join-Path $PSScriptRoot 'RunChecks.cs'
    if($Mode -eq 'Balance') {
        $entry=Join-Path $runDirectory 'Entry.cs'
        [IO.File]::WriteAllText($entry,'public static class Entry { public static void Main() { BalanceMeasure.Run(); } }')
    }
    $sources=@((Join-Path $PSScriptRoot 'HeadlessStub.cs'),$entry)+@(Get-ChildItem Assets\Scripts\*.cs,Assets\Editor\*.cs | ForEach-Object FullName)
    $exe=Join-Path $runDirectory 'run.exe'
    & $monoExe $compiler '-nowarn:0169,0414,0649,0108,0219,0660,0661' "-out:$exe" @sources
    if($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
    & $monoExe $exe
    if($LASTEXITCODE -ne 0) { throw 'Checks failed; inspect the output above' }
} finally { Pop-Location }