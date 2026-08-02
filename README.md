# DisplayLift V8 — Rust Auto Region Visuals

DisplayLift is a Windows 10/11 display-color utility designed around one built-in Rust configuration. It automatically watches for `RustClient.exe`, samples broad screen colors while Rust is foreground, and selects a tuned visual mode for temperate terrain, desert, snow, coastline/water, or dark night/interior scenes.

## What changed in V8

- One Rust target; no profile list or profile-management tab.
- Automatic screen-color scene detection with rolling averages and switch hysteresis.
- Six manual fallback buttons: Balanced, Temperate, Desert, Snow, Coast and Night.
- Four clean global trims: Color, Brightness, Contrast and Shadows.
- Rust installation auto-discovery with browse and Steam-launch fallbacks.
- NVIDIA Digital Vibrance through NVAPI when supported, plus Windows color-matrix and gamma controls.
- Desktop restoration whenever Rust loses focus, optional startup, tray controls and global hotkeys.

## Automatic detection limits

The detector does not read Rust memory, the map, coordinates, game files or network traffic. It samples six small screen patches in memory and discards them immediately. Weather, sunsets, skins, monuments and custom maps can make visual classification ambiguous. DisplayLift therefore waits for repeated readings before switching and always keeps the manual scene buttons visible.

For the most reliable screen sampling, use Rust in borderless-windowed mode. Exclusive fullscreen or protected capture paths may return a blank image; the app then stays on Balanced and asks you to choose a scene manually.

## Hotkeys

- `Ctrl+Alt+F8` — return to Auto Region
- `Ctrl+Alt+F9` — cycle manual scenes
- `Ctrl+Alt+F10` — restore original desktop colors

## Safety boundary

DisplayLift is external display software. It does not inject DLLs, hook DirectX, modify Rust files, inspect process memory, draw an overlay, automate input or bypass Easy Anti-Cheat. It cannot guarantee how future Facepunch or EAC policy changes will treat any third-party utility.

## Build

Double-click `ONE-CLICK-BUILD.cmd`, or run:

```powershell
./scripts/build.ps1
```

The output is `dist/DisplayLift.exe`.
