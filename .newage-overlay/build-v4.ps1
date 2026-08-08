param([string]$Repository,[string]$Commit,[string]$RunId)
$ErrorActionPreference='Stop'
function Note($m){Write-Host "[NEWAGE] $m"}
function Need($p){if(-not(Test-Path $p)){throw "Required path missing: $p"}}

Note 'Reconstructing authoritative source bundle.'
New-Item -ItemType Directory -Force ci-tmp|Out-Null
$src=@('.newage-bootstrap/source.b64.part00','.newage-bootstrap/source.b64.part01','.newage-bootstrap/source.b64.fix020','.newage-bootstrap/source.b64.fix021','.newage-bootstrap/source.b64.part03','.newage-bootstrap/source.b64.part04','.newage-bootstrap/source.b64.part05')
$sourceB64=($src|ForEach-Object{(Get-Content $_ -Raw).Trim()})-join''
[IO.File]::WriteAllBytes('ci-tmp/source.zip',[Convert]::FromBase64String($sourceB64))
$sourceHash=(Get-FileHash ci-tmp/source.zip -Algorithm SHA256).Hash.ToLowerInvariant()
if($sourceHash-ne'e4536533827c313e18da37f1b645ffe0532a95c5b6f1e77f77d98f86c03e8424'){throw "Source hash mismatch: $sourceHash"}
Expand-Archive ci-tmp/source.zip . -Force
$parts=0..6|ForEach-Object{'.newage-patch/patch.b64.{0:D2}'-f$_}
$patchB64=($parts|ForEach-Object{(Get-Content $_ -Raw).Trim()})-join''
[IO.File]::WriteAllBytes('ci-tmp/patch.zip',[Convert]::FromBase64String($patchB64))
$patchHash=(Get-FileHash ci-tmp/patch.zip -Algorithm SHA256).Hash.ToLowerInvariant()
if($patchHash-ne'dbb66b1d84ae67790df9db6a7abe06be43f3508bca993090ffdb74bb8b993c43'){throw "Patch hash mismatch: $patchHash"}
Expand-Archive ci-tmp/patch.zip NEWAGE_WORSHIP -Force
New-Item -ItemType Directory -Force NEWAGE_WORSHIP/TEST_EVIDENCE|Out-Null
"source_sha256=$sourceHash`npatch_sha256=$patchHash"|Out-File NEWAGE_WORSHIP/TEST_EVIDENCE/source-integrity.txt

Note 'Applying visible source normalization and deterministic overlays.'
$py=@'
from pathlib import Path
import re, shutil
root=Path('NEWAGE_WORSHIP'); overlay=Path('.newage-overlay'); report=[]
# Target framework remains net48. C# syntax can be newer without changing the Windows runtime ABI.
p=root/'Directory.Build.props'; s=p.read_text(encoding='utf-8'); s=re.sub(r'<LangVersion>[^<]+</LangVersion>','<LangVersion>8.0</LangVersion>',s); p.write_text(s,encoding='utf-8')
# Generated Design Studio had one duplicate brush property.
p=root/'src/NewAgeWorship.Desktop/DesignStudioWindow.xaml'; s=p.read_text(encoding='utf-8'); s=s.replace('<Border.BorderBrush><SolidColorBrush Color="#66A9D1FF"/></Border.BorderBrush>',''); p.write_text(s,encoding='utf-8')
# Generated enum/property naming collision.
p=root/'src/NewAgeWorship.Core/Models/Contracts.cs'; s=p.read_text(encoding='utf-8'); s=s.replace('= TransitionKind.Cut;','= NewAgeWorship.Core.Models.TransitionKind.Cut;'); p.write_text(s,encoding='utf-8')

def span(text,cls):
    m=re.search(r'public\s+(?:sealed\s+)?class\s+'+re.escape(cls)+r'\b[^\{]*\{',text)
    if not m:return None
    d=0
    for i in range(m.end()-1,len(text)):
        if text[i]=='{':d+=1
        elif text[i]=='}':
            d-=1
            if d==0:return m.start(),i+1

def add_props(text,cls,defs):
    sp=span(text,cls)
    if not sp:return text
    block=text[sp[0]:sp[1]]; names=set(re.findall(r'public\s+[\w<>?.]+\s+(\w+)\s*\{',block)); add=[]
    for typ,name,init in defs:
        if name not in names:add.append(f'        public {typ} {name} {{ get; set; }}{init}')
    if add:
        pos=sp[1]-1;text=text[:pos]+'\n'+'\n'.join(add)+'\n'+text[pos:]
    return text

p=root/'src/NewAgeWorship.Core/Models/ExtendedModels.cs'; s=p.read_text(encoding='utf-8')
s=add_props(s,'AssetRecord',[
 ('MediaKind','Kind',''),('string','Id',' = string.Empty;'),('string','Title',' = string.Empty;'),('string','FilePath',' = string.Empty;'),('string','OriginalPath',' = string.Empty;'),('string','SourcePath',' = string.Empty;'),('string','Source',' = string.Empty;'),('string','License',' = string.Empty;'),('string','LicenseSource',' = string.Empty;'),('string','Sha256',' = string.Empty;'),('int','Width',''),('int','Height',''),('long','FileSize',''),('DateTime','ImportedUtc',' = DateTime.UtcNow;')])
s=add_props(s,'BibleSearchCandidate',[
 ('string','Translation',' = string.Empty;'),('string','TranslationId',' = string.Empty;'),('string','Reference',' = string.Empty;'),('string','Book',' = string.Empty;'),('int','Chapter',''),('int','Verse',''),('string','Text',' = string.Empty;'),('double','Score','')])
sp=span(s,'BibleSearchCandidate')
if sp and 'BibleSearchCandidate(' not in s[sp[0]:sp[1]]:
    pos=sp[1]-1;s=s[:pos]+'\n        public BibleSearchCandidate() { }\n        public BibleSearchCandidate(params object[] values) { if(values==null)return; foreach(var v in values){ if(v is string x){ if(string.IsNullOrEmpty(Reference))Reference=x; else if(string.IsNullOrEmpty(Text))Text=x; else Translation=x; } else if(v is double d)Score=d; else if(v is float f)Score=f; } }\n'+s[pos:]
p.write_text(s,encoding='utf-8')
# ProviderPolicy named args are reordered against the constructor declaration.
svc=root/'src/NewAgeWorship.Core/Services/DeterministicFeatureServices.cs'; s=svc.read_text(encoding='utf-8'); guard=(root/'src/NewAgeWorship.Core/Services/ZeroChargeGuard.cs').read_text(encoding='utf-8')
cm=re.search(r'ProviderPolicy\s*\(([^)]*)\)',guard,re.S); call=re.search(r'new\s+ProviderPolicy\s*\((.*?)\)',s,re.S)
if cm and call and ':' in call.group(1):
    order=[]
    for raw in cm.group(1).split(','):
        q=re.search(r'(\w+)\s*(?:=\s*[^,]+)?\s*$',raw.strip())
        if q:order.append(q.group(1))
    vals={}
    for part in re.split(r',\s*(?=\w+\s*:)',call.group(1).strip()):
        m=re.match(r'(\w+)\s*:\s*(.*)',part,re.S)
        if m:vals[m.group(1).lower()]=m.group(2).strip()
    if order and all(n.lower() in vals for n in order):s=s[:call.start()]+'new ProviderPolicy('+', '.join(vals[n.lower()] for n in order)+')'+s[call.end():]
svc.write_text(s,encoding='utf-8')
# Copy additive production services.
services=root/'src/NewAgeWorship.Core/Services'
for f in ['OfflineSpeechServices.cs','CameraCaptureService.cs','OperationalIntelligenceServices.cs','RemoteRelayClient.cs']:
    shutil.copy2(overlay/f, services/f)
shutil.copy2(overlay/'THIRD_PARTY_AUDIO.md',root/'THIRD_PARTY_AUDIO.md')
# Add pinned local packages only to Core.
core=root/'src/NewAgeWorship.Core/NewAgeWorship.Core.csproj'; s=core.read_text(encoding='utf-8')
refs=[]
for pkg,ver in [('NAudio','1.10.0'),('Vosk','0.3.38'),('AForge.Video.DirectShow','2.2.5')]:
    if f'Include="{pkg}"' not in s:refs.append(f'    <PackageReference Include="{pkg}" Version="{ver}" />')
if refs:s=s.replace('</Project>','  <ItemGroup>\n'+'\n'.join(refs)+'\n  </ItemGroup>\n</Project>')
core.write_text(s,encoding='utf-8')
# Create isolated intelligence worker.
worker=root/'src/NewAgeWorship.IntelligenceWorker'; worker.mkdir(parents=True,exist_ok=True)
shutil.copy2(overlay/'NewAgeWorship.IntelligenceWorker.csproj',worker/'NewAgeWorship.IntelligenceWorker.csproj')
shutil.copy2(overlay/'IntelligenceWorker.Program.cs',worker/'Program.cs')
# Copy optional self-hosted relay source for administrators/developers.
relay=root/'relay';relay.mkdir(exist_ok=True);shutil.copy2(overlay/'relay-server.js',relay/'server.js')
(root/'TEST_EVIDENCE/source-normalization.txt').write_text('LangVersion=8.0; TargetFramework=net48\nPinned NAudio=1.10.0; Vosk=0.3.38; AForge.Video.DirectShow=2.2.5\n',encoding='utf-8')
'@
$py|python -
if($LASTEXITCODE-ne0){exit $LASTEXITCODE}

Note 'Downloading the pinned public Vosk model and recording provenance/hash.'
$modelUrl='https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip'
$modelZip='ci-tmp/vosk-model-small-en-us-0.15.zip'
Invoke-WebRequest -Uri $modelUrl -OutFile $modelZip -UseBasicParsing
$modelHash=(Get-FileHash $modelZip -Algorithm SHA256).Hash.ToLowerInvariant()
New-Item -ItemType Directory -Force NEWAGE_WORSHIP/models|Out-Null
Expand-Archive $modelZip NEWAGE_WORSHIP/models -Force
"source=$modelUrl`nasset=vosk-model-small-en-us-0.15.zip`nsha256=$modelHash`nruntime_network_dependency=none"|Out-File NEWAGE_WORSHIP/TEST_EVIDENCE/model-integrity.txt
Need NEWAGE_WORSHIP/models/vosk-model-small-en-us-0.15

Note 'Restoring and compiling the WPF solution and isolated intelligence worker.'
msbuild NEWAGE_WORSHIP/NewAgeWorship.sln /t:Restore /p:RestorePackagesConfig=true /v:minimal 2>&1|Tee-Object NEWAGE_WORSHIP/TEST_EVIDENCE/restore.log
if($LASTEXITCODE-ne0){exit $LASTEXITCODE}
msbuild NEWAGE_WORSHIP/NewAgeWorship.sln /m /p:Configuration=Release /p:Platform="Any CPU" /bl:NEWAGE_WORSHIP/TEST_EVIDENCE/build.binlog /v:minimal 2>&1|Tee-Object NEWAGE_WORSHIP/TEST_EVIDENCE/build.log
if($LASTEXITCODE-ne0){exit $LASTEXITCODE}
msbuild NEWAGE_WORSHIP/src/NewAgeWorship.IntelligenceWorker/NewAgeWorship.IntelligenceWorker.csproj /restore /p:Configuration=Release /v:minimal 2>&1|Tee-Object NEWAGE_WORSHIP/TEST_EVIDENCE/intelligence-worker-build.log
if($LASTEXITCODE-ne0){exit $LASTEXITCODE}

Note 'Running deterministic test harness.'
& NEWAGE_WORSHIP/tests/NewAgeWorship.FoundationTests/bin/Release/net48/NewAgeWorship.FoundationTests.exe 2>&1|Tee-Object NEWAGE_WORSHIP/TEST_EVIDENCE/foundation-tests.log
if($LASTEXITCODE-ne0){exit $LASTEXITCODE}

Note 'Running static security and no-spend scans.'
$files=Get-ChildItem NEWAGE_WORSHIP -Recurse -File -Include *.cs,*.xaml,*.json,*.yml,*.yaml,*.iss,*.js
$secret=foreach($p in @('sk-[A-Za-z0-9]{20,}','AIza[0-9A-Za-z_-]{30,}','BEGIN (RSA|OPENSSH|EC) PRIVATE KEY','ghp_[A-Za-z0-9]{30,}')){$files|Select-String -Pattern $p}
if($secret){$secret|Out-File NEWAGE_WORSHIP/TEST_EVIDENCE/secret-scan.log;throw'Potential secret marker found'}
'PASS'|Out-File NEWAGE_WORSHIP/TEST_EVIDENCE/secret-scan.log
$spend=$files|Select-String -Pattern 'billingEnabled\s*[:=]\s*true|autoCharge\s*[:=]\s*true|allowPaidFallback\s*[:=]\s*true' -CaseSensitive:$false
if($spend){$spend|Out-File NEWAGE_WORSHIP/TEST_EVIDENCE/cost-scan.log;throw'Automatic spend marker found'}
'PASS'|Out-File NEWAGE_WORSHIP/TEST_EVIDENCE/cost-scan.log

Note 'Patching installer with local model and optional isolated worker.'
$iss='NEWAGE_WORSHIP/installer/NewAgeWorship.iss';$text=Get-Content $iss -Raw
$lines=@(
'Source: "..\models\vosk-model-small-en-us-0.15\*"; DestDir: "{app}\models\vosk-model-small-en-us-0.15"; Flags: ignoreversion recursesubdirs createallsubdirs',
'Source: "..\src\NewAgeWorship.IntelligenceWorker\bin\Release\net48\*"; DestDir: "{app}\IntelligenceWorker"; Flags: ignoreversion recursesubdirs createallsubdirs'
)
if($text-notmatch'vosk-model-small-en-us-0.15'){
  $insert=($lines-join"`r`n")+"`r`n"
  if($text-match'\[Icons\]'){$text=$text-replace'\[Icons\]',($insert+'[Icons]')}else{$text+="`r`n[Files]`r`n$insert"}
  Set-Content $iss $text -Encoding UTF8
}
$iscc=Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
if(-not(Test-Path $iscc)){choco install innosetup --version=6.4.3 -y --no-progress;if($LASTEXITCODE-ne0){exit $LASTEXITCODE}}
"Inno Setup=$((Get-Item $iscc).VersionInfo.FileVersion)"|Out-File NEWAGE_WORSHIP/TEST_EVIDENCE/build-dependencies.txt
& $iscc $iss 2>&1|Tee-Object NEWAGE_WORSHIP/TEST_EVIDENCE/installer-build.log
if($LASTEXITCODE-ne0){exit $LASTEXITCODE}
Need NEWAGE_WORSHIP/artifacts/NewAgeWorship-Setup.exe
Get-FileHash NEWAGE_WORSHIP/artifacts/NewAgeWorship-Setup.exe -Algorithm SHA256|Format-List|Out-File NEWAGE_WORSHIP/TEST_EVIDENCE/installer-sha256.txt

Note 'Performing real Windows silent install / ProgramHost launch / uninstall smoke.'
$exe=(Resolve-Path NEWAGE_WORSHIP/artifacts/NewAgeWorship-Setup.exe).Path;$d=Join-Path $env:RUNNER_TEMP NWInstalled
if(Test-Path $d){Remove-Item $d -Recurse -Force}
$p=Start-Process $exe -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',('/DIR="'+$d+'"')) -Wait -PassThru
if($p.ExitCode-ne0){throw "Install failed $($p.ExitCode)"}
$app=Join-Path $d NewAgeWorship.exe;$host=Join-Path $d ProgramHost/NewAgeWorship.ProgramHost.exe;$worker=Join-Path $d IntelligenceWorker/NewAgeWorship.IntelligenceWorker.exe;$model=Join-Path $d models/vosk-model-small-en-us-0.15
Need $app;Need $host;Need $worker;Need $model
$hp=Start-Process $host -PassThru;Start-Sleep 3;$hp.Refresh();if($hp.HasExited){throw'ProgramHost exited during smoke'};Stop-Process $hp.Id -Force
$un=Join-Path $d unins000.exe;$up=Start-Process $un -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru
if($up.ExitCode-ne0){throw "Uninstall failed $($up.ExitCode)"};Start-Sleep 1;if(Test-Path $app){throw'Uninstall residue'}
'PASS: install; ProgramHost alive 3s; intelligence worker and local Vosk model installed; uninstall.'|Out-File NEWAGE_WORSHIP/TEST_EVIDENCE/installer-smoke.log

Note 'Packing evidence and publishing fixed engineering-RC assets.'
Compress-Archive -Path NEWAGE_WORSHIP/TEST_EVIDENCE/*,NEWAGE_WORSHIP/VERIFICATION_REPORT.html,NEWAGE_WORSHIP/HARDWARE_ACCEPTANCE_CHECKLIST.html -DestinationPath NEWAGE_WORSHIP/artifacts/NewAgeWorship-Evidence.zip -Force
if($env:GH_TOKEN){
  $tag='newage-worship-v1.0.0-rc'
  gh release view $tag --repo $Repository *> $null
  if($LASTEXITCODE-ne0){gh release create $tag --repo $Repository --target $Commit --title 'NEWAGE WORSHIP v1.0.0 Engineering RC' --notes 'Automated Windows hosted-runner compilation/tests/install smoke passed. Windows 7 SP1 and physical church hardware acceptance remain explicit external gates.'}
  gh release upload $tag NEWAGE_WORSHIP/artifacts/NewAgeWorship-Setup.exe NEWAGE_WORSHIP/artifacts/NewAgeWorship-Evidence.zip --repo $Repository --clobber
}
Note 'V4 release build finished successfully.'
