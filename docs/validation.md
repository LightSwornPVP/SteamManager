# Validation — Phase 1 preview

Date: September 10, 2026. Target: Valheim 1.0.7, Steam build 25185596, Windows x64, Direct3D 11.

## Passed

- Desktop and runtime compile against the installed game assemblies.
- 45 automated checks: exact priority IDs, disabled defaults, numeric bounds/non-finite rejection, action-versus-toggle validation, message serialization, Unicode, and payload-size limits.
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

Movement currently uses one shared multiplier for walking, running, and swimming. Only the disable-all global hotkey is implemented; custom per-feature bindings and profiles remain desktop backlog. Blueprint access is temporary, while spawned items, repairs, edited skills, and gameplay progress can persist in saves.

Use `test.ps1` for automated checks. Run `tests/Integration.ps1 -AllowPersistentTestChanges` only after entering a disposable single-player test character/world and connecting the matching app build. Integration tests deliberately add items and edit a skill. Local detailed results are written under ignored `.local/`.

`tests/build-probe.ps1` builds an optional behavior probe. Run its `ProbeLauncher.exe --disposable-world` only in the authorized disposable single-player world. It exercises real game methods, temporarily creates items and a creature, and restores/removes its temporary state afterward. Existing equipment is repaired by the repair-all test; gameplay statistics may change. Results are written to `behavior-results.json` beside the probe. Probe binaries are excluded from the distribution ZIP.
