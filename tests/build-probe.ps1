param([string]$AppDirectory=(Join-Path $PSScriptRoot '..\artifacts\preview-003'),[string]$GamePath='C:\Program Files (x86)\Steam\steamapps\common\Valheim',[string]$ProbeName='SteamManager.BehaviorProbe.dll')
$ErrorActionPreference='Stop'
$compiler='C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
$managed=Join-Path $GamePath 'valheim_Data\Managed'
$refs=@('mscorlib.dll','System.dll','System.Core.dll','System.Runtime.Serialization.dll','netstandard.dll','assembly_valheim.dll','assembly_utils.dll','assembly_guiutils.dll','UnityEngine.CoreModule.dll','UnityEngine.PhysicsModule.dll','UnityEngine.AnimationModule.dll','UnityEngine.dll')
$compileArgs=@('/nologo','/target:library','/platform:x64','/nostdlib+',('/out:'+(Join-Path $AppDirectory $ProbeName)),('/reference:'+(Join-Path $AppDirectory '0Harmony.dll')),('/reference:'+(Join-Path $AppDirectory 'SteamManager.Runtime.dll')))
foreach($ref in $refs){$compileArgs+='/reference:'+(Join-Path $managed $ref)}
$compileArgs+=(Join-Path $PSScriptRoot 'RuntimeProbe.cs')
$compileArgs+=(Join-Path $PSScriptRoot 'ExtendedProbe.cs')
& $compiler @compileArgs
if($LASTEXITCODE -ne 0){throw 'Probe compilation failed.'}
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $AppDirectory 'ProbeLauncher.exe')) (Join-Path $PSScriptRoot 'ProbeLauncher.cs') (Join-Path $PSScriptRoot '..\src\Desktop\MonoConnector.cs')
if($LASTEXITCODE -ne 0){throw 'Probe launcher compilation failed.'}
