# Roadmap

LightWorship aims to be a practical, low-cost worship projection tool for churches and community teams using Windows machines, including older hardware.

## Near Term

- Keep the projection, KJV scripture search, schedule, lyrics, and media workflows stable.
- Add regression tests for transcript parsing, scripture reference detection, and slide-deck behavior.
- Improve documentation for first-time setup and second-screen projection.
- Harden local settings so provider API keys remain local and never enter source control.

## Maintenance Workflows

- Use CI to build the .NET Framework app and run parser/service tests on every pull request.
- Triage bugs by Windows version, projection setup, and live-listening provider.
- Track provider-specific behavior for Deepgram, Gemini Live, Windows Speech, Voicebox, and local Whisper.

## Future Work

- Add safe import paths for licensed Bible translations where users provide their own licensed data.
- Improve song and lyric management without bundling copyrighted lyric catalogs.
- Add release packaging automation for signed or checksummed installer builds.
- Expand accessibility and keyboard-first workflows for live operators.
