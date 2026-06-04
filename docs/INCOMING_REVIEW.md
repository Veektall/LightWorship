# Incoming Bundle Review

Reviewed files:

- `content-2.zip`: simple HTML prototype with schedule, scripture, songs, media, and settings pages.
- `EasyWorship_Lite_Project_Bundle.zip`: technical spec, WPF dual-screen bootstrapper, and a richer HTML/Tailwind prototype.
- `file_00000000ae3071f484c1b9cae1bea6f5.png`: polished UI guide showing a dense operator dashboard.

## Useful Items To Adopt

- Keep the app native WPF/.NET Framework for Windows 7 compatibility.
- Avoid WebView2 as a core dependency because modern WebView2 is not a reliable Windows 7 target.
- Use the UI guide's operator model: top command bar, left navigation, large scripture/song/media workspace, preview/live column, schedule, AI Assist, notes, and bottom shortcut strip.
- Add explicit Bible version dropdown but only ship KJV until licensed versions are provided.
- Add book/chapter browsing, not only search.
- Add schedule notes.
- Add local AI Assist simulation now, then real microphone listening later.
- Add song metadata fields for copyright/CCLI and later key/time/tempo.
- Add stronger reference parsing for inputs like `Jn 3 16`.

## Deferred

- WebView2/CefSharp HTML host.
- Real AI microphone listening.
- Online lyric scraping.
- Bundling copyrighted Bible versions without licenses.
