param(
    [string]$AppDirectory = (Join-Path $PSScriptRoot '..\artifacts\preview-002'),
    [switch]$AllowPersistentTestChanges
)
$ErrorActionPreference = 'Stop'
if (!$AllowPersistentTestChanges) { throw 'Run only in a disposable test character/world; pass -AllowPersistentTestChanges to authorize item and skill changes.' }
$gameProcesses = @(Get-Process -Name valheim)
if ($gameProcesses.Count -ne 1) { throw 'Exactly one Valheim process is required.' }
$gameProcessId = $gameProcesses[0].Id
$sessionSecret = (Get-Content -LiteralPath (Join-Path $AppDirectory 'runtime.token') -Raw).Trim()
$checks = [System.Collections.Generic.List[string]]::new()
function Send-Game([hashtable]$Request) {
    $Request.Auth = $sessionSecret
    $pipe = [System.IO.Pipes.NamedPipeClientStream]::new('.', ('SteamManager.Valheim.' + $gameProcessId), [System.IO.Pipes.PipeDirection]::InOut, [System.IO.Pipes.PipeOptions]::Asynchronous)
    try {
        $pipe.Connect(1500)
        $reader = [System.IO.StreamReader]::new($pipe)
        $writer = [System.IO.StreamWriter]::new($pipe)
        $writer.AutoFlush = $true
        $writer.WriteLine(($Request | ConvertTo-Json -Compress))
        $read = $reader.ReadLineAsync()
        if (!$read.Wait(10000)) { throw 'Game response timed out.' }
        $response = $read.Result | ConvertFrom-Json
        if (!$response.Ok) { throw $response.Message }
        return $response
    } finally { $pipe.Dispose() }
}
function Check([bool]$Passed, [string]$Name) { if (!$Passed) { throw ('FAILED: ' + $Name) }; $checks.Add($Name) }
function Read-Measurements { $r = Send-Game @{Command='diagnostics'}; $m=@{}; foreach($entry in $r.Entries){$m[$entry.Id]=$entry.Value}; return $m }
$originalSkill = $null
try {
    $status = Send-Game @{Command='status'}
    Check ($null -ne $status.Player -and !$status.Multiplayer) 'Single-player test character is loaded'
    Check ($status.Features.Count -eq 23 -and @($status.Features | Where-Object Error).Count -eq 0) 'All 23 feature hook registrations are available'
    $null = Send-Game @{Command='reset'}
    $baseline = Read-Measurements
    foreach($feature in $status.Features | Where-Object { !$_.Action }) {
        $value = $feature.Value
        if($feature.Id -eq 6){$value=1234}
        if($feature.Id -in 7,8){$value=1.25}
        $set = Send-Game @{Command='set';Id=$feature.Id;Enabled=$true;Value=$value}
        Check (($set.Features | Where-Object Id -eq $feature.Id).Enabled) ('Toggle command accepted: #' + $feature.Id)
        $m = Read-Measurements
        switch($feature.Id) {
            6 { Check ($m.carry -eq 1234) 'Carry-weight getter applies override' }
            7 { Check ([Math]::Abs($m.walk - $baseline.walk*1.25) -lt 0.01 -and [Math]::Abs($m.run - $baseline.run*1.25) -lt 0.01 -and [Math]::Abs($m.swim - $baseline.swim*1.25) -lt 0.01) 'Walking/running/swimming fields apply multiplier' }
            8 { Check ([Math]::Abs($m.jump - $baseline.jump*1.25) -lt 0.01) 'Jump field applies multiplier' }
            17 { Check ($m.comfort -ge $baseline.comfort) 'Comfort getter reflects enabled maximum' }
            36 { Check ($m.freeCraft -eq 1) 'Game no-cost mode is enabled' }
            39 { Check ($m.flight -eq 1) 'Game flight state is enabled' }
            49 { Check ($m.achievementEligibility -eq 1) 'Normal achievement eligibility gate remains open' }
        }
        $null = Send-Game @{Command='set';Id=$feature.Id;Enabled=$false;Value=$value}
    }
    $after = Read-Measurements
    foreach($field in 'carry','walk','run','swim','jump','flight','freeCraft') { Check ([Math]::Abs($after[$field]-$baseline[$field]) -lt 0.01) ('Restored baseline: '+$field) }
    $items = Send-Game @{Command='items';Text='Wood'}
    Check (@($items.Entries | Where-Object Id -eq 'Wood').Count -eq 1) 'Item search returns installed Wood definition'
    $null = Send-Game @{Command='spawn';Text='Wood';Quantity=5;Quality=1}
    $null = Send-Game @{Command='spawn';Text='CopperOre';Quantity=1;Quality=1}
    $null = Send-Game @{Command='spawn';Text='Hammer';Quantity=1;Quality=1}
    Check ((Read-Measurements).inventoryEntries -gt $baseline.inventoryEntries) 'Spawning adds inventory entries'
    $null = Send-Game @{Command='repair'}
    $checks.Add('Repair-all command completed (damaged-equipment behavior still needs testing)')
    Check ((Read-Measurements).portal -eq 0) 'Copper ore blocks normal portal inventory eligibility'
    $null = Send-Game @{Command='set';Id=43;Enabled=$true;Value=0}
    Check ((Read-Measurements).portal -eq 1) 'Portal toggle permits restricted inventory'
    $null = Send-Game @{Command='set';Id=43;Enabled=$false;Value=0}
    Check ((Read-Measurements).portal -eq 0) 'Portal restriction is restored when disabled'
    $skills = Send-Game @{Command='skills'}
    $originalSkill = $skills.Entries | Where-Object Id -eq 'Run' | Select-Object -First 1
    if(!$originalSkill){$originalSkill=$skills.Entries[0]}
    $targetLevel = if($originalSkill.Value -lt 99){$originalSkill.Value+1}else{98}
    $null = Send-Game @{Command='skill';Text=$originalSkill.Id;Value=$targetLevel}
    $updated = Send-Game @{Command='skills'}
    Check ([Math]::Abs(($updated.Entries | Where-Object Id -eq $originalSkill.Id).Value - $targetLevel) -lt 0.01) 'Skill editor sets requested skill level'
    $null = Send-Game @{Command='skill';Text=$originalSkill.Id;Value=$originalSkill.Value}
    $checks.Add('Edited test skill restored to its original level (partial XP accumulator resets)')
} finally {
    try { $null=Send-Game @{Command='reset'} } catch { Write-Warning 'Reset failed; close the trainer and let its heartbeat expire.' }
    $reportPath=Join-Path $PSScriptRoot '..\.local\integration-results.json'
    @{DateUtc=[DateTime]::UtcNow.ToString('O');Checks=$checks.ToArray();Count=$checks.Count} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath
}
Write-Output ($checks.Count.ToString() + ' integration checks completed. All toggles reset. Test items remain in the test inventory.')
