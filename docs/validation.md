# Validation — Phase 1 preview

Date: September 10, 2026. Target: Valheim 1.0.7, Steam build 25185596, Windows x64, Direct3D 11.

## Passed

- Desktop and runtime compile against the installed game assemblies.
- 127 automated checks: all 53 feature IDs, disabled defaults, numeric bounds/non-finite rejection, action-versus-toggle validation, message serialization, Unicode, and payload-size limits.
- Helper attachment to the running game, all hook registrations, and authenticated named-pipe request/response.
- 44 integration checks in a user-designated single-player test world. All 20 toggles accepted enable/disable requests; the three action features accepted requests.
- Carry-weight getter override; movement and jump field multipliers; restoration of all baseline values.
- Flight and free-crafting game flags; restoration of their original states.
- Comfort getter while maximum comfort is enabled.
- Item search and spawning. Test inventory received 5 Wood, 1 CopperOre, and 1 Hammer.
- Skill editing, followed by restoring the original skill level. The partial XP accumulator is reset by the editor.
- Copper ore blocks normal portal eligibility; the toggle allows it; disabling restores the restriction.
- Achievement eligibility query returns true while feature 49 is enabled.
- Desktop layout rendered and inspected; connection buttons and disabled-control contrast corrected.
- Heartbeat-loss test: the carry override automatically disabled after 15 seconds without desktop contact.
- 26 further main-thread behavior probes passed: health damage protection, stamina/Eitr spending prevention and affordability, fall-damage interception, food and rested timer freeze/scaling, food consumption retention, equipment durability protection, repair of damaged equipment, earned XP scaling, expanded recipe access, actual weapon-damage scaling, and a one-hit kill on a temporary test creature. Temporary probe objects were removed and original health, food state, resource values, and tested skill state were restored.

## Still needs scenario testing

| Features | Remaining checks |
| --- | --- |
| 2–4, 9 | Extended play across damage sources, abilities, and falling situations |
| 15–16 | Long-running countdown and expiration behavior |
| 17 | Resting and buff duration at computed comfort |
| 23, 25 | Ranged/projectile paths and boss phases; multiplier uses normal weapon damage types, not raw untyped damage |
| 29 | Ammo and crafting consumption; normal transfers/drop behavior |
| 30–31 | Durability edge cases across all equipment types |
| 34 | Long-running progression and level-boundary behavior |
| 36–37 | Actual crafting, upgrades, recipe selection, and building piece availability |
| 39 | Movement and landing during flight |
| 43 | End-to-end traversal through a portal |
| 49 | A legitimately earned achievement reaches Steam; no direct award calls are made |
| All | Dedicated-server and multiplayer behavior, Vulkan, future game builds |

Independent sprint and swim overrides supplement the shared movement multiplier. Profiles and custom toggle/hold hotkeys are implemented. Blueprint access is temporary, while spawned items, repairs, edited skills, and gameplay progress can persist in saves.

Use `test.ps1` for automated checks. Run `tests/Integration.ps1 -AllowPersistentTestChanges` only after entering a disposable single-player test character/world and connecting the matching app build. Integration tests deliberately add items and edit a skill. Local detailed results are written under ignored `.local/`.

`tests/build-probe.ps1` builds an optional behavior probe. Run its `ProbeLauncher.exe --disposable-world` only in the authorized disposable single-player world. It exercises real game methods, temporarily creates items and a creature, and restores/removes its temporary state afterward. Existing equipment is repaired by the repair-all test; gameplay statistics may change. Results are written to `behavior-results.json` beside the probe. Probe binaries are excluded from the distribution ZIP.

## Preview-003 expanded validation

- All 53 feature registrations completed without errors on the installed build.
- 69 integration checks passed, including every toggle command and reset to baseline.
- 85 main-thread runtime checks passed, covering the original behavior suite plus god/ghost flags, three regeneration hooks, stamina/Eitr cost and affordability, guardian cooldown/duration, incoming damage, gathering damage-prefix calculations and building protection, station/roof bypass, selected inventory edits/repair, buffs, skill-death protection and tombstone-transfer suppression, independent sprint/swim, noclip collision/flight restoration, HUD, camera/FOV, local game speed, visual clock and single-player world time, saved-location persistence, profile application and validation rollback, and Steam achievement enumeration/guards.
- 14 desktop interaction checks passed: connection; real achievement list, selected unlock-button availability and empty-search behavior; editor list loading; hold-to-enable and release restoration; and disable-all clearing held controls. The empty-search check caught and fixed a stale unlock-button state. Achievement and profile pages were rendered and visually inspected.
- Achievement tests rejected unconfirmed requests and unknown IDs and verified all returned unlock states were unchanged. The test suite never invokes a valid confirmed unlock. Steam submission and synchronization are therefore **implemented but not end-to-end tested**.
- The first expanded probe stopped because its test item merged into an existing stack. The test helper now puts temporary items into separate empty slots; the rerun passed. This was a test-fixture issue, not an inventory-editor failure.

Remaining expanded scenarios: complete death/respawn sequences (individual death hooks were tested), physical wall traversal with noclip, object destruction across all tree/ore/structure types (damage-prefix logic tested), placement edge cases, distant teleport loading/return, long camera/time sessions, native OS hotkey conflicts, and all multiplayer behavior. Tests of selected teleport persistence did not initiate a teleport. Destructive gameplay and edited save/account state are not reversible through Disable all.

`tests/test-desktop.ps1 -AllowGameTestChanges` builds and runs the desktop interaction test against the connected disposable world; it toggles regeneration temporarily and resets controls, but never unlocks achievements. Requires the matching runtime to have been connected first.
