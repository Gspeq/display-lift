# DisplayLift Vibrance

DisplayLift Vibrance is a small 64-bit Windows utility for making the entire desktop dramatically more colorful. It applies a real full-screen color matrix for saturation, contrast, and brightness, while an independent gamma ramp provides optional shadow lift.

It uses standard Windows display APIs only:

- no game injection or DLL loading into Rust
- no reading or writing another process
- no in-game overlay
- no memory access
- no automatic game detection
- no anti-cheat bypass behavior

## Controls

Four manual settings are available:

- **Saturation:** 100–350%
- **Contrast:** 80–130%
- **Brightness:** −10% to +20%
- **Shadow lift:** 0–60%

Included presets:

1. **Normal** — restore the original display.
2. **Color Pop** — 180% saturation.
3. **Extreme** — 260% saturation.
4. **Nuclear** — 340% saturation with extra contrast.
5. **Neon Shadows** — 290% saturation with a large dark-area lift.

Global hotkeys:

- `Ctrl + Alt + F9`: cycle presets
- `Ctrl + Alt + F10`: restore the original display

The color effect changes the whole desktop. DisplayLift captures the existing Windows full-screen color matrix and gamma ramp when it starts, then restores both when you press Restore or exit normally.

## One-click build

Double-click:

```text
ONE-CLICK-BUILD.cmd
```

The script installs the .NET 8 SDK through `winget` when necessary, runs the color-matrix tests, and creates:

```text
dist\DisplayLift.exe
DisplayLift-win-x64.zip
DisplayLift-win-x64.zip.sha256
```

Launch the finished app at:

```text
Desktop\DisplayLift\dist\DisplayLift.exe
```

## One-click build and GitHub publish

Double-click:

```text
ONE-CLICK-PUBLISH.cmd
```

It builds and tests the utility, initializes or updates the local Git repository, creates or reconnects the `display-lift` GitHub repository, pushes `main`, and verifies that local `HEAD` exactly matches `origin/main`.

The repository checks also reject missing managed files, unexpected managed files, common duplicate-copy filenames, byte-for-byte duplicate managed files, case-colliding Git paths, committed build output, dirty local changes, and local/remote commit mismatches.

## Display behavior

Windows applies the color matrix to the desktop through its full-screen Magnification color-effect API. Some exclusive-fullscreen games or GPU-driver color profiles can override desktop transforms. Borderless-windowed mode is the most reliable mode for this type of system-level adjustment.

## Development

Build manually:

```powershell
./scripts/build.ps1
```

Run from source:

```powershell
dotnet run --project ./src/DisplayLift/DisplayLift.csproj
```

Run repository verification:

```powershell
./tests/Test-RepositoryState.ps1 -RequireClean -RequireRemoteSync
```
