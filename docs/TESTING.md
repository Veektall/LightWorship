# Testing LightWorship

## Run From Build Output

Build the app, then run:

```powershell
C:\Users\simpa\Documents\New folder\LightWorship\src\LightWorship\bin\Debug\LightWorship.exe
```

## Quick Smoke Test

1. Open the Bible tab.
2. Search `John 3:16`.
3. Select the result and click `Preview`.
4. Click `Test Window`.
5. Click `Go Live`.
6. Confirm the projection test window shows the scripture.
7. Press `B` for black screen.
8. Press `C` to clear text.
9. Open Songs, preview a section, and send it live.
10. Import an image or video from Media and send it live.
11. Open Bible, choose `Genesis` and chapter `1`, then click `Load Chapter`.
12. Search `Jn 3 16` and confirm it finds John 3:16.
13. Add an item to Schedule, select it, type notes, and click `Save Notes`.
14. Open AI Assist, type `let there be light`, click `Analyze`, preview the Genesis 1:3 suggestion, and send it live.
15. In Songs, fill copyright/CCLI/key/tempo fields, save, and preview a section.
16. Click into any text box and type `b c l f space`; confirm Black/Clear/Logo/Freeze shortcuts do not fire while typing.
17. Search `Genesis 1:1-5`, preview it, send live, then use Right/Left to move through split slides.
18. In Media, choose `Fill`, `Fit`, `Stretch`, or `Center` before previewing imported media.
19. In AI Assist, try Start Listening if Windows speech recognition is configured on the machine. Watch the transcript history and suggestion queue.
20. In Settings, use `Import Bible JSON` for legally supplied Bible JSON files shaped like `{ "Genesis 1:1": "..." }`.
21. In AI Assist, type or speak `John three sixteen` and confirm John 3:16 appears as a suggestion.
22. Enable `Auto-preview top suggestion`, set confidence to `0.35`, and confirm high-confidence live listening updates Preview without going Live.

## Automated Tests

Run:

```powershell
C:\Users\simpa\Documents\New folder\LightWorship\tests\LightWorship.Tests\bin\Debug\LightWorship.Tests.exe
```

Current automated checks cover:

- KJV data loading.
- `John 3:16`, `Jn 3 16`, and spoken-style scripture references.
- Genesis 1 chapter browsing.
- Phrase search for `let there be light`.
- AI Assist scripture phrase matching.
- AI Assist direct reference matching.
- AI Assist local song lyric matching.
- Spoken number parsing for references like `John three sixteen`.
- Live transcription suggestion pipeline parsing.

## Notes

- Video playback depends on codecs available on the Windows machine.
- The bundled Bible data is KJV only. Other translations need licensed import/provider support.
