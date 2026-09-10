# Architecture

## Desktop and runtime

`SteamManager.exe` is a Windows x64 WinForms application targeting .NET Framework. `SteamManager.Runtime.dll` is a temporary managed helper loaded into the running Valheim Mono runtime. No loader files are installed in the game directory or on a server.

The connector verifies the game assembly SHA-256 fingerprint before attachment. It resolves exported functions from the loaded Mono module, attaches a temporary thread to Mono, loads Harmony and the helper, invokes the bootstrap, and detaches that thread. Remote allocations are released after the thread finishes; allocations are deliberately retained if the thread times out rather than freeing executing code. One attachment attempt per process/session is recorded.

Harmony patches `Game.Update` and `FejdStartup.Update` as main-thread command pumps. Commands are queued from a named pipe and expire if the main thread cannot execute them promptly. The pipe authenticates requests using a local, per-install random token file restricted to the current Windows user. Nothing listens on an Internet port.

## Lifecycle

- Controls begin disabled.
- Only the local player is targeted by player and inventory hooks.
- Outgoing damage changes exclude players and tamed creatures.
- Movement and jump fields are restored to values captured when the character becomes active.
- Flight and no-cost mode restore their pre-existing states when disabled.
- Changing character or losing desktop contact for 15 seconds resets toggles on the next game update.
- Closing the desktop requests reset. The temporary helper remains loaded until Valheim exits.
- Reset does not remove spawned items, revert edited skills, undo repairs, reverse restored health, or undo saved game progress.

## Compatibility

Source was compiled against local Valheim 1.0.7 / Steam build 25185596. Live attachment, the authenticated communication channel, and all 53 control registrations passed testing. The expanded single-player runs completed 69 integration checks and 85 runtime behavior probes, plus 14 desktop interaction checks. Compilation and hook resolution alone do not establish gameplay correctness; see the validation report for remaining behavioral checks.

Feature 49 patches the normal achievement eligibility query only. It does not call Steam unlock APIs, fabricate achievement events, or erase saved cheat flags. Real achievement earning still requires end-to-end verification.

## Dependencies and reference material

- Harmony 2.3.3 (MIT): https://github.com/pardeike/Harmony
- Harmony patching documentation: https://harmony.pardeike.net/v2/articles/patching.html
- Mono embedding API: https://www.mono-project.com/docs/advanced/embedding/

Game assemblies are used locally for compilation and inspection; they are excluded from version control and release packages. Decompiled inspection files stay under ignored `.local/`.

## Expanded preview

Protocol 2 uses `SteamManager.Valheim.v2.<pid>` and verifies a response version. Restart Valheim when upgrading, because managed assemblies already loaded in Mono cannot be replaced in place.

`Extended.cs` owns the additional gameplay hooks and editors. Profiles validate every setting before applying; an apply failure restores previous control settings. Inventory selection uses runtime tokens referring to carried item instances, so removed or moved items are rejected. World bookmarks are stored by world UID. Global time scale, camera, and HUD state are captured when their override is first enabled and restored on disable/reset.

Feature 50 enumerates Steam's live achievement definitions and state. A selected unlock requires `Confirmed=true`, validates the ID against that enumeration, and calls `SetAchievement` followed by `StoreStats`. Submission is distinguished from remote Steam synchronization. Feature 49 remains the separate normal-earning eligibility toggle. No achievement API writes are made by the test suite.
