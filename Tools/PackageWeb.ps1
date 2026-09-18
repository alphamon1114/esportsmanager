param([Parameter(Mandatory=$true)][string]$BuildPath)
$ErrorActionPreference='Stop'
$BuildPath=[IO.Path]::GetFullPath($BuildPath)
$loader=@(Get-ChildItem -LiteralPath (Join-Path $BuildPath 'Build') -Filter '*.loader.js')
$data=@(Get-ChildItem -LiteralPath (Join-Path $BuildPath 'Build') -Filter '*.data*')
$framework=@(Get-ChildItem -LiteralPath (Join-Path $BuildPath 'Build') -Filter '*.framework.js*')
$wasm=@(Get-ChildItem -LiteralPath (Join-Path $BuildPath 'Build') -Filter '*.wasm*')
if($loader.Count -ne 1 -or $data.Count -ne 1 -or $framework.Count -ne 1 -or $wasm.Count -ne 1){throw 'Expected one Unity Web build.'}
$html=@"
<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Esports Manager — Prototype</title>
<style>html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#080e16;color:#e4edf5;font:16px sans-serif}#game{position:absolute;inset:0;width:100%;height:100%;outline:none}#loading{position:absolute;inset:0;display:grid;place-content:center;text-align:center;background:#080e16;gap:16px}h1{letter-spacing:.12em;font-size:25px}progress{width:min(70vw,360px);height:12px}#message{max-width:80vw;line-height:1.6;white-space:pre-wrap}#fullscreen{position:absolute;right:12px;bottom:10px;background:#162b40cc;color:white;border:1px solid #456783;border-radius:4px;padding:7px 10px;cursor:pointer;opacity:.65}#fullscreen:hover{opacity:1}</style></head>
<body><canvas id="game" tabindex="0" aria-label="Esports Manager game"></canvas><div id="loading"><h1>ESPORTS MANAGER</h1><progress id="progress" value="0" max="1"></progress><div id="message">게임을 불러오는 중입니다…<br>Loading game…</div></div><button id="fullscreen" title="Fullscreen / 전체 화면">⛶</button>
<script src="Build/__LOADER__"></script><script>
const canvas=document.getElementById('game'),panel=document.getElementById('loading'),message=document.getElementById('message');let game;
function showError(text){panel.style.display='grid';message.textContent='게임을 시작하지 못했습니다. 새로고침 후 다시 시도해 주세요.\n'+text;console.error(text);}
createUnityInstance(canvas,{dataUrl:'Build/__DATA__',frameworkUrl:'Build/__FRAMEWORK__',codeUrl:'Build/__WASM__',streamingAssetsUrl:'StreamingAssets',companyName:'Esports Manager Prototype',productName:'Esports Manager Prototype',productVersion:'0.1.0',matchWebGLToCanvasSize:true,devicePixelRatio:Math.min(window.devicePixelRatio||1,1.5),showBanner:(text,type)=>{if(type==='error')showError(text);else console.log(text);}},p=>{document.getElementById('progress').value=p;}).then(instance=>{game=instance;panel.style.display='none';canvas.focus();}).catch(showError);
document.getElementById('fullscreen').onclick=()=>{if(game)game.SetFullscreen(1);};canvas.addEventListener('pointerdown',()=>canvas.focus());
</script></body></html>
"@
$html=$html.Replace('__LOADER__',$loader[0].Name).Replace('__DATA__',$data[0].Name).Replace('__FRAMEWORK__',$framework[0].Name).Replace('__WASM__',$wasm[0].Name)
[IO.File]::WriteAllText((Join-Path $BuildPath 'index.html'),$html,[Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'Assets/Resources/Fonts/OFL.txt') -Destination (Join-Path $BuildPath 'NanumGothic-OFL.txt')
$files=@(Get-ChildItem -LiteralPath $BuildPath -Recurse -File)
$total=($files | Measure-Object Length -Sum).Sum
if($files.Count -gt 1000 -or $total -gt 500000000 -or @($files | Where-Object Length -gt 200000000).Count -gt 0){throw 'Build exceeds itch.io HTML5 default limits.'}
foreach($file in $files){if($file.FullName.Substring($BuildPath.Length+1).Length -gt 240){throw 'File path exceeds itch.io limit.'}}
$zip=$BuildPath+'-itch.zip'
if(Test-Path -LiteralPath $zip){throw "Archive already exists: $zip"}
Compress-Archive -Path (Join-Path $BuildPath '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Output "ITCH_ZIP=$zip UNPACKED_BYTES=$total FILES=$($files.Count)"
