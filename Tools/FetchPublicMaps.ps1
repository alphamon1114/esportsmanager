# Fetch pinned CS2 public map geometry and navigation data, verifying publisher checksums.
param([string]$Version='2000908')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$base="https://github.com/pnxenopoulos/awpy-data/releases/download/$Version"
$cache=Join-Path ([IO.Path]::GetTempPath()) ('cs2-map-import-'+[Guid]::NewGuid().ToString('N'))
New-Item $cache -ItemType Directory | Out-Null
Invoke-WebRequest "$base/manifest.json" -OutFile "$cache/manifest.json"
$manifest=Get-Content "$cache/manifest.json" -Raw | ConvertFrom-Json
if($manifest.client_version -ne $Version){throw 'Release version mismatch'}
foreach($file in @('geometry.zip','navs.zip')){
 Invoke-WebRequest "$base/$file" -OutFile "$cache/$file"
 if((Get-FileHash "$cache/$file" -Algorithm SHA256).Hash.ToLower() -ne $manifest.artifacts.$file.sha256){throw "SHA-256 mismatch: $file"}
 Expand-Archive "$cache/$file" "$cache/$($file.Replace('.zip',''))"
}
$dest=Join-Path $root "Assets/MapSources/$Version"
New-Item $dest -ItemType Directory -Force | Out-Null
foreach($map in @('de_inferno','de_dust2','de_mirage','de_nuke','de_vertigo')){
 Copy-Item "$cache/geometry/$map.mesh" "$dest/$map.awmh"
 Copy-Item "$cache/navs/$map.nav" "$dest/$map.nav"
}
Copy-Item "$cache/manifest.json" $dest
Invoke-WebRequest "$base/map_data.json" -OutFile "$dest/map_data.json"
Write-Output "Verified map sources ready: $dest. In Unity: FPS Manager > Maps > Build public map previews."
