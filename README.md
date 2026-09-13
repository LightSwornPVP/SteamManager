# SteamManager

A Windows desktop trainer prototype for the Steam edition of Valheim, targeting the Deep North release.


## Blueprint tools (first implementation)

The Blueprints page imports PlanBuild `.blueprint` and BuildShare `.vbuild` geometry and exports captured player-built pieces as `.blueprint`. Select a capture radius (1–50 m) around your character to save nearby building pieces. This is a radius selection, not a connected-building selection. Import a file, inspect unavailable pieces/materials/warnings, then use Show / update preview. The anchor starts 6 m ahead of the character; X, height, Z and rotation apply when the preview button is clicked. Move preview here changes the anchor to your current position plus 6 m forward.

The preview draws meshes only and creates no networked pieces. It defaults to the original piece textures and materials with a solid appearance; turn off Real textures and materials for a subdued translucent teal ghost. The choice saves automatically and changes the active preview immediately. Only the highest detail level is drawn, and source materials are never modified. Real-material rendering is compiled and UI-checked; its in-game appearance still needs verification. Place blueprint requires confirmation, validates distance and protected/no-build areas, and uses Valheim's `Player.PlacePiece` to create ordinary building pieces at up to ten per second. Materials and crafting stations are required unless Free crafting (#36) or the game's no-cost mode is enabled. Cancel, closing SteamManager, loss of its heartbeat, character changes or leaving the world stop subsequent placement; already placed pieces remain. Normal structural support applies. Collision, ground alignment and all vanilla placement conditions are **not** comprehensively reproduced, and no undo is provided yet.

Preview limits: 12,000 objects and 2 MB input, including scales and available non-building models. The import checkbox defaults to preview-only. Large, scaled and non-building imports are locked to preview; the runtime independently rejects placement even if the flag is removed. Placement remains limited to 500 normal-scale build-tool pieces within 100 m of the character. Terrain changes, custom snap markers, container contents, sign text, item-stand data and other extra state are excluded and reported on import. Scaled objects retain their transforms for preview. Available rocks, vegetation and creatures are rendered as meshes only; no prefabs, actors, colliders, terrain changes or networked objects are created. Missing renderable models are reported and omitted. Terrain and extra saved state remain excluded. No PlanBuild/BepInEx installation or server mod is needed. A live Valheim 1.0.12 multiplayer test confirmed visible preview rendering, four wood-floor placements with Free crafting enabled, survival through a follow-up capture, and capture/export/import of those four pieces. Other-client visibility, persistence after reconnect, normal material consumption, and larger structures remain unverified. Test small structures on supported ground first; the initial anchor preserves player height rather than snapping to terrain. [StarterPlatform.blueprint](examples/StarterPlatform.blueprint) is a four-piece test fixture.

Format handling was checked against [PlanBuild's format reader](https://github.com/sirskunkalot/PlanBuild/tree/master/PlanBuild/Blueprints). The implementation has 27 isolated format checks alongside the existing protocol/map checks. File and command data are validated independently before use.

## Requirements

- Standalone desktop controls with toggles, sliders, and hotkeys.
- No server-side installation.
- Target a runtime trainer rather than a conventional installed client mod.
- Support single-player worlds and test client-side behavior when connected to the owner's server.
- Detect the installed game build and clearly report unsupported features.
- Keep achievements earnable through normal gameplay, plus an explicit menu for unlocking a selected Steam achievement.

## Status

All **54 feature controls** are implemented against **Valheim 1.0.12 / Steam build 25253764**, including the full original list, independent sprint/swim/mining overrides, and the selected Steam achievement menu. The desktop also includes per-feature reset, carry presets, saved profiles, custom global hotkeys, and hold-to-enable bindings.

The **Map tools** page adds world/nearby location scans, loaded resource scans within 200 m, prefab-name search, temporary selected/matching pins, and full fog reveal. Locally hosted worlds expose their recorded locations; remote clients show only server-provided markers, not the server's complete location list. Resource results are snapshots and can include depleted objects. No seed cracking or procedural world generation is performed. Pins are not saved, duplicate requests do not add duplicates, and Remove my pins preserves user pins. Fog reveal requires confirmation, runs in batches over multiple frames, and is saved normally by Valheim; it cannot be undone through this menu. Name-filter pinning is capped at 250 results per request and 500 temporary pins in total.

Map tools also includes Show seed and Copy seed, which read the current world seed received by the client. Seed values are returned only for an explicit seed request, are not stored in preferences, and are copied only when Copy seed is clicked. No server installation is needed.

Map command behavior has 19 isolated checks covering host/client visibility, scan radius, pin ownership and duplication, world changes, and reveal confirmation. The updated runtime compiles against the installed 1.0.12 assemblies; live map behavior still needs verification after restarting the game with the updated helper.

The original 23 controls passed single-player tests and user playtesting. The expanded build passes **127 automated protocol checks, 69 live integration checks, 85 runtime behavior checks, and 14 desktop interaction checks**. See [the validation report](docs/validation.md) for tested behavior and remaining limits. Multiplayer remains unverified.

## Build and test

Requires Windows x64, Visual Studio C# Build Tools, .NET Framework 4.7.2 or later, and the Steam game installed locally. The build downloads the pinned Harmony package if needed.

```powershell
.\build.ps1
.\test.ps1
```

For another installation, pass `-GamePath 'D:\SteamLibrary\steamapps\common\Valheim'`. The executable and its companion files are produced in `artifacts/`. Keep the executable, runtime helper, Harmony library, and fingerprint file together.

Launch Valheim and open the desktop app. It connects automatically and restores your last saved toggle states and numeric values when a character enters the world. Enter a world to enable gameplay controls. **Ctrl+Alt+F12** disables active runtime controls; closing the app also requests reset. Toggles and numeric edits apply live and save immediately; there is no Apply button. Food/rested duration `0` freezes the existing duration; `1` is normal.

The item, inventory, and skill editors perform persistent save changes. The **Steam achievements** page loads names, descriptions, and locked/unlocked status from Steam; select an entry and choose **Unlock selected** to submit that achievement after confirmation. This affects the Steam account and is separate from the normal eligibility toggle. Use a disposable test character/world while this remains a prototype. No server or game-directory installation is required.

See [architecture and lifecycle details](docs/architecture.md).

See [the feature checklist](docs/features.md). Saved inventory, skills, and world changes are not undone by disabling runtime features.

## Expanded controls

- **Profiles & hotkeys:** apply Normal, Builder, Explorer, or Recovery, or save the current applied settings under a custom name. Named profiles apply when selected; the resulting settings save automatically and restore on the next connection. Assign Ctrl/Alt combinations to individual toggles, optionally hold-to-enable. Release restores the previous enabled state and value. Ctrl+Alt+F12 clears active holds and disables all.
- **Status effects:** search installed effects, apply one with its normal duration or a custom duration in seconds, or remove an active effect.
- **Carried inventory:** refresh, choose a stack, change its quantity within its game-defined limit, or repair that item.
- **Saved locations:** save the current position under a name, teleport to a selected location, or return to the previous position. Lists are isolated by world ID.
- **Time and camera:** freeze visual daytime, adjust time passage, change local game speed, hide the HUD, or enter free camera with adjustable FOV. Time passage changes the world clock only in single-player; connected multiplayer uses local visual daytime. Local game speed may desynchronize multiplayer.
- **Gathering:** chopping/mining damage plus an independent mining override. Building protection takes priority over your one-hit object destruction.

Desktop preferences and world location files live in `%LocalAppData%\SteamManager`. No death penalty preserves inventory and skills; food/effects and respawning still follow the game. Building restriction overrides do not grant materials; enable free crafting separately.

The app checks for Valheim every two seconds and reconnects after lost contact or a new game process. First use adopts the current game controls; later launches restore your saved settings. Closing temporarily resets runtime controls while retaining saved preferences. **Disable all** also saves the disabled state. Hold-to-enable hotkeys are temporary and never save their held state. One-time actions such as item spawning, teleporting, and achievement unlocks are never replayed on reconnect. Game save files continue to use Valheim's normal save system.

## Item discovery

Open **Item discovery** and refresh to inspect discovered item types and eligible pickup counts at the current achievement difficulty. **Discover ingredients** covers enabled crafting recipes and building-piece resources. **Mark all items collected** covers available item definitions. Confirming either action adds missing discovery records and raises missing/zero pickup counts to one in raw, shared achievement, and current-difficulty records. Existing counts are never reduced, other difficulty tiers are untouched, and no inventory items or crafted-item counts are created.

The action makes a JSON backup of known materials, known recipes, and pickup records in `%LocalAppData%\SteamManager\discovery-backups` before changing anything. The changes reach the character save through Valheim's normal saving. These records can affect pickup achievements when the game next evaluates them. The action is never replayed on reconnection. Keep feature 49 enabled for future crafting progress; previously missed crafts must be crafted again. Discovery is separate from the game's cheat flags, which this action does not erase.

This update uses protocol 3 and needs one Valheim restart when replacing a previously loaded helper.

## September 11 compatibility and achievement fix

Updated the checked game fingerprint for Valheim 1.0.12 / Steam build 25253764. All 54 runtime features registered without errors. The 133 protocol/data checks and eight targeted live eligibility/notification checks passed on this build; earlier broad gameplay results refer to 1.0.7.

Feature 49 now overrides the game's `s_bypassCheatChecks` query while active, in addition to the eligibility query, so the built-in achievement screen recognizes bypass mode. The local blocked-item pickup notification is suppressed only while this override is active. Disabling restores the game's original behavior without modifying its saved bypass key or item tags. Selected repair no longer passes an unconditional cheated-state-change flag to the inventory notification path. End-to-end Steam achievement delivery remains unverified; tests do not award achievements.

Large castle preview: the supplied 8,557-record file passes five additional exact-file checks, preserves nine scales, round-trips through IPC, and is rejected by placement validation. Mesh templates are cached by prefab; preparation processes at most 100 records or 4 ms of work per frame (an individual asset operation can take longer). Rendering groups identical meshes/materials into GPU-instanced batches of up to 1,023 instances. Original materials are cloned for instancing rather than modified. An incompatible instancing path may reject a large preview instead of issuing more than 3,000 individual draws per frame. Large-preview in-game performance and appearance still need verification.
