#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Збирає чистий образ Кватирки з офіційного ISO Windows 11.

.DESCRIPTION
    Бере ваш офіційний ISO від Microsoft і робить з нього свій: прибирає нав'язані програми, вмикає
    покращення Кватирки (без реклами, зайвого стеження й підказок), додає потрібні компоненти й саму
    Кватирку з охоронцем, який після оновлень Windows повертає ваш вибір. Що саме робити – описано
    в профілі (builder\profiles\*.json), його можна скопіювати й змінити під себе. Покращення – ті самі,
    що й у вікні Кватирки (catalog\catalog.json), і застосовує їх той самий Kvatyrka.exe.

.EXAMPLE
    .\Build-Kvatyrka.ps1 -Iso D:\Win11_26H2_Ukrainian_x64.iso
    .\Build-Kvatyrka.ps1 -Iso D:\Win11.iso -Config gigant
    .\Build-Kvatyrka.ps1 -Iso D:\Win11.iso -Config C:\мій-профіль.json -Check
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Iso,
    [string]$Config = 'universal',
    [string]$Out = (Join-Path $PSScriptRoot 'out'),
    [string]$Shell = (Join-Path $PSScriptRoot 'shell\Kvatyrka.exe'),
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
# Відносні шляхи – від поточної папки PowerShell, а не від System32, де працюють reg.exe і .NET.
$Out = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Out)
$Shell = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Shell)
Set-StrictMode -Version 3

$script:Step = 0
function Write-Step([string]$Text) {
    $script:Step++
    Write-Host ''
    Write-Host ("[{0}] {1}" -f $script:Step, $Text) -ForegroundColor Cyan
}
function Write-Note([string]$Text) { Write-Host "    $Text" -ForegroundColor DarkGray }
function Fail([string]$Text) { throw $Text }

# ---------- профіль ----------
function Read-Config([string]$NameOrPath) {
    $path = if (Test-Path $NameOrPath) { $NameOrPath } else { Join-Path $PSScriptRoot "profiles\$NameOrPath.json" }
    if (-not (Test-Path $path)) { Fail "Немає профілю «$NameOrPath». Є: $((Get-ChildItem (Join-Path $PSScriptRoot 'profiles') -Filter *.json).BaseName -join ', ')" }
    $cfg = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($key in 'name', 'edition', 'keepApps', 'image', 'tweaks', 'registry', 'folders', 'drivers', 'wallpaper', 'packages', 'kvatyrka') {
        if (-not ($cfg.PSObject.Properties.Name -contains $key)) { Fail "У профілі $path немає поля «$key»" }
    }
    foreach ($key in 'localAccount', 'removeOneDrive', 'blockOutlookAndDevHome', 'recoveryEnvironment') {
        if (-not ($cfg.image.PSObject.Properties.Name -contains $key)) { Fail "У профілі $path немає поля «image.$key»" }
    }
    # Без Game Bar Windows при кожній грі просить його встановити, якщо не вимкнути запис ігор.
    $gameBar = $cfg.keepApps | Where-Object { 'Microsoft.XboxGamingOverlay' -like $_ }
    if (-not $gameBar -and -not (@($cfg.tweaks.on) -contains 'games.noCapture')) {
        Fail "У профілі $path прибрано Game Bar – додайте «games.noCapture» у tweaks.on"
    }
    $cfg | Add-Member -NotePropertyName Path -NotePropertyValue (Resolve-Path $path).Path
    return $cfg
}

$Editions = @{ 'Home' = 'Core'; 'Pro' = 'Professional'; 'Education' = 'Education'; 'Enterprise' = 'Enterprise'; 'ProWorkstations' = 'ProfessionalWorkstation' }

# ---------- реєстр образу ----------
$Hives = [ordered]@{ 'KV_SOFTWARE' = 'Windows\System32\config\SOFTWARE'; 'KV_SYSTEM' = 'Windows\System32\config\SYSTEM'; 'KV_USER' = 'Users\Default\NTUSER.DAT' }

function Mount-Hives([string]$Root) {
    foreach ($h in $Hives.GetEnumerator()) {
        & reg.exe load "HKLM\$($h.Key)" (Join-Path $Root $h.Value) | Out-Null
        if ($LASTEXITCODE -ne 0) { Fail "Не вдалося відкрити реєстр образу: $($h.Value)" }
    }
}

# Відключає всі завантажені гілки; ті, що вже відключені, пропускає. Помилки reg.exe не зупиняють повтори.
function Dismount-Hives {
    $ErrorActionPreference = 'Continue'
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    $left = @()
    foreach ($h in $Hives.Keys) {
        if (-not (Test-Path "Registry::HKEY_LOCAL_MACHINE\$h")) { continue }
        for ($i = 0; $i -lt 10; $i++) {
            & cmd.exe /c "reg unload HKLM\$h >nul 2>&1"
            if (-not (Test-Path "Registry::HKEY_LOCAL_MACHINE\$h")) { break }
            [GC]::Collect(); Start-Sleep -Milliseconds 500
        }
        if (Test-Path "Registry::HKEY_LOCAL_MACHINE\$h") { $left += $h }
    }
    if ($left) { throw "Не вдалося відключити реєстр образу: $($left -join ', ')" }
}

# Запис у завантажений реєстр образу через .NET: без проблем із лапками в аргументах reg.exe.
function Set-Reg([string]$Key, [string]$Name, $Value, [string]$Type = 'DWord') {
    $k = [Microsoft.Win32.Registry]::LocalMachine.CreateSubKey($Key)
    try {
        if ($Type -eq 'DWord') { $Value = [int]$Value }
        $k.SetValue($Name, $Value, [Microsoft.Win32.RegistryValueKind]$Type)
    }
    finally { $k.Dispose() }
}

# $true – ключа більше немає (прибрано або й не було).
function Remove-RegKey([string]$Key) {
    try { [Microsoft.Win32.Registry]::LocalMachine.DeleteSubKeyTree($Key, $false); return $true }
    catch { Write-Warning "Не вдалося прибрати $Key ($($_.Exception.Message))"; return $false }
}

function Remove-RegValue([string]$Key, [string]$Name) {
    $k = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey($Key, $true)
    if ($k) { try { $k.DeleteValue($Name, $false) } finally { $k.Dispose() } }
}

# Те, що має сенс лише в новому образі; решту покращень застосовує Kvatyrka.exe з каталогу.
function Set-ImageOnly($cfg) {
    $sw = 'KV_SOFTWARE'; $usr = 'KV_USER'
    $img = $cfg.image
    if ($img.localAccount) {
        Set-Reg "$sw\Microsoft\Windows\CurrentVersion\OOBE" 'BypassNRO' 1
        Write-Note 'Під час встановлення можна створити локальний акаунт'
    }
    if ($img.removeOneDrive) {
        Remove-RegValue "$usr\Software\Microsoft\Windows\CurrentVersion\Run" 'OneDriveSetup'
        Set-Reg "$sw\Policies\Microsoft\Windows\OneDrive" 'DisableFileSyncNGSC' 1
        Write-Note 'OneDrive не встановлюється'
    }
    if ($img.blockOutlookAndDevHome) {
        $ok = $true
        foreach ($k in 'OutlookUpdate', 'DevHomeUpdate') {
            foreach ($s in 'UScheduler_Oobe', 'UScheduler') { $ok = (Remove-RegKey "$sw\Microsoft\WindowsUpdate\Orchestrator\$s\$k") -and $ok }
        }
        if ($ok) { Write-Note 'Outlook і Dev Home не встановлюються самі' }
    }
    # Заготовки «рекомендованих» програм, які Windows ставить при першому вході.
    $ok = $true
    foreach ($k in 'Subscriptions', 'SuggestedApps') { $ok = (Remove-RegKey "$usr\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager\$k") -and $ok }
    if ($ok) { Write-Note 'Заготовки «рекомендованих» програм прибрано' }
}

# Покращення з профілю застосовує сам Kvatyrka.exe – тим самим рушієм, що й вікно Кватирки.
function Invoke-Tweaks($cfg, [string]$Exe, [string]$Dir) {
    New-Item -ItemType Directory -Force -Path $Dir | Out-Null
    $p = Start-Process -FilePath $Exe -ArgumentList '--image', "`"$($cfg.Path)`"", "`"$Dir`"" -Wait -PassThru
    $report = Join-Path $Dir 'report.txt'
    if (Test-Path $report) { Get-Content $report -Encoding UTF8 | ForEach-Object { Write-Note $_ } }
    if ($p.ExitCode -ne 0) { Fail "Kvatyrka.exe не застосувала покращення (код $($p.ExitCode))" }
    return @(Get-Content (Join-Path $Dir 'firstrun.txt') -Encoding UTF8 | Where-Object { $_ })
}

# ---------- пакунки ----------
function Get-Packages($cfg, [string]$Dir) {
    New-Item -ItemType Directory -Force -Path $Dir | Out-Null
    $list = @()
    $i = 0
    foreach ($p in $cfg.packages) {
        $i++
        $file = Join-Path $Dir ("{0:D2}-{1}" -f $i, ([IO.Path]::GetFileName(([Uri]$p.url).AbsolutePath)))
        if (-not $file.EndsWith('.exe')) { $file += '.exe' }
        if (-not (Test-Path $file)) {
            Write-Note "Завантажую: $($p.name)"
            Invoke-WebRequest -Uri $p.url -OutFile $file -UseBasicParsing
        }
        $sig = Get-AuthenticodeSignature $file
        if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
            Remove-Item $file -Force
            Fail "$($p.name): файл не підписаний Microsoft, зупиняюся"
        }
        $list += [pscustomobject]@{ Name = $p.name; File = [IO.Path]::GetFileName($file); Args = $p.args; Then = $(if ($p.PSObject.Properties.Name -contains 'then') { $p.then } else { $null }) }
    }
    return $list
}

# Перший запуск: SetupComplete (якщо Windows його виконає) і RunOnce як запасний шлях; виконується один раз.
function Write-FirstRun($cfg, $packages, $commands, [string]$Root) {
    $dir = Join-Path $Root 'Windows\Setup\Scripts'
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $lines = @(
        '@echo off',
        'rem Кватирка: перший запуск після встановлення. Виконується один раз.',
        'chcp 65001 >nul',
        'if exist "%WINDIR%\Setup\Scripts\kvatyrka.done" exit /b 0',
        'set "PKG=%WINDIR%\Setup\Scripts\packages"',
        'set "LOG=%WINDIR%\Setup\Scripts\kvatyrka-firstrun.log"',
        '>> "%LOG%" echo %DATE% %TIME% початок'
    )
    # Після кожного кроку – запис у журнал, якщо він не вдався (3010 – «потрібне перезавантаження», це успіх).
    # Перенаправлення спереду: цифра в кінці назви («.NET 8») інакше стала б номером потоку. Службові символи cmd з назви прибрано.
    function Step([string]$Name, [string]$Command) {
        $safe = $Name -replace '[&|<>^%()"]', ''
        return @($Command, "if errorlevel 1 if not errorlevel 3010 >> `"%LOG%`" echo [помилка %ERRORLEVEL%] $safe")
    }
    foreach ($p in $packages) {
        $lines += "rem $($p.Name)"
        if ($p.Then) {
            $tmp = '%TEMP%\kvatyrka-' + [IO.Path]::GetFileNameWithoutExtension($p.File)
            $lines += Step $p.Name "start `"`" /wait `"%PKG%\$($p.File)`" $($p.Args.Replace('{dir}', $tmp))"
            $lines += Step $p.Name "start `"`" /wait `"$tmp\$($p.Then.Split(' ')[0])`" $(($p.Then.Split(' ') | Select-Object -Skip 1) -join ' ')"
            $lines += "rd /s /q `"$tmp`""
        }
        else {
            $lines += Step $p.Name "start `"`" /wait `"%PKG%\$($p.File)`" $($p.Args)"
        }
    }
    foreach ($c in $commands) { $lines += Step $c $c }
    foreach ($f in $cfg.folders) { $lines += "mkdir `"$f`" 2>nul" }
    # Охоронець: після оновлень Windows повертає вибір. Не встановився – позначки «готово» немає, і запасний шлях спробує знову.
    if ($cfg.kvatyrka) {
        $lines += 'start "" /wait "%ProgramFiles%\Kvatyrka\Kvatyrka.exe" --elevated guardian:on'
        $lines += 'if errorlevel 1 (>> "%LOG%" echo [помилка %ERRORLEVEL%] охоронець & exit /b 1)'
    }
    $lines += '>> "%LOG%" echo %DATE% %TIME% готово'
    $lines += 'echo done> "%WINDIR%\Setup\Scripts\kvatyrka.done"'
    $lines += 'rd /s /q "%PKG%" 2>nul'
    $text = ($lines -join "`r`n") + "`r`n"
    [IO.File]::WriteAllText((Join-Path $dir 'kvatyrka-firstrun.cmd'), $text, (New-Object Text.UTF8Encoding $false))
    [IO.File]::WriteAllText((Join-Path $dir 'SetupComplete.cmd'), "@call `"%WINDIR%\Setup\Scripts\kvatyrka-firstrun.cmd`"`r`n", [Text.Encoding]::ASCII)
    # Якщо Windows пропустить SetupComplete (буває з OEM-ключем), при першому вході скрипт попросить права адміністратора й виконається.
    $fallback = 'cmd.exe /c if not exist "%WINDIR%\Setup\Scripts\kvatyrka.done" powershell.exe -NoProfile -WindowStyle Hidden -Command "Start-Process -Verb RunAs -FilePath $env:WINDIR\Setup\Scripts\kvatyrka-firstrun.cmd"'
    Set-Reg 'KV_SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce' 'KvatyrkaFirstRun' $fallback 'ExpandString'
}

# ---------- ISO ----------
function New-IsoImage([string]$Source, [string]$Path, [string]$Label) {
    if (-not ('KvatyrkaIso' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
public static class KvatyrkaIso {
    public static void Save(object stream, string path, int blockSize, int blocks) {
        IStream s = (IStream)stream;
        byte[] buf = new byte[blockSize * 256];
        IntPtr read = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
        try {
            using (FileStream f = File.Create(path)) {
                long total = (long)blockSize * blocks;
                while (total > 0) {
                    s.Read(buf, buf.Length, read);
                    int n = System.Runtime.InteropServices.Marshal.ReadInt32(read);
                    if (n <= 0) break;
                    f.Write(buf, 0, n);
                    total -= n;
                }
                if (total > 0) throw new IOException("Потік ISO закінчився раніше: бракує " + total + " байт");
            }
        } finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(read); }
    }
}
'@
    }
    $boot = @()
    foreach ($b in @(@{ File = 'boot\etfsboot.com'; Platform = 0 }, @{ File = 'efi\microsoft\boot\efisys_noprompt.bin'; Platform = 0xEF })) {
        $file = Join-Path $Source $b.File
        if (-not (Test-Path $file)) { continue }
        $stream = New-Object -ComObject ADODB.Stream
        $stream.Open(); $stream.Type = 1; $stream.LoadFromFile($file)
        $opt = New-Object -ComObject IMAPI2FS.BootOptions
        $opt.AssignBootImage($stream)
        $opt.PlatformId = $b.Platform
        $opt.Emulation = 0
        $boot += $opt
    }
    if ($boot.Count -eq 0) { Fail 'У джерелі немає завантажувальних файлів Windows' }
    $fsi = New-Object -ComObject IMAPI2FS.MsftFileSystemImage
    $fsi.ChooseImageDefaultsForMediaType(12) # диск: без обмеження розміру DVD
    $fsi.FileSystemsToCreate = 4             # UDF – файли понад 4 ГБ
    $fsi.UDFRevision = 0x102
    $fsi.VolumeName = $Label
    $fsi.BootImageOptionsArray = [object[]]$boot
    $fsi.Root.AddTree($Source, $false)
    $img = $fsi.CreateResultImage()
    [KvatyrkaIso]::Save($img.ImageStream, $Path, $img.BlockSize, $img.TotalBlocks)
}

# ---------- головне ----------
$cfg = Read-Config $Config
$Iso = (Resolve-Path $Iso).Path
$work = Join-Path $Out 'work'
$mount = Join-Path $Out 'mount'
$downloads = Join-Path $Out 'downloads'
$edition = $Editions[$cfg.edition]
if (-not $edition) { Fail "Невідома редакція «$($cfg.edition)». Можна: $($Editions.Keys -join ', ')" }
if (-not (Test-Path $Shell)) { Fail "Немає Kvatyrka.exe: $Shell. Покладіть туди Kvatyrka.exe з релізу або вкажіть -Shell – він застосовує покращення." }
$driverDir = if ($cfg.drivers) { Join-Path $PSScriptRoot $cfg.drivers } else { $null }

Write-Host "Кватирка: $($cfg.name)" -ForegroundColor White
Write-Note "Профіль: $($cfg.Path)"
Write-Note "Образ: $Iso"
Write-Note "Редакція: Windows 11 $($cfg.edition)"
Write-Note "Залишаються програми: $($cfg.keepApps -join ', ')"
Write-Note "Покращення: $(if ($cfg.tweaks.recommended) { 'рекомендовані' } else { 'лише вибрані' })$(if (@($cfg.tweaks.on).Count) { ' + ' + ($cfg.tweaks.on -join ', ') })$(if (@($cfg.tweaks.off).Count) { ' – без ' + ($cfg.tweaks.off -join ', ') })"
Write-Note "Компоненти: $(if ($cfg.packages) { ($cfg.packages | ForEach-Object name) -join ', ' } else { 'немає' })"
if ($driverDir) { Write-Note "Драйвери: $driverDir $(if (Test-Path $driverDir) { '' } else { '(папки немає – пропущу)' })" }
if ($Check) { Write-Host ''; Write-Host 'Перевірка пройдена, нічого не змінено.' -ForegroundColor Green; return }

if ((Get-PSDrive -Name ([IO.Path]::GetPathRoot($Out).TrimEnd(':\'))).Free -lt 25GB) { Fail "На диску з $Out потрібно щонайменше 25 ГБ вільного місця" }
foreach ($d in $work, $mount) { if (Test-Path $d) { Remove-Item $d -Recurse -Force } }
New-Item -ItemType Directory -Force -Path $work, $mount | Out-Null

$mounted = $false
$hivesLoaded = $false
try {
    Write-Step 'Копіюю вміст ISO'
    $disk = Mount-DiskImage -ImagePath $Iso -PassThru
    try {
        $letter = ($disk | Get-Volume).DriveLetter
        & robocopy.exe "$($letter):\" $work /E /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { Fail "Не вдалося скопіювати ISO (robocopy $LASTEXITCODE)" }
    }
    finally { Dismount-DiskImage -ImagePath $Iso | Out-Null }
    Get-ChildItem $work -Recurse | ForEach-Object { $_.Attributes = $_.Attributes -band (-bnot [IO.FileAttributes]::ReadOnly) }

    Write-Step "Залишаю лише Windows 11 $($cfg.edition)"
    $source = @('install.wim', 'install.esd') | ForEach-Object { Join-Path $work "sources\$_" } | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $source) { Fail 'В ISO немає sources\install.wim чи install.esd – це не образ Windows' }
    $index = $null
    foreach ($img in Get-WindowsImage -ImagePath $source) {
        if ((Get-WindowsImage -ImagePath $source -Index $img.ImageIndex).EditionId -eq $edition) { $index = $img.ImageIndex; break }
    }
    if (-not $index) { Fail "В образі немає редакції $($cfg.edition)" }
    $wim = Join-Path $work 'sources\install.kv.wim'
    Export-WindowsImage -SourceImagePath $source -SourceIndex $index -DestinationImagePath $wim -CompressionType max | Out-Null
    Remove-Item $source -Force
    # В образі одна редакція: ei.cfg каже інсталятору брати її, навіть якщо в прошивці ключ іншої (часто Home).
    [IO.File]::WriteAllText((Join-Path $work 'sources\ei.cfg'), "[EditionID]`r`n$edition`r`n[Channel]`r`nRetail`r`n", [Text.Encoding]::ASCII)

    Write-Step 'Відкриваю образ'
    Mount-WindowsImage -ImagePath $wim -Index 1 -Path $mount | Out-Null
    $mounted = $true

    Write-Step 'Прибираю непотрібні програми'
    foreach ($app in Get-AppxProvisionedPackage -Path $mount) {
        $keep = $false
        foreach ($pattern in $cfg.keepApps) { if ($app.DisplayName -like $pattern) { $keep = $true; break } }
        if ($keep) { continue }
        Write-Note "– $($app.DisplayName)"
        Remove-AppxProvisionedPackage -Path $mount -PackageName $app.PackageName | Out-Null
    }
    if ($cfg.image.removeOneDrive) {
        foreach ($f in 'Windows\System32\OneDriveSetup.exe', 'Windows\SysWOW64\OneDriveSetup.exe') {
            $p = Join-Path $mount $f
            if (Test-Path $p) { & takeown.exe /f $p | Out-Null; & icacls.exe $p /grant '*S-1-5-32-544:F' | Out-Null; Remove-Item $p -Force; Write-Note '– OneDrive' }
        }
    }
    if (-not $cfg.image.recoveryEnvironment) {
        $re = Join-Path $mount 'Windows\System32\Recovery\Winre.wim'
        if (Test-Path $re) { Remove-Item $re -Force; Write-Note '– середовище відновлення' }
    }

    if ($driverDir -and (Test-Path $driverDir) -and (Get-ChildItem $driverDir -Recurse -Filter *.inf | Select-Object -First 1)) {
        Write-Step 'Додаю драйвери'
        Add-WindowsDriver -Path $mount -Driver $driverDir -Recurse | Out-Null
        Write-Note "+ $((Get-WindowsDriver -Path $mount | Measure-Object).Count) сторонніх драйверів в образі"
    }

    Write-Step 'Налаштовую систему'
    $hivesLoaded = $true
    Mount-Hives $mount
    Set-ImageOnly $cfg
    $commands = Invoke-Tweaks $cfg $Shell (Join-Path $Out 'tweaks')

    if ($cfg.wallpaper) {
        # Шпалери за замовчуванням: замінюємо файли світлої й темної тем Windows і типовий екран блокування.
        $src = Join-Path $PSScriptRoot $cfg.wallpaper
        if (-not (Test-Path $src)) { Fail "Немає файла шпалер: $src" }
        $targets = @(Get-ChildItem (Join-Path $mount 'Windows\Web') -Recurse -Include 'img0*.jpg', 'img19*.jpg', 'img100.jpg')
        foreach ($f in $targets) {
            & takeown.exe /f $f.FullName | Out-Null
            & icacls.exe $f.FullName /grant '*S-1-5-32-544:F' | Out-Null
            Copy-Item $src $f.FullName -Force
        }
        Write-Note 'Шпалери Кватирки встановлено'
    }

    if ($cfg.kvatyrka) {
        $dest = Join-Path $mount 'Program Files\Kvatyrka'
        New-Item -ItemType Directory -Force -Path $dest | Out-Null
        Copy-Item $Shell (Join-Path $dest 'Kvatyrka.exe') -Force
        Set-Reg 'KV_USER\Software\Microsoft\Windows\CurrentVersion\Run' 'Kvatyrka' '"C:\Program Files\Kvatyrka\Kvatyrka.exe" --startup' 'String'
        Write-Note 'Кватирка з охоронцем запускатиметься з Windows'
    }
    else { Write-Note 'Без Kvatyrka.exe в образі: частина покращень Провідника не застосується, охоронця не буде' }

    Write-Step 'Готую перший запуск'
    $packages = @(Get-Packages $cfg $downloads)
    $pkgDir = Join-Path $mount 'Windows\Setup\Scripts\packages'
    New-Item -ItemType Directory -Force -Path $pkgDir | Out-Null
    foreach ($p in $packages) { Copy-Item (Join-Path $downloads $p.File) $pkgDir -Force; Write-Note "+ $($p.Name)" }
    Write-FirstRun $cfg $packages $commands $mount

    Dismount-Hives
    $hivesLoaded = $false

    Write-Step 'Стискаю компоненти (кілька хвилин)'
    & dism.exe /Image:$mount /Cleanup-Image /StartComponentCleanup /ResetBase | Out-Null
    # Невдале стискання не псує образ – лише лишає його більшим.
    if ($LASTEXITCODE -ne 0) { Write-Warning "DISM не стиснув компоненти (код $LASTEXITCODE), образ буде більшим" }

    Write-Step 'Зберігаю образ'
    Dismount-WindowsImage -Path $mount -Save | Out-Null
    $mounted = $false
    $final = Join-Path $work 'sources\install.wim'
    Export-WindowsImage -SourceImagePath $wim -SourceIndex 1 -DestinationImagePath $final -CompressionType max | Out-Null
    Remove-Item $wim -Force

    Write-Step 'Створюю ISO'
    $name = 'Kvatyrka-{0}-{1:yyyyMMdd}.iso' -f ([IO.Path]::GetFileNameWithoutExtension($cfg.Path)), (Get-Date)
    $isoPath = Join-Path $Out $name
    if (Test-Path $isoPath) { Remove-Item $isoPath -Force }
    New-IsoImage -Source $work -Path $isoPath -Label 'KVATYRKA'
    Remove-Item $work -Recurse -Force

    Write-Host ''
    Write-Host "Готово: $isoPath" -ForegroundColor Green
    Write-Note 'Запишіть на флешку (наприклад, Rufus) і встановлюйте як звичайний Windows.'
}
finally {
    if ($hivesLoaded) { try { Dismount-Hives } catch { Write-Warning $_ } }
    if ($mounted) { try { Dismount-WindowsImage -Path $mount -Discard | Out-Null } catch { Write-Warning "Образ лишився підключеним: $mount. Виконайте: dism /Unmount-Image /MountDir:`"$mount`" /Discard" } }
}
