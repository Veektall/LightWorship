# NEWAGE WORSHIP — pinned offline audio dependencies

The Windows Legacy build uses these zero-charge local components only. They are build/runtime dependencies, not network services.

| Component | Pinned version / asset | Source | Licence / use | Purpose |
|---|---|---|---|---|
| NAudio | 1.10.0 NuGet | https://www.nuget.org/packages/NAudio/1.10.0 and https://github.com/naudio/NAudio | MIT | Windows audio capture wrapper |
| Vosk | 0.3.38 NuGet | https://www.nuget.org/packages/Vosk/0.3.38 and https://github.com/alphacep/vosk-api | Apache-2.0 project | Offline streaming speech recognition |
| Vosk small English model | vosk-model-small-en-us-0.15 | https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip | Model provenance is recorded during build; the release evidence records its exact SHA-256 | Baseline local English model; church-specific accuracy remains a calibration/physical-audio gate |
| AForge.Video.DirectShow | 2.2.5 NuGet | https://www.nuget.org/packages/AForge.Video.DirectShow/2.2.5 | LGPL-family AForge.NET distribution; licence file must be retained in release package | Windows 7 DirectShow webcam/capture-card input |

NuGet restores validate package integrity through NuGet's package hash mechanism. The Vosk model is data rather than executable code; the build records the downloaded archive's SHA-256 and source URL in `TEST_EVIDENCE/model-integrity.txt` before it is unpacked.

No dependency above contains a billing path, API key, or automatic network fallback at runtime.
