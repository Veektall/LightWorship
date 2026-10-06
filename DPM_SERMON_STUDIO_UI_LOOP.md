# DPM Sermon Studio Android UI Closed Loop

This branch contains the bootstrap infrastructure for designing and testing the Android UI before the production audio engine is connected.

## Source package

The UI-shell source is stored in ordered binary chunks named `dpm-source.part-00` through `dpm-source.part-06`. The GitHub Actions workflow reconstructs the ZIP and extracts it into `dpm-sermon-studio-ui-loop/` before building.

The source includes:

- a real Jetpack Compose UI shell;
- deterministic fake project and processing states;
- light and dark themes;
- official Compose preview screenshot tests;
- instrumented Compose navigation tests;
- scripts that capture 24 emulator screenshots, a walkthrough video, Logcat and graphics diagnostics;
- readiness and acceptance-gate documentation.

## Workflow

`.github/workflows/dpm-sermon-studio-ui-loop.yml` runs two independent jobs:

1. Host screenshot generation and validation.
2. Android emulator flow testing and artifact capture.

The first pull-request run is a bootstrap run. It must prove compilation, dependency resolution, screenshot generation, emulator startup, navigation tests and artifact upload before the loop is declared operational.
