# SteamManager

A Windows desktop trainer prototype for the Steam edition of Valheim, targeting the Deep North release.

## Requirements

- Standalone desktop controls with toggles, sliders, and hotkeys.
- No server-side installation.
- Target a runtime trainer rather than a conventional installed client mod.
- Support single-player worlds and test client-side behavior when connected to the owner's server.
- Detect the installed game build and clearly report unsupported features.
- Keep achievements earnable through normal gameplay where supported; do not unlock achievements on demand.

## Status

The first implementation phase contains 23 user-selected features: **2, 3, 4, 6, 7, 8, 9, 15, 16, 17, 23, 25, 29, 30, 31, 33, 34, 35, 36, 37, 39, 43, 49**. Other numbered features remain in the backlog; see [the feature checklist](docs/features.md).

The desktop application and all 23 Phase 1 control paths are implemented and compile against **Valheim 1.0.7 / Steam build 25185596**. Live attachment and authenticated communication work. **45 automated checks, 44 single-player integration checks, and 26 direct gameplay behavior probes passed.** Boss/projectile edge cases, actual crafting/portal traversal, and Steam achievement delivery still need playtesting; multiplayer is unverified. See [the validation report](docs/validation.md).

## Build and test

Requires Windows x64, Visual Studio C# Build Tools, .NET Framework 4.7.2 or later, and the Steam game installed locally. The build downloads the pinned Harmony package if needed.

```powershell
.\build.ps1
.\test.ps1
```

For another installation, pass `-GamePath 'D:\SteamLibrary\steamapps\common\Valheim'`. The executable and its companion files are produced in `artifacts/`. Keep the executable, runtime helper, Harmony library, and fingerprint file together.

Launch Valheim, open the desktop app, and choose **Connect to Valheim**. Enter a world to enable gameplay controls. **Ctrl+Alt+F12** disables active runtime controls; closing the app also requests reset. Numeric edits take effect when you enable the feature or click **Apply value**. Food/rested duration `0` freezes the existing duration; `1` is normal.

The item browser and skill editor perform persistent changes. Use a disposable test character/world while this remains a prototype. No server or game-directory installation is required.

See [architecture and lifecycle details](docs/architecture.md).

See [the proposed feature checklist](docs/features.md). Saved inventory, skills, and world changes are not undone by disabling runtime features.
