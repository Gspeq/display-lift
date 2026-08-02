# DisplayLift

Installer/publisher revision: **V4**.

DisplayLift is a small Windows utility that applies one of four desktop gamma presets and restores the exact gamma ramp that was active when the app started.

It is deliberately limited to normal Windows display APIs:

- no game injection or DLL loading
- no reading or writing another process
- no overlay
- no memory access
- no automatic game detection
- no anti-cheat bypass behavior

## Presets

1. **Normal** — restores the original display ramp.
2. **Clear** — mild midtone lift.
3. **Shadow Lift** — stronger dark-area visibility.
4. **Strong** — aggressive shadow lift with visibly washed-out blacks.

Global hotkeys:

- `Ctrl + Alt + F9`: cycle presets
- `Ctrl + Alt + F10`: restore the original display

The setting affects the whole desktop. DisplayLift restores the original ramp when you press Restore or exit normally. A GPU driver reset, Windows sign-out, display mode change, or exclusive-fullscreen application can override the ramp.

## One-click build

On Windows 10 or 11, double-click:

```text
ONE-CLICK-BUILD.cmd
```

The script installs the .NET 8 SDK through `winget` when necessary, then creates:

```text
dist\DisplayLift.exe
DisplayLift-win-x64.zip
DisplayLift-win-x64.zip.sha256
```

## One-click build and GitHub publish

Double-click:

```text
ONE-CLICK-PUBLISH.cmd
```

It will:

1. install Git and GitHub CLI through `winget` if needed;
2. build the executable;
3. ask you to authenticate with GitHub if needed;
4. initialize a local `main` repository;
5. commit the source;
6. create a public `display-lift` GitHub repository;
7. push `main`;
8. fetch the remote and verify that local `HEAD` exactly matches `origin/main`;
9. verify that the generated source tree contains no missing, unexpected, case-colliding, or byte-for-byte duplicate managed files;
10. open the repository in your browser.

To make the remote private, edit `ONE-CLICK-PUBLISH.cmd` and change:

```text
-Visibility "public"
```

to:

```text
-Visibility "private"
```

## Important use note

This utility changes only the operating system's display gamma. Rules can differ between games, competitive leagues, and community servers. Use only display adjustments permitted where you play. The project intentionally does not contain techniques intended to hide itself, evade detection, inspect a game, or create an in-game overlay.

## Development

Requirements:

- Windows 10 or 11
- .NET 8 SDK

Build manually:

```powershell
./scripts/build.ps1
```

Run from source:

```powershell
dotnet run --project ./src/DisplayLift/DisplayLift.csproj
```

## Safe reruns and verification

The single-file installer is idempotent. On every run it replaces only DisplayLift-managed source/configuration paths and preserves the existing `.git` directory. The publish workflow refuses to force-push diverged history.

Run the repository checks manually with:

```powershell
./tests/Test-RepositoryState.ps1 -RequireClean -RequireRemoteSync
```

The test verifies the expected file manifest, scans for common duplicate-copy filenames, rejects byte-for-byte duplicate managed files, checks for case-colliding Git paths, confirms generated build output is not tracked, requires a clean worktree, and confirms local and remote branch commit IDs are identical.
