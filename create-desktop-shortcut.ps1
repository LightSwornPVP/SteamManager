param([string]$AppDirectory=(Join-Path $PSScriptRoot 'artifacts'))
$ErrorActionPreference='Stop'
$executable=Join-Path (Resolve-Path -LiteralPath $AppDirectory).Path 'SteamManager.exe'
$icon=Join-Path $PSScriptRoot 'assets\valkyrie.ico'
if(!(Test-Path -LiteralPath $executable)){throw 'Build SteamManager before creating its shortcut.'}
if(!(Test-Path -LiteralPath $icon)){throw 'The Valkyrie icon is missing.'}
$desktop=[Environment]::GetFolderPath('Desktop')
$shortcutPath=Join-Path $desktop 'SteamManager - Valheim.lnk'
$shell=New-Object -ComObject WScript.Shell
$shortcut=$shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath=$executable
$shortcut.WorkingDirectory=Split-Path $executable
$shortcut.IconLocation=$icon+',0'
$shortcut.Description='Valheim trainer with automatic connection and live saved settings'
$shortcut.Save()
Write-Output $shortcutPath
