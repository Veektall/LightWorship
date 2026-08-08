param([string]$Repository,[string]$Commit,[string]$RunId)
$ErrorActionPreference='Stop'
$base=Get-Content ./.newage-overlay/build-v4.ps1 -Raw
$base=$base.Replace("'OfflineSpeechServices.cs','CameraCaptureService.cs','OperationalIntelligenceServices.cs','RemoteRelayClient.cs'","'OfflineSpeechServices.cs','CameraCaptureService.cs','OperationalIntelligenceServices.Safe.cs','RemoteRelayClient.cs','AdvancedLocalServices.cs'")
# Add the isolated restricted legacy web worker before model acquisition/build.
$inject=@'
# Add isolated restricted web source worker.
$web=Join-Path $root 'src/NewAgeWorship.WebWorker'; $web.mkdir(parents=True,exist_ok=True)
shutil.copy2(overlay/'NewAgeWorship.WebWorker.csproj',web/'NewAgeWorship.WebWorker.csproj')
shutil.copy2(overlay/'WebWorker.Program.cs',web/'Program.cs')
'@
$base=$base.Replace("(root/'TEST_EVIDENCE/source-normalization.txt').write_text",$inject+"`n(root/'TEST_EVIDENCE/source-normalization.txt').write_text")
# Build the web worker after the intelligence worker.
$needle="msbuild NEWAGE_WORSHIP/src/NewAgeWorship.IntelligenceWorker/NewAgeWorship.IntelligenceWorker.csproj /restore /p:Configuration=Release /v:minimal 2>&1|Tee-Object NEWAGE_WORSHIP/TEST_EVIDENCE/intelligence-worker-build.log`nif(`$LASTEXITCODE-ne0){exit `$LASTEXITCODE}"
$extra=$needle+"`nmsbuild NEWAGE_WORSHIP/src/NewAgeWorship.WebWorker/NewAgeWorship.WebWorker.csproj /restore /p:Configuration=Release /v:minimal 2>&1|Tee-Object NEWAGE_WORSHIP/TEST_EVIDENCE/web-worker-build.log`nif(`$LASTEXITCODE-ne0){exit `$LASTEXITCODE}"
$base=$base.Replace($needle,$extra)
# Package the isolated web worker with the installer.
$base=$base.Replace("'Source: \"..\\src\\NewAgeWorship.IntelligenceWorker\\bin\\Release\\net48\\*\"; DestDir: \"{app}\\IntelligenceWorker\"; Flags: ignoreversion recursesubdirs createallsubdirs'","'Source: \"..\\src\\NewAgeWorship.IntelligenceWorker\\bin\\Release\\net48\\*\"; DestDir: \"{app}\\IntelligenceWorker\"; Flags: ignoreversion recursesubdirs createallsubdirs',`n'Source: \"..\\src\\NewAgeWorship.WebWorker\\bin\\Release\\net48\\*\"; DestDir: \"{app}\\WebWorker\"; Flags: ignoreversion recursesubdirs createallsubdirs'")
# Require it in install smoke evidence.
$base=$base.Replace("`$worker=Join-Path `$d IntelligenceWorker/NewAgeWorship.IntelligenceWorker.exe;`$model=Join-Path `$d models/vosk-model-small-en-us-0.15","`$worker=Join-Path `$d IntelligenceWorker/NewAgeWorship.IntelligenceWorker.exe;`$webWorker=Join-Path `$d WebWorker/NewAgeWorship.WebWorker.exe;`$model=Join-Path `$d models/vosk-model-small-en-us-0.15")
$base=$base.Replace("Need `$app;Need `$host;Need `$worker;Need `$model","Need `$app;Need `$host;Need `$worker;Need `$webWorker;Need `$model")
$base=$base.Replace("intelligence worker and local Vosk model installed","intelligence worker, restricted web worker and local Vosk model installed")
Set-Content ci-tmp-build-v5.ps1 $base -Encoding UTF8
& ./ci-tmp-build-v5.ps1 -Repository $Repository -Commit $Commit -RunId $RunId
exit $LASTEXITCODE
