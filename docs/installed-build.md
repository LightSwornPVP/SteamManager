# Installed build inspection

Inspected September 10, 2026 from the local Steam manifest, game directory, and running game's Player.log.

| Property | Observed value |
| --- | --- |
| Steam app ID | 892970 |
| Installed and target Steam build | 25185596 |
| Download state | Fully installed; downloaded bytes match requested bytes |
| Runtime game version | 1.0.7 |
| Network version | 39 |
| Unity version | 6000.0.75f1 |
| Runtime | Mono; MonoBleedingEdge and managed game assemblies present |
| Active renderer | Direct3D 11 |
| Install directory | `C:\Program Files (x86)\Steam\steamapps\common\Valheim` |

Valheim was already running during inspection. No game files, game memory, saves, or server settings were changed.

The managed Mono runtime makes a runtime-loaded helper a candidate for the desktop trainer. This is an implementation hypothesis, not a verified attachment mechanism. Inspect the relevant game methods and validate runtime loading and reversible controls before claiming feature support. Runtime loading would not require a conventional persistent client mod installation or a server installation.

Achievement behavior and all 23 Phase 1 features remain unverified.
