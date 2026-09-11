# Feature checklist

All original features 1–49 and additions 50–54 are implemented in preview-003. Implementation is distinct from scenario validation; see [validation](validation.md). No server installation is required.

## Player and survival

- [x] 1. God mode
- [x] 2. Unlimited health
- [x] 3. Unlimited stamina
- [x] 4. Unlimited Eitr
- [x] 5. Stealth mode
- [x] 6. Adjustable carry weight and presets
- [x] 7. Walking, sprinting, and swimming speed controls
- [x] 8. Jump height
- [x] 9. Fall damage toggle
- [x] 10. Health regeneration multiplier
- [x] 11. Stamina regeneration multiplier
- [x] 12. Eitr regeneration multiplier
- [x] 13. Stamina cost multiplier
- [x] 14. Eitr cost multiplier
- [x] 15. Food duration multiplier or unlimited duration
- [x] 16. Rested duration controls or unlimited duration
- [x] 17. Maximum comfort
- [x] 18. Apply or remove buffs
- [x] 19. No death penalty
- [x] 20. Independent skill-loss protection
- [x] 21. Guardian power cooldown
- [x] 22. Guardian power duration

## Combat and gathering

- [x] 23. Damage dealt multiplier
- [x] 24. Damage received multiplier
- [x] 25. One-hit kills
- [x] 26. Independent chopping and mining damage
- [x] 27. One-hit object destruction
- [x] 28. Prevent own attacks from damaging buildings

## Inventory and progression

- [x] 29. Unlimited items
- [x] 30. Unlimited equipment durability
- [x] 31. Repair all equipment
- [x] 32. Selected-item quantity and repair controls
- [x] 33. Searchable item spawner with quantity and quality
- [x] 34. Fast skill leveling / XP multiplier
- [x] 35. Individual skill editor

## Building and exploration

- [x] 36. No crafting requirements
- [x] 37. Unlock all blueprints
- [x] 38. Placement, workbench, and roof restriction controls
- [x] 39. Flight
- [x] 40. Noclip
- [x] 41. Saved teleport locations
- [x] 42. Return to previous location
- [x] 43. Portal item restriction toggle

## Time and camera

- [x] 44. Freeze daytime
- [x] 45. Passage-of-time multiplier
- [x] 46. Game speed multiplier
- [x] 47. Hide HUD
- [x] 48. Photo camera controls

## Achievements

- [x] 49. Keep normal achievement eligibility enabled (Steam delivery still requires playtesting)

- [x] 50. Searchable Steam achievement menu with selected, confirmed unlock
- [x] 51. Independent sprint multiplier
- [x] 52. Independent swim multiplier
- [x] 53. Independent mining multiplier

- [x] 54. Item discovery and ingredient/all-item pickup-record actions

## Desktop controls

- [x] Custom hotkeys and hold-to-enable bindings
- [x] Saved Normal, Builder, Explorer, and Recovery profiles
- [x] Disable all active runtime modifications
- [x] Reset individual settings
- [x] Game and build detection
- [x] Per-feature support and active status
- [x] Explicit single-player and multiplayer validation status
- [x] Separate combat controls from destructive building controls

## Validation requirements

Record the exact Steam build for each tested feature. Test enabling, disabling, restoration of runtime values, and game restart behavior. Distinguish temporary effects from persistent save changes. Test multiplayer independently from single-player before claiming compatibility. Do not distribute proprietary game binaries with the project.

## Reference

The baseline is the 21 features listed at https://www.wemod.com/cheats/valheim-trainers, expanded with the user's requested customization ideas. This project is independent of WeMod.
