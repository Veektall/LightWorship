# LightWorship Tooling Notes

## Target Runtime

LightWorship is planned as a lightweight Windows desktop application that can run on Windows 7 SP1.

Recommended runtime target:

- .NET Framework 4.8 desktop application
- WPF or WinForms UI
- Local JSON storage in the v0.1 test build; SQLite can replace this later if the data layer grows.
- Windows-compatible media playback layer

## Development Tools Needed

- Git: installed through winget. If it is not on PATH yet, use `C:\Program Files\Git\cmd\git.exe`.
- Visual Studio Build Tools with MSBuild: installed through winget.
- MSBuild path: `C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe`.
- .NET Framework 4.8 targeting/developer pack: available in reference assemblies.
- NuGet: downloaded to `tools\nuget.exe`.
- Inno Setup: installed through winget.
- Inno Setup compiler path: `C:\Users\simpa\AppData\Local\Programs\Inno Setup 6\ISCC.exe`.

## Notes

- Modern .NET, recent Electron, and WebView2 are not good Windows 7 targets.
- KJV can be bundled if we use a valid public-domain source.
- NIV, NLT, GNT, and The Message should be supported through import or licensed provider integration unless you already have distribution rights.
- AI listening features are intentionally deferred until the projection core is stable.
