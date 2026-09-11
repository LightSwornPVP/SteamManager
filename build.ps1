param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim', [string]$OutputPath = 'artifacts', [switch]$DesktopOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $vsPath = & $vswhere -latest -property installationPath
    $compiler = Join-Path $vsPath 'MSBuild\Current\Bin\Roslyn\csc.exe'
}
if (!(Test-Path -LiteralPath $compiler)) { throw 'Install Visual Studio Build Tools with the C# compiler.' }
$managed = Join-Path $GamePath 'valheim_Data\Managed'
if (!(Test-Path -LiteralPath (Join-Path $managed 'assembly_valheim.dll'))) { throw 'Valheim managed assemblies were not found.' }
$outDir = Join-Path $projectRoot $OutputPath
$packages = Join-Path $projectRoot '.local\packages'
New-Item -ItemType Directory -Path $outDir,$packages -Force | Out-Null
$harmonyDir = Join-Path $packages 'harmony'
if (!(Test-Path -LiteralPath (Join-Path $harmonyDir 'lib\net472\0Harmony.dll'))) {
    $zip = Join-Path $packages 'harmony.zip'
    Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/lib.harmony/2.3.3/lib.harmony.2.3.3.nupkg' -OutFile $zip
    Expand-Archive -LiteralPath $zip -DestinationPath $harmonyDir -Force
}
if (!$DesktopOnly) {
Copy-Item -LiteralPath (Join-Path $harmonyDir 'lib\net472\0Harmony.dll') -Destination $outDir
Copy-Item -LiteralPath (Join-Path $harmonyDir 'LICENSE') -Destination (Join-Path $outDir 'Harmony-LICENSE.txt')
$hash = (Get-FileHash -LiteralPath (Join-Path $managed 'assembly_valheim.dll') -Algorithm SHA256).Hash
Set-Content -LiteralPath (Join-Path $outDir 'game-assembly.sha256') -Value $hash -Encoding ascii
$common = Join-Path $projectRoot 'src\Shared\Protocol.cs'
$helperRefs = @('mscorlib.dll','System.dll','System.Core.dll','System.Runtime.Serialization.dll','netstandard.dll','assembly_valheim.dll','assembly_utils.dll','assembly_guiutils.dll','com.rlabrecque.steamworks.net.dll','UnityEngine.CoreModule.dll','UnityEngine.PhysicsModule.dll','UnityEngine.AnimationModule.dll','UnityEngine.dll')
$argsHelper = @('/nologo','/target:library','/platform:x64','/optimize+','/langversion:latest','/nostdlib+',('/out:' + (Join-Path $outDir 'SteamManager.Runtime.dll')),('/reference:' + (Join-Path $outDir '0Harmony.dll')))
foreach ($reference in $helperRefs) { $argsHelper += '/reference:' + (Join-Path $managed $reference) }
$argsHelper += $common
$argsHelper += Join-Path $projectRoot 'src\Shared\DiscoveryRecords.cs'
$argsHelper += (Get-ChildItem (Join-Path $projectRoot 'src\Runtime\*.cs')).FullName
& $compiler @argsHelper
if ($LASTEXITCODE -ne 0) { throw 'Runtime compilation failed.' }
}
$common = Join-Path $projectRoot 'src\Shared\Protocol.cs'
$argsDesktop = @('/nologo','/target:winexe','/platform:x64','/optimize+','/langversion:latest','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Runtime.Serialization.dll',('/out:' + (Join-Path $outDir 'SteamManager.exe')),$common)
$argsDesktop += (Get-ChildItem (Join-Path $projectRoot 'src\Desktop\*.cs')).FullName
& $compiler @argsDesktop
if ($LASTEXITCODE -ne 0) { throw 'Desktop compilation failed.' }
Write-Output "Built $outDir\SteamManager.exe"
