param([string]$AppDirectory=(Join-Path $PSScriptRoot 'artifacts'))
$ErrorActionPreference='Stop'
$instructions=@'
SteamManager - Deep North preview

Target: Steam Valheim 1.0.12, build 25253764, Windows x64.

Extract this entire folder. Keep the EXE and companion files together.
Requires .NET Framework 4.7.2 or later (included with current Windows versions).

1. Launch Valheim through Steam.
2. Run SteamManager.exe; it connects to Valheim automatically.
3. Enter a world; your last saved toggle states and values restore.
4. Ctrl+Alt+F12 disables runtime controls. Closing resets runtime controls but retains your saved settings.

No server installation or game-directory modification is required.
When switching between trainer builds/folders, restart Valheim first.
Toggle and numeric changes apply live and save immediately.
Food/rested duration: 0 freezes, 1 is normal, 2 lasts twice as long.
Flight: use the game's movement, jump, and crouch controls.

This is a tested single-player preview, not a claim of full compatibility.
Multiplayer, boss/projectile edge cases, crafting/portal traversal, and actual
Steam achievement delivery need further playtesting. Use a test save first.
Spawned items, repairs, skill edits, and gameplay progress can persist in saves.
Keep achievements enabled activates the game bypass while on and suppresses
the blocked-pickup popup. Existing item and character cheat tags remain.
Steam achievements menu: select one entry, choose Unlock selected, and confirm.
This submits a permanent Steam account change. Tests do not award achievements.
Profiles & hotkeys includes saved profiles and toggle/hold hotkey bindings.
Saved locations, status effects, and carried inventory have dedicated menus.
Item discovery can mark ingredients or all item types discovered/collected.
It backs up records first; it does not count prior crafts or add inventory.
Restart Valheim once after upgrading from an older helper.

Source and detailed validation: https://github.com/LightSwornPVP/SteamManager
'@
Set-Content -LiteralPath (Join-Path $AppDirectory 'README.txt') -Value $instructions
$names=@('SteamManager.exe','SteamManager.Runtime.dll','0Harmony.dll','game-assembly.sha256','Harmony-LICENSE.txt','README.txt')
$files=foreach($name in $names){$path=Join-Path $AppDirectory $name;if(!(Test-Path -LiteralPath $path)){throw "Missing package file: $name"};$path}
$zip=Join-Path $PSScriptRoot 'artifacts\SteamManager-DeepNorth-preview.zip'
Compress-Archive -LiteralPath $files -DestinationPath $zip -Force
Write-Output $zip
