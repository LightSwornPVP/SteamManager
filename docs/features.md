# Proposed feature checklist

Phase 1 control paths are implemented against Valheim 1.0.7; their behavioral validation is tracked in [the validation report](validation.md). Checkboxes below represent full feature acceptance, not merely code completion, and remain open pending scenario testing. Features outside Phase 1 are unimplemented backlog. Features that require server installation are outside scope. Overlapping controls may share an implementation.

## Phase 1 — user-selected priorities

Implement these 23 feature IDs first: **2, 3, 4, 6, 7, 8, 9, 15, 16, 17, 23, 25, 29, 30, 31, 33, 34, 35, 36, 37, 39, 43, 49**.

- Player: unlimited health, stamina, and Eitr; carry weight; movement speed; jump height; fall damage control.
- Food and rest: food duration, rested duration, and maximum comfort.
- Combat: damage dealt multiplier and one-hit kills.
- Inventory: unlimited items, unlimited durability, repair all, and searchable item spawning.
- Skills: XP multiplier and individual skill editing.
- Building and exploration: free crafting, all blueprints, flight, and portal item restrictions.
- Achievements: preserve normal earning where supported; investigate compatibility only if needed.

All other numbered features remain in the backlog. Desktop connection, build detection, feature status, and basic controls support this phase. Priority does not imply verified compatibility or completion. Original feature IDs are retained below.

## Player and survival

- [ ] 1. God mode
- [ ] 2. Unlimited health
- [ ] 3. Unlimited stamina
- [ ] 4. Unlimited Eitr
- [ ] 5. Stealth mode
- [ ] 6. Adjustable carry weight and presets
- [ ] 7. Walking, sprinting, and swimming speed controls
- [ ] 8. Jump height
- [ ] 9. Fall damage toggle
- [ ] 10. Health regeneration multiplier
- [ ] 11. Stamina regeneration multiplier
- [ ] 12. Eitr regeneration multiplier
- [ ] 13. Stamina cost multiplier
- [ ] 14. Eitr cost multiplier
- [ ] 15. Food duration multiplier or unlimited duration
- [ ] 16. Rested duration controls or unlimited duration
- [ ] 17. Maximum comfort
- [ ] 18. Apply or remove buffs
- [ ] 19. No death penalty
- [ ] 20. Independent skill-loss protection
- [ ] 21. Guardian power cooldown
- [ ] 22. Guardian power duration

## Combat and gathering

- [ ] 23. Damage dealt multiplier
- [ ] 24. Damage received multiplier
- [ ] 25. One-hit kills
- [ ] 26. Independent chopping and mining damage
- [ ] 27. One-hit object destruction
- [ ] 28. Prevent own attacks from damaging buildings

## Inventory and progression

- [ ] 29. Unlimited items
- [ ] 30. Unlimited equipment durability
- [ ] 31. Repair all equipment
- [ ] 32. Selected-item quantity and repair controls
- [ ] 33. Searchable item spawner with quantity and quality
- [ ] 34. Fast skill leveling / XP multiplier
- [ ] 35. Individual skill editor

## Building and exploration

- [ ] 36. No crafting requirements
- [ ] 37. Unlock all blueprints
- [ ] 38. Placement, workbench, and roof restriction controls
- [ ] 39. Flight
- [ ] 40. Noclip
- [ ] 41. Saved teleport locations
- [ ] 42. Return to previous location
- [ ] 43. Portal item restriction toggle

## Time and camera

- [ ] 44. Freeze daytime
- [ ] 45. Passage-of-time multiplier
- [ ] 46. Game speed multiplier
- [ ] 47. Hide HUD
- [ ] 48. Photo camera controls

## Achievements

- [ ] 49. Verify normal achievement earning and investigate compatibility only if needed and supported

## Desktop controls

- [ ] Custom hotkeys and hold-to-enable bindings
- [ ] Saved Normal, Builder, Explorer, and Recovery profiles
- [ ] Disable all active runtime modifications
- [ ] Reset individual settings
- [ ] Game and build detection
- [ ] Per-feature support and active status
- [ ] Explicit single-player and multiplayer validation status
- [ ] Separate combat controls from destructive building controls

## Validation requirements

Record the exact Steam build for each tested feature. Test enabling, disabling, restoration of runtime values, and game restart behavior. Distinguish temporary effects from persistent save changes. Test multiplayer independently from single-player before claiming compatibility. Do not distribute proprietary game binaries with the project.

## Reference

The baseline is the 21 features listed at https://www.wemod.com/cheats/valheim-trainers, expanded with the user's requested customization ideas. This project is independent of WeMod.
