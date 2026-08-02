# DisplayLift V9 — Rust Auto Region, Restore-Safe

DisplayLift is a Windows display utility that applies external color, contrast, gamma, shadow and NVIDIA Digital Vibrance adjustments while Rust is active. It does not inject into Rust, modify game files, read game memory or create an in-game overlay.

## V9 restoration fix

V9 corrects the stale-color problem from earlier builds:

- Clicking **X now exits the application** instead of silently hiding it in the tray.
- Every normal close runs three restoration paths: captured baseline restoration, a hard Windows identity/gamma reset, and NVIDIA Digital Vibrance reset to the driver default.
- Startup closes older DisplayLift processes and normalizes the display before capturing a new baseline.
- A stable cross-version mutex prevents old and new versions from running together.
- `--restore-only` performs an emergency reset without opening the interface.
- Manual region buttons produce a visible 10-second desktop preview when Rust is not foreground, then automatically restore normal colors.

## Usage

Run `dist\DisplayLift.exe`. Keep **Auto Region** enabled for automatic Temperate, Desert, Snow, Coast and Night/Interior switching while `RustClient.exe` is foreground.

Manual buttons can be used inside Rust. Outside Rust, they intentionally create only a 10-second preview so you can verify that a button works without leaving the desktop altered.

Hotkeys:

- `Ctrl+Alt+F8` — return to automatic mode
- `Ctrl+Alt+F9` — cycle manual regions
- `Ctrl+Alt+F10` — restore normal colors and pause

The minimize button can still hide DisplayLift to the tray when that option is enabled. The window **X always exits and restores normal display settings**.

## Emergency recovery

Run:

```powershell
.\dist\DisplayLift.exe --restore-only
```

This closes other DisplayLift instances and resets Windows color effects, gamma/shadow lift and NVIDIA Digital Vibrance to normal defaults.

## Build

Double-click `ONE-CLICK-BUILD.cmd`, or run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
```
