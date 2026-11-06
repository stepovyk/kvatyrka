$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3
$script:Regs = @()
function Set-Reg([string]$Key, [string]$Name, $Value, [string]$Type = 'DWord') { $script:Regs += "$Key\$Name=$Value ($Type)" }
function Remove-RegKey([string]$Key) { $script:Regs += "DEL $Key" }
function Remove-RegValue([string]$Key, [string]$Name) { $script:Regs += "DEL $Key\$Name" }
function Write-Note([string]$Text) { "    $Text" }
$Editions = @{ 'Pro' = 'Professional' }
foreach ($name in 'universal', 'gigant') {
    $cfg = Read-Config "$env:KV_PROFILES/$name.json"
    "== $($cfg.name): $($cfg.packages.Count) компонентів"
    $i = 0
    $packages = foreach ($p in $cfg.packages) { $i++; [pscustomobject]@{ Name = $p.name; File = ("{0:D2}-x.exe" -f $i); Args = $p.args; Then = $(if ($p.PSObject.Properties.Name -contains 'then') { $p.then } else { $null }) } }
    $root = Join-Path ([IO.Path]::GetTempPath()) "kv-$name"
    Remove-Item $root -Recurse -Force -ErrorAction SilentlyContinue
    $script:Regs = @()
    Set-Policies $cfg
    Write-FirstRun $cfg @($packages) $root
    "реєстр: $($script:Regs.Count) записів"
    Get-Content (Join-Path $root 'Windows/Setup/Scripts/kvatyrka-firstrun.cmd') | Select-Object -First 40
}
