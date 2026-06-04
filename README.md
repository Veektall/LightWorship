# LightWorship

[![CI](https://github.com/Veektall/LightWorship/actions/workflows/ci.yml/badge.svg)](https://github.com/Veektall/LightWorship/actions/workflows/ci.yml)

LightWorship is a lightweight worship projection application for Windows 7 SP1 and newer.

The current public release focuses on the stable projection core:

- Bible scripture search and projection
- Song lyric library and projection
- Image/video media library
- Text overlays
- Service schedule
- Keyboard shortcuts
- Second-screen projection output

AI listening supports Deepgram Nova streaming, Gemini Live, Windows Speech, and local Whisper. For real worship-service transcription accuracy, Deepgram is the recommended engine.

See `BUILD_CHECKLIST.md` for the live build tracker and `ROADMAP.md` for the maintainer roadmap.

## Current Development Stack

- .NET Framework 4.8 target
- WPF desktop application
- Local JSON storage for the v0.1 test build
- MSBuild from Visual Studio Build Tools
- Inno Setup for packaging

## Support

LightWorship is maintained as a zero-cost tool for churches and small community teams. If it helps you, support maintenance through GitHub Sponsors when available or USDC on Base:

`0x8f1c1108e339be9adf77baf1eb44232d956cb7b7`

## Test Build

Run the debug build:

```powershell
C:\Users\simpa\Documents\New folder\LightWorship\src\LightWorship\bin\Debug\LightWorship.exe
```

Run the release build:

```powershell
C:\Users\simpa\Documents\New folder\LightWorship\src\LightWorship\bin\Release\LightWorship.exe
```

Installer:

```powershell
C:\Users\simpa\Documents\New folder\LightWorship\installer\output\LightWorship-Setup-0.13.0.exe
```

Run automated tests:

```powershell
C:\Users\simpa\Documents\New folder\LightWorship\tests\LightWorship.Tests\bin\Debug\LightWorship.Tests.exe
```

## Current Limitations

- KJV is bundled. NIV, NLT, GNT, and The Message require licensed import/provider support.
- Deepgram Nova transcription requires internet access and a Deepgram API key. It is the recommended live transcription engine because it is built specifically for real-time speech-to-text.
- Gemini Live transcription remains available, but it may miss words because it is a live multimodal model rather than a dedicated transcription service.
- Local Whisper transcription is still available as an advanced fallback, but it is not included in the default portable bundle because model files make the app heavy.
- Local AI Assist can detect direct Bible references and many quote-like phrases against the loaded Bible. It is not yet a full cloud semantic search engine.
- Lyric identification and online lyric/provider integration are not yet implemented.
- Video playback depends on codecs installed on the Windows machine.
- This is a v0.13 test build. It includes live transcription UI, Voicebox local transcription integration, Deepgram Nova streaming, Gemini Live transcription, local AI Assist, optional Windows speech listening, external scripture-reference injection, and integration scaffolds, but not licensed provider integrations.

## Recommended Live Transcription Setup

In Settings, set:

- Live transcription engine: `Deepgram`
- Audio input: choose the actual microphone, mixer, or USB audio interface
- Deepgram model: `nova-3`
- Deepgram API key: enter your own key in local settings

Then open AI Assist and press Start Listening. The app streams microphone audio continuously to Deepgram and feeds final transcript results into the Bible/song suggestion queue.

## External Scripture Input Bridge

LightWorship can send a detected Bible reference into another worship app such as EasyWorship:

- Open the target worship app and click its scripture search/input field once.
- In LightWorship Settings, set `External scripture target window title` to part of the app window title, such as `EasyWorship`.
- If the target app needs a shortcut to focus its scripture field, set `External scripture input hotkey`.
- Use `Test External Scripture Send` to paste `Genesis 1:3` into the target app.
- In AI Assist, enable `Send detected Bible reference to EasyWorship/sermon app`.

The bridge copies the detected reference to the clipboard, focuses the target app, optionally sends the configured hotkey, pastes the reference, and can optionally press Enter.

## Voicebox Local STT Option

Voicebox can be used as a local transcription backend when it is installed and running:

- Install/run Voicebox from `voicebox.sh` or the GitHub project.
- In LightWorship Settings, set `Live transcription engine` to `VoiceboxLocal`.
- Leave `Voicebox server URL` as `http://127.0.0.1:17493` unless Voicebox uses a different local port.
- Use model `turbo` and language `en` as a starting point.

LightWorship records short chunks from the selected audio input, sends them to Voicebox `/transcribe`, then uses the transcript for scripture detection and external app injection.
