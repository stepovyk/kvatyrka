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
    Set-ImageOnly $cfg
    Write-FirstRun $cfg @($packages) @('powercfg.exe /hibernate off') $root
    "реєстр: $($script:Regs.Count) записів"
    $script:Regs
    # Збирач пише лише в гілки образу, ніколи в реєстр ПК, на якому збирає.
    foreach ($r in $script:Regs) { if ($r -notmatch '^(DEL )?KV_(SOFTWARE|SYSTEM|USER)\\') { throw "Запис поза образом: $r" } }
    if ($cfg.image.localAccount -and -not ($script:Regs -match 'OOBE\\BypassNRO=1')) { throw 'Немає BypassNRO' }
    if ($cfg.image.removeOneDrive -and -not ($script:Regs -match 'DisableFileSyncNGSC=1')) { throw 'Немає заборони OneDrive' }
    $text = Get-Content (Join-Path $root 'Windows/Setup/Scripts/kvatyrka-firstrun.cmd')
    if (-not ($text -contains 'powercfg.exe /hibernate off')) { throw 'Команди покращень не потрапили в перший запуск' }
    if (-not ($text | Where-Object { $_ -like '*Kvatyrka.exe" --elevated guardian:on' })) { throw 'Охоронець не вмикається в першому запуску' }
    $text | Select-Object -First 40
}
# Профіль без Game Bar і без вимкненого запису ігор має не пройти перевірку.
$bad = Get-Content "$env:KV_PROFILES/gigant.json" -Raw -Encoding UTF8 | ConvertFrom-Json
$bad.tweaks.on = @($bad.tweaks.on | Where-Object { $_ -ne 'games.noCapture' })
$badPath = Join-Path ([IO.Path]::GetTempPath()) 'kv-bad.json'
$bad | ConvertTo-Json -Depth 8 | Set-Content $badPath -Encoding UTF8
$rejected = $false
try { Read-Config $badPath | Out-Null }
catch { if ($_.Exception.Message -notlike '*прибрано Game Bar*') { throw }; $rejected = $true }
if (-not $rejected) { throw 'Профіль без games.noCapture пройшов перевірку' }
'Профіль без games.noCapture відхилено – так і треба'

# Invoke-Tweaks бере команди з firstrun.txt, що записав Kvatyrka.exe, і зупиняється, якщо той не впорався.
$script:ExitCode = 0
function Start-Process {
    param([string]$FilePath, [string[]]$ArgumentList, [switch]$Wait, [switch]$PassThru)
    $dir = $ArgumentList[2].Trim('"')
    Set-Content (Join-Path $dir 'firstrun.txt') @('powercfg.exe /hibernate off', '', 'powercfg.exe /h on') -Encoding UTF8
    Set-Content (Join-Path $dir 'report.txt') @('Мінімум діагностики') -Encoding UTF8
    [pscustomobject]@{ ExitCode = $script:ExitCode }
}
$cfg = Read-Config "$env:KV_PROFILES/gigant.json"
$tw = Join-Path ([IO.Path]::GetTempPath()) 'kv-tweaks'
$got = @(Invoke-Tweaks $cfg 'Kvatyrka.exe' $tw | Where-Object { $_ -notlike '    *' })
if (($got -join '|') -ne 'powercfg.exe /hibernate off|powercfg.exe /h on') { throw "Invoke-Tweaks повернув: $($got -join '|')" }
$script:ExitCode = 1
$failed = $false
try { Invoke-Tweaks $cfg 'Kvatyrka.exe' $tw | Out-Null } catch { $failed = $true }
if (-not $failed) { throw 'Invoke-Tweaks не зупинився на помилці Kvatyrka.exe' }
'Invoke-Tweaks: команди й помилки – так і треба'
