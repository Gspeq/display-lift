# DisplayLift Visual Panel

DisplayLift is an external Windows display-profile manager designed around Rust visibility and fast biome switching. It provides a dark real-time visual panel with saved profiles, automatic process activation, NVIDIA driver vibrance, Windows color/tone controls and one-click Rust presets.

This is an independent implementation based only on publicly advertised feature descriptions of color-profile utilities. It does not contain proprietary code, assets or branding from another application.

## Rust Visual panel

The main panel presents the controls in the same practical order used by dedicated Rust visual utilities:

- Saturation
- Vibrance
- Brightness
- Contrast
- Exposure
- Gamma / midtones
- Shadow lift
- Temperature and tint
- Red, green and blue channel gain

The bundled **Clean Rust** preset uses the public demonstration values `1.52` saturation, `+0.80` vibrance, `+0.06` brightness and `1.05` contrast. Additional one-click profiles cover Summer, Winter, Desert, Night, Competitive and Maximum Color.

## Profiles and automation

- A Rust profile targeting `RustClient.exe` is created automatically.
- Rust is located across Steam library folders when possible.
- Add any running program or browse directly to an executable.
- Activate a profile while its app is foregrounded or for the entire time it is running.
- Priorities resolve overlapping profiles.
- Clone, import and export profile JSON files.
- Restore the original display state after a game closes or loses focus.
- Minimize to the system tray and optionally start with Windows.

## Display backends

- NVIDIA Digital Vibrance through NVAPI when an NVIDIA-driven display is available.
- Windows full-screen color matrix for saturation, contrast, brightness, exposure, temperature, tint and RGB gain.
- Windows gamma ramp for gamma and shadow lift.
- Matrix-based vibrance approximation when driver vibrance is unavailable.

Borderless-windowed mode is recommended for the Windows full-screen color matrix. Driver vibrance and gamma support depend on the installed display driver and monitor path.

## Hotkeys

- `Ctrl+Alt+F9`: cycle Rust presets
- `Ctrl+Alt+F10`: restore original display settings
- `Ctrl+Alt+F11`: resume automatic profile switching

## Build and publish

Double-click `ONE-CLICK-BUILD.cmd` to run tests and produce:

```text
Desktop\DisplayLift\dist\DisplayLift.exe
```

Double-click `ONE-CLICK-PUBLISH.cmd` to build, initialize or update the local Git repository, create/connect the GitHub remote, push `main`, verify that local and remote commits match, create a Desktop shortcut and launch the app.

The standalone installer BAT supplied with the release creates or updates the entire `Desktop\DisplayLift` project while preserving `.git` and build output. It replaces managed source directories rather than layering duplicate files.

## Safety boundary

DisplayLift does not:

- inject DLLs;
- modify Rust or Easy Anti-Cheat files;
- read game memory;
- draw an in-game overlay;
- automate mouse or keyboard input;
- alter recoil, aim or game mechanics;
- attempt to bypass anti-cheat.

It watches only process names and changes external display settings. No third-party utility can guarantee that a game publisher will keep the same policy indefinitely, so users should review current Rust and Easy Anti-Cheat rules before use.

## License

MIT. `NvAPIWrapper.Net` has its own license; see `THIRD-PARTY-NOTICES.md`.
