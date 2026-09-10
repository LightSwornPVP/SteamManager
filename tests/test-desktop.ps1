param(
    [string]$AppDirectory=(Join-Path $PSScriptRoot '..\artifacts\preview-003'),
    [switch]$AllowGameTestChanges
)
$ErrorActionPreference='Stop'
if(!$AllowGameTestChanges){throw 'Run only in the connected disposable single-player world; pass -AllowGameTestChanges.'}
$compiler='C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
$projectRoot=Split-Path $PSScriptRoot
$testExe=Join-Path $AppDirectory 'DesktopQa.exe'
$compileArgs=@('/nologo','/target:exe','/main:DesktopQa','/platform:x64','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Runtime.Serialization.dll',('/out:'+$testExe),(Join-Path $projectRoot 'src\Shared\Protocol.cs'),(Join-Path $PSScriptRoot 'DesktopTests.cs'))
$compileArgs+=(Get-ChildItem (Join-Path $projectRoot 'src\Desktop\*.cs')).FullName
& $compiler @compileArgs
if($LASTEXITCODE -ne 0){throw 'Desktop test compilation failed.'}
& $testExe --disposable-world
if($LASTEXITCODE -ne 0){throw 'Desktop tests failed.'}
