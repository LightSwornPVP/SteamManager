# SteamManager

A Windows desktop trainer prototype for the Steam edition of Valheim, targeting the Deep North release.

## Requirements

- Standalone desktop controls with toggles, sliders, and hotkeys.
- No server-side installation.
- Target a runtime trainer rather than a conventional installed client mod.
- Support single-player worlds and test client-side behavior when connected to the owner's server.
- Detect the installed game build and clearly report unsupported features.
- Keep achievements earnable through normal gameplay, plus an explicit menu for unlocking a selected Steam achievement.

## Status

All **53 feature controls** are implemented against **Valheim 1.0.7 / Steam build 25185596**, including the full original list, independent sprint/swim/mining overrides, and the selected Steam achievement menu. The desktop also includes per-feature reset, carry presets, saved profiles, custom global hotkeys, and hold-to-enable bindings.

The original 23 controls passed single-player tests and user playtesting. The expanded build passes **127 automated protocol checks, 69 live integration checks, 85 runtime behavior checks, and 14 desktop interaction checks**. See [the validation report](docs/validation.md) for tested behavior and remaining limits. Multiplayer remains unverified.

## Build and test

Requires Windows x64, Visual Studio C# Build Tools, .NET Framework 4.7.2 or later, and the Steam game installed locally. The build downloads the pinned Harmony package if needed.

```powershell
.\build.ps1
.\test.ps1
```

For another installation, pass `-GamePath 'D:\SteamLibrary\steamapps\common\Valheim'`. The executable and its companion files are produced in `artifacts/`. Keep the executable, runtime helper, Harmony library, and fingerprint file together.

Launch Valheim, open the desktop app, and choose **Connect to Valheim**. Enter a world to enable gameplay controls. **Ctrl+Alt+F12** disables active runtime controls; closing the app also requests reset. Numeric edits take effect when you enable the feature or click **Apply value**. Food/rested duration `0` freezes the existing duration; `1` is normal.

The item, inventory, and skill editors perform persistent save changes. The **Steam achievements** page loads names, descriptions, and locked/unlocked status from Steam; select an entry and choose **Unlock selected** to submit that achievement after confirmation. This affects the Steam account and is separate from the normal eligibility toggle. Use a disposable test character/world while this remains a prototype. No server or game-directory installation is required.

See [architecture and lifecycle details](docs/architecture.md).

See [the feature checklist](docs/features.md). Saved inventory, skills, and world changes are not undone by disabling runtime features.

## Expanded controls

- **Profiles & hotkeys:** apply Normal, Builder, Explorer, or Recovery, or save the current applied settings under a custom name. Profiles never apply automatically. Assign Ctrl/Alt combinations to individual toggles, optionally hold-to-enable. Release restores the previous enabled state and value. Ctrl+Alt+F12 clears active holds and disables all.
- **Status effects:** search installed effects, apply one with its normal duration or a custom duration in seconds, or remove an active effect.
- **Carried inventory:** refresh, choose a stack, change its quantity within its game-defined limit, or repair that item.
- **Saved locations:** save the current position under a name, teleport to a selected location, or return to the previous position. Lists are isolated by world ID.
- **Time and camera:** freeze visual daytime, adjust time passage, change local game speed, hide the HUD, or enter free camera with adjustable FOV. Time passage changes the world clock only in single-player; connected multiplayer uses local visual daytime. Local game speed may desynchronize multiplayer.
- **Gathering:** chopping/mining damage plus an independent mining override. Building protection takes priority over your one-hit object destruction.

Desktop preferences and world location files live in `%LocalAppData%\SteamManager`. No death penalty preserves inventory and skills; food/effects and respawning still follow the game. Building restriction overrides do not grant materials; enable free crafting separately.
