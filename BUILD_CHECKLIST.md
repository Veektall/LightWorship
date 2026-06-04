# LightWorship Build Checklist

This checklist is the working tracker for Phase 1. As we build, we will update these boxes from `[ ]` to `[x]` only after the feature is implemented and checked.

## Phase 0 - Tooling And Project Setup

- [x] Confirm Windows desktop stack for Windows 7 compatibility.
- [x] Create isolated project folder.
- [x] Install or download required development tools.
- [x] Create solution/project structure.
- [x] Add source control setup when Git is available.
- [x] Add initial README with run/build instructions.
- [x] Add local data folder layout.
- [x] Add application settings storage.

## Phase 1A - App Shell

- [x] Create operator desktop application.
- [x] Add main navigation areas: Bible, Songs, Media, Schedule, Settings.
- [x] Add preview panel.
- [x] Add live status panel.
- [x] Add command buttons: Go Live, Black, Clear, Logo, Freeze.
- [x] Add basic dark operator theme suitable for projection work.
- [x] Add keyboard shortcut handling.
- [x] Prevent keyboard shortcuts from hijacking typing fields.

## Phase 1B - Projection Output

- [x] Create separate projection window.
- [x] Support fullscreen output on selected monitor.
- [x] Support windowed test output.
- [x] Render text slides.
- [x] Render Bible verse slides.
- [x] Render song lyric slides.
- [x] Render image backgrounds.
- [x] Render clear text over retained background.
- [x] Render black screen.
- [x] Render logo screen.
- [x] Add next/previous slide navigation.

## Phase 1C - Bible Module

- [x] Add Bible local data schema.
- [x] Bundle or import public-domain KJV data.
- [x] Add book/chapter/verse browsing.
- [x] Add reference parser for examples like `John 3:16` and `Genesis 1:1-3`.
- [x] Add phrase/keyword scripture search.
- [x] Add Bible version selector baseline with KJV.
- [x] Add passage-to-slide splitting.
- [x] Add scripture preview.
- [x] Add scripture Go Live behavior.
- [x] Add Bible display styling settings baseline.

## Phase 1D - Songs Module

- [x] Add songs local data schema.
- [x] Add song list/search.
- [x] Add song editor.
- [x] Support song sections: Verse, Chorus, Bridge, Ending.
- [x] Support custom display sequence.
- [x] Add lyric slide splitting.
- [x] Add song preview.
- [x] Add song Go Live behavior.
- [x] Add optional copyright/CCLI fields.

## Phase 1E - Media Module

- [x] Add media library local data schema.
- [x] Import image files.
- [x] Import video files.
- [x] Copy imported media into app library by default.
- [x] Preview images as projection slides.
- [x] Preview videos as projection slides where Windows codecs support playback.
- [x] Set image as slide background.
- [x] Set video as slide/background media where supported.
- [x] Add fit modes: Fill, Fit, Stretch, Center.

## Phase 1F - Text And Template Slides

- [x] Add plain text slide creation.
- [x] Add text overlay over image backgrounds.
- [x] Add reusable slide templates.
- [x] Add font family, size, color, alignment, shadow/outline settings baseline.
- [x] Save custom text/media slides through schedule.
- [x] Project custom text/media slides.

## Phase 1G - Service Schedule

- [x] Add schedule local data schema.
- [x] Add items from Bible, Songs, Media, and Text Slides.
- [x] Reorder schedule items.
- [x] Preview selected schedule item.
- [x] Send selected schedule item live.
- [x] Save schedule.
- [x] Load schedule.
- [x] Autosave current service.

## Phase 1H - Settings

- [x] Select default output monitor.
- [x] Select default Bible version baseline.
- [x] Select default font and text style baseline.
- [x] Configure logo screen image.
- [x] Configure media library location.
- [x] Configure app startup behavior.
- [x] Persist settings across restarts.

## Phase 1I - Packaging And Verification

- [x] Smoke test on development machine.
- [x] Verify keyboard shortcuts compile and route.
- [x] Add automated test harness.
- [x] Verify scripture search, browsing, and AI Assist with automated tests.
- [x] Verify projection window launches on development machine.
- [x] Verify projection window behavior with two monitors where available.
- [x] Verify app starts without internet.
- [x] Verify local data files are created on first run.
- [x] Create installer script.
- [x] Add crash logging.
- [x] Document Windows 7 runtime requirements.

## Phase 1J - Incoming UI Proposal Integration

- [x] Review attached HTML prototype bundle.
- [x] Review attached technical project bundle.
- [x] Review attached UI guide image.
- [x] Add top command bar.
- [x] Add left dashboard navigation.
- [x] Add Bible book/chapter browsing inspired by the UI guide.
- [x] Add scripture results with book/chapter context.
- [x] Add schedule notes panel.
- [x] Add local AI Assist simulator panel.
- [x] Expand local AI Assist ranked scripture/song suggestions.
- [x] Add stronger reference parsing for `Jn 3 16` style inputs.
- [x] Add song copyright/CCLI editing fields.
- [x] Add richer song metadata fields: key, tempo, time signature, duration.
- [x] Add preview/live 16:9 frames in the operator window.

## Deferred To Phase 2+

- [x] Live microphone/audio listening foundation using Windows speech APIs.
- [x] Speech-to-text adapter foundation using Windows dictation.
- [x] Free local Whisper.cpp transcription option.
- [x] Download local Whisper.cpp CPU test binary and tiny English model for development testing.
- [x] Add configurable Whisper executable/model paths.
- [x] Add microphone chunk recorder for Local Whisper mode.
- [x] Smoke-test Whisper transcription against a generated WAV phrase.
- [x] Add Gemini Live streaming transcription as the default lightweight engine.
- [x] Verify Gemini Live WebSocket setup and audio-send path with the configured API key.
- [x] Build lightweight USB-friendly portable package without Whisper model files.
- [x] Add Deepgram Nova live transcription engine for higher-accuracy dedicated STT.
- [x] Add selectable Windows audio input device for cloud/offline microphone engines.
- [x] Add Bible vocabulary keyword boosting for Deepgram streaming.
- [x] Add external scripture-reference injection for EasyWorship/sermon software.
- [x] Add configurable target window title, hotkey, focus delay, and optional Enter key.
- [x] Add test-send button for external scripture input integration.
- [x] Spoken Bible reference detection through AI Assist pipeline.
- [x] Scripture quote matching through local AI Assist ranking.
- [x] Live transcript history panel.
- [x] Live suggestion queue.
- [x] Auto-preview top suggestion with confidence threshold.
- [x] Spoken number parsing for references like `John three sixteen`.
- [x] Semantic-like scripture quote matching through local ranking.
- [ ] Lyric identification.
- [ ] Online lyric/provider integration.
- [x] Licensed/custom Bible import scaffold for user-supplied JSON.
- [ ] Licensed Bible provider integration.
- [ ] Remote control/stage display.
