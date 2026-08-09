$ErrorActionPreference='Stop'

$names=@(
  '.newage-bootstrap/source.b64.part00',
  '.newage-bootstrap/source.b64.part01',
  '.newage-bootstrap/source.b64.fix020',
  '.newage-bootstrap/source.b64.fix021',
  '.newage-bootstrap/source.b64.part03',
  '.newage-bootstrap/source.b64.part04',
  '.newage-bootstrap/source.b64.part05'
)
$b64=($names|ForEach-Object{(Get-Content $_ -Raw).Trim()}) -join ''
$zip=Join-Path $env:RUNNER_TEMP 'NW_CODE.zip'
[IO.File]::WriteAllBytes($zip,[Convert]::FromBase64String($b64))
$sourceHash=(Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
if($sourceHash -ne 'e4536533827c313e18da37f1b645ffe0532a95c5b6f1e77f77d98f86c03e8424'){throw "source hash mismatch: $sourceHash"}
Expand-Archive $zip . -Force

$desktop='NEWAGE_WORSHIP/src/NewAgeWorship.Desktop'
$core='NEWAGE_WORSHIP/src/NewAgeWorship.Core/Services'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$overlay=[IO.Compression.ZipFile]::OpenRead((Resolve-Path '.newage-redesign/overlay-v5.zip'))
try {
  $entry=$overlay.Entries|Where-Object{$_.FullName -eq 'MainWindow.xaml'}|Select-Object -First 1
  if(-not $entry){throw 'verified V5 MainWindow.xaml missing'}
  [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path (Resolve-Path $desktop) 'MainWindow.xaml'),$true)
} finally {$overlay.Dispose()}

git apply --directory=$desktop '.newage-functional-v3/MainWindow.xaml.patch'
if($LASTEXITCODE -ne 0){throw 'functional XAML patch failed'}
git apply --directory=$desktop '.newage-functional-v3/AssistScroll.patch'
if($LASTEXITCODE -ne 0){throw 'assist scroll patch failed'}

Copy-Item '.newage-functional-v3/MainWindow.xaml.cs' "$desktop/MainWindow.xaml.cs" -Force
Copy-Item '.newage-functional-v3/BibleLibrary.cs' "$desktop/BibleLibrary.cs" -Force
Copy-Item '.newage-functional-v3/LocalAiAssistant.cs' "$desktop/LocalAiAssistant.cs" -Force
Copy-Item '.newage-functional-v3/VoskSpeechService.cs' "$desktop/VoskSpeechService.cs" -Force
Copy-Item '.newage-functional-v3/WhisperSpeechService.cs' "$desktop/WhisperSpeechService.cs" -Force
Copy-Item '.newage-functional-v3/MixerAudioService.cs' "$desktop/MixerAudioService.cs" -Force
Copy-Item '.newage-functional-v3/NewAgeWorship.Desktop.csproj' "$desktop/NewAgeWorship.Desktop.csproj" -Force
Copy-Item '.newage-functional-v3/ScriptureReferenceParser.cs' "$core/ScriptureReferenceParser.cs" -Force

# Rights-safe deterministic compatibility fixture. The user's exact Bible pack remains outside the public repository.
New-Item -ItemType Directory -Force "$desktop/Data"|Out-Null
$fixture=@'
import json,zipfile,pathlib
books=['Genesis','Exodus','Leviticus','Numbers','Deuteronomy','Joshua','Judges','Ruth','1 Samuel','2 Samuel','1 Kings','2 Kings','1 Chronicles','2 Chronicles','Ezra','Nehemiah','Esther','Job','Psalms','Proverbs','Ecclesiastes','Song of Solomon','Isaiah','Jeremiah','Lamentations','Ezekiel','Daniel','Hosea','Joel','Amos','Obadiah','Jonah','Micah','Nahum','Habakkuk','Zephaniah','Haggai','Zechariah','Malachi','Matthew','Mark','Luke','John','Acts','Romans','1 Corinthians','2 Corinthians','Galatians','Ephesians','Philippians','Colossians','1 Thessalonians','2 Thessalonians','1 Timothy','2 Timothy','Titus','Philemon','Hebrews','James','1 Peter','2 Peter','1 John','2 John','3 John','Jude','Revelation']
verses={'16':'For God so loved the world, that he gave his only begotten Son, that whosoever believeth in him should not perish, but have everlasting life.','17':'For God sent not his Son into the world to condemn the world; but that the world through him might be saved.','18':'He that believeth on him is not condemned: but he that believeth not is condemned already.'}
out=pathlib.Path(r'NEWAGE_WORSHIP/src/NewAgeWorship.Desktop/Data/Bible_Versions.zip')
with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
  for code in ['AMP','GNT','HCSB','KJV','MSG','NIV','NKJV','NLT']:
    data={b:{} for b in books}; data['John']={'3':dict(verses)}
    if code=='MSG': data['John']['3']={'16-18':' '.join(verses[str(i)] for i in (16,17,18))}
    z.writestr(code+'.json',json.dumps({'version':code,'books':data},separators=(',',':')))
'@
Set-Content fixture.py $fixture -Encoding UTF8
python fixture.py

$desktopAbs=(Resolve-Path $desktop).Path
$tmp=Join-Path $env:RUNNER_TEMP 'newage-functional-v4-assets'
New-Item -ItemType Directory -Force $tmp|Out-Null

curl.exe -L --fail --retry 3 'https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip' -o "$tmp/vosk.zip"
Expand-Archive "$tmp/vosk.zip" "$tmp/vosk" -Force
New-Item -ItemType Directory -Force "$desktopAbs/Models"|Out-Null
Copy-Item "$tmp/vosk/vosk-model-small-en-us-0.15" "$desktopAbs/Models/vosk-model-small-en-us-0.15" -Recurse -Force

$headers=@{'Accept'='application/vnd.github+json';'X-GitHub-Api-Version'='2022-11-28'}
if($env:GITHUB_TOKEN){$headers['Authorization']='Bearer '+$env:GITHUB_TOKEN}
$whisperRelease=Invoke-RestMethod 'https://api.github.com/repos/ggml-org/whisper.cpp/releases/latest' -Headers $headers
$whisperAsset=$whisperRelease.assets|Where-Object{$_.name -eq 'whisper-bin-x64.zip'}|Select-Object -First 1
if(-not $whisperAsset){throw 'official whisper.cpp x64 release asset missing'}
curl.exe -L --fail --retry 3 $whisperAsset.browser_download_url -o "$tmp/whisper.zip"
Expand-Archive "$tmp/whisper.zip" "$tmp/whisper" -Force
$whisperExe=Get-ChildItem "$tmp/whisper" -Recurse -Filter whisper-cli.exe|Select-Object -First 1
if(-not $whisperExe){throw 'whisper-cli.exe missing'}
New-Item -ItemType Directory -Force "$desktopAbs/AI/whisper"|Out-Null
Copy-Item "$($whisperExe.Directory.FullName)/*" "$desktopAbs/AI/whisper" -Recurse -Force
New-Item -ItemType Directory -Force "$desktopAbs/Models/whisper"|Out-Null
curl.exe -L --fail --retry 3 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin?download=true' -o "$desktopAbs/Models/whisper/ggml-tiny.en.bin"

$llamaRelease=Invoke-RestMethod 'https://api.github.com/repos/ggml-org/llama.cpp/releases/latest' -Headers $headers
$llamaAsset=$llamaRelease.assets|Where-Object{$_.name -match 'bin-win-cpu-x64\.zip$'}|Select-Object -First 1
if(-not $llamaAsset){$llamaAsset=$llamaRelease.assets|Where-Object{$_.name -match 'win.*x64.*\.zip$' -and $_.name -notmatch 'cuda|vulkan|sycl|hip|arm'}|Select-Object -First 1}
if(-not $llamaAsset){throw 'official llama.cpp Windows x64 CPU release asset missing'}
curl.exe -L --fail --retry 3 $llamaAsset.browser_download_url -o "$tmp/llama.zip"
Expand-Archive "$tmp/llama.zip" "$tmp/llama" -Force
$llamaExe=Get-ChildItem "$tmp/llama" -Recurse -Filter llama-cli.exe|Select-Object -First 1
if(-not $llamaExe){throw 'llama-cli.exe missing'}
New-Item -ItemType Directory -Force "$desktopAbs/AI/llama"|Out-Null
Copy-Item "$($llamaExe.Directory.FullName)/*" "$desktopAbs/AI/llama" -Recurse -Force
New-Item -ItemType Directory -Force "$desktopAbs/AI/models"|Out-Null
curl.exe -L --fail --retry 3 'https://huggingface.co/unsloth/SmolLM2-360M-Instruct-GGUF/resolve/main/SmolLM2-360M-Instruct-Q4_K_M.gguf?download=true' -o "$desktopAbs/AI/models/SmolLM2-360M-Instruct-Q4_K_M.gguf"
$modelHash=(Get-FileHash "$desktopAbs/AI/models/SmolLM2-360M-Instruct-Q4_K_M.gguf" -Algorithm SHA256).Hash.ToLowerInvariant()
if($modelHash -ne '16c7f1667fea34bacad196a57b548effcb37614db4ab5677a20c8c7b823b9e63'){throw "AI model hash mismatch: $modelHash"}
