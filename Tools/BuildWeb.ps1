param(
 [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe',
 [string]$Output
)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
if(-not $Output){$Output=Join-Path $project ('Builds\WebGL-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$Output=[IO.Path]::GetFullPath($Output)
$workspace=Join-Path $env:TEMP ('esports-web-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $workspace,$Output -Force | Out-Null
foreach($folder in @('Assets','Packages','ProjectSettings')){
 & robocopy.exe (Join-Path $project $folder) (Join-Path $workspace $folder) /E /NFL /NDL /NJH /NJS /NP
 if($LASTEXITCODE -ge 8){throw "Copy failed: $folder"}
}
$log=Join-Path (Split-Path $Output -Parent) ((Split-Path $Output -Leaf)+'.log')
@{workspace=$workspace;output=$Output;log=$log} | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path (Split-Path $Output -Parent) 'latest-web-build.json')
$env:ESPORTS_WEB_OUTPUT=$Output
try{
 $process=Start-Process -FilePath $UnityEditor -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-nographics','-quit','-buildTarget','WebGL','-projectPath',('"'+$workspace+'"'),'-executeMethod','WebReleaseBuild.Build','-logFile',('"'+$log+'"'))
 Write-Output "BUILD_PID=$($process.Id) LOG=$log"
 $process.WaitForExit()
 if($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $Output 'index.html'))){throw "Unity Web build failed. Read $log"}
 & (Join-Path $PSScriptRoot 'PackageWeb.ps1') -BuildPath $Output
}finally{Remove-Item Env:ESPORTS_WEB_OUTPUT -ErrorAction SilentlyContinue}
