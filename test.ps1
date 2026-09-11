$ErrorActionPreference = 'Stop'
$compiler = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $vsPath = & $vswhere -latest -property installationPath
    $compiler = Join-Path $vsPath 'MSBuild\Current\Bin\Roslyn\csc.exe'
}
$testDir = Join-Path $PSScriptRoot '.local\tests'
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
$testExe = Join-Path $testDir 'ProtocolTests.exe'
& $compiler /nologo /target:exe /reference:System.Runtime.Serialization.dll ('/out:' + $testExe) (Join-Path $PSScriptRoot 'src\Shared\Protocol.cs') (Join-Path $PSScriptRoot 'tests\ProtocolTests.cs') (Join-Path $PSScriptRoot 'src\Shared\DiscoveryRecords.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
