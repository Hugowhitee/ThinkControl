param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$serviceName = 'ThinkControlService'
$pipeName = 'ThinkControl.Service.v1'
$smokeDir = Join-Path $env:TEMP 'ThinkControlInstallerReliabilitySmoke'
$cleanInstallLog = Join-Path $env:TEMP 'ThinkControlInstallerReliabilityClean.log'
$updateLog = Join-Path $env:TEMP 'ThinkControlInstallerReliabilityUpdate.log'
$localDataDir = Join-Path $env:LOCALAPPDATA 'ThinkControl'
$commonDataDir = Join-Path $env:ProgramData 'ThinkControl'
$preferencesRegistry = 'HKCU:\Software\ThinkControl'
$runRegistry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

$setup = Get-ChildItem $ArtifactDirectory -Recurse -File -Filter 'ThinkControl-Setup-*.exe' | Select-Object -First 1
$payload = Get-ChildItem $ArtifactDirectory -Recurse -File -Filter 'ThinkControl-Payload-*.zip' | Select-Object -First 1
if ($null -eq $setup) { throw 'Installer artifact is missing.' }
if ($null -eq $payload) { throw 'Payload artifact is missing.' }

function Assert-UpdaterElevationContract {
    $updateSource = Join-Path $PWD 'src/ThinkControl.UI/Services/UpdateService.cs'
    $installerSource = Join-Path $PWD 'installer/ThinkControl.iss'
    if (-not (Test-Path $updateSource) -or -not (Test-Path $installerSource)) {
        Write-Host '[smoke] Source contract files not present; skipping elevation-source assertions.'
        return
    }

    $updateText = Get-Content $updateSource -Raw
    $installerText = Get-Content $installerSource -Raw

    if ($updateText -match 'Verb\s*=\s*"runas"') {
        throw 'UpdateService pre-elevates Setup. Let Inno Setup own the UAC transition instead.'
    }
    if ($installerText -notmatch 'runasoriginaluser') {
        throw 'Installer no longer guarantees a normal-user ThinkControl relaunch after elevation.'
    }
    if ($installerText -notmatch 'PrivilegesRequired=admin') {
        throw 'Installer no longer owns the expected administrator transition.'
    }

    if (-not $installerText.Contains('DisableDirPage=no')) {
        throw 'Installer no longer explicitly exposes the install-location page for clean installs.'
    }
    if (-not $installerText.Contains("ExistingInstall := FileExists(ExpandConstant('{app}\ui\{#UiExeName}'));")) {
        throw 'Installer update detection is not based on Inno''s resolved {app} path.'
    }
    if (-not $installerText.Contains('(PageID = wpSelectDir)')) {
        throw 'Existing-install updates no longer skip the directory page and may relocate accidentally.'
    }

    if ($updateText -notmatch 'Process\?\s+process\s*=\s*Process\.Start\(start\)') {
        throw 'UpdateService no longer verifies the spawned Setup process.'
    }
    if ($updateText -notmatch 'process\.HasExited') {
        throw 'UpdateService no longer detects an installer that dies during handoff.'
    }
    if ($updateText -notmatch 'Task\.Delay\(900') {
        throw 'UpdateService no longer gives Setup a bounded survival window before reporting handoff success.'
    }
    if ($updateText -notmatch 'ArgumentList\.Add\("/SILENT"\)') {
        throw 'User-triggered updates must use visible /SILENT Setup, not a hidden very-silent flow.'
    }
    if ($updateText -match 'Application\.Current\.Shutdown|Current\.Shutdown\(\)') {
        throw 'UpdateService must not shut ThinkControl down immediately after launching Setup.'
    }

    $prepareStart = $installerText.IndexOf('function PrepareToInstall', [System.StringComparison]::Ordinal)
    $prepareEnd = if ($prepareStart -ge 0) {
        $installerText.IndexOf('procedure CurStepChanged', $prepareStart, [System.StringComparison]::Ordinal)
    } else { -1 }
    if ($prepareStart -lt 0 -or $prepareEnd -le $prepareStart) {
        throw 'Installer PrepareToInstall lifecycle could not be located.'
    }
    $prepareText = $installerText.Substring($prepareStart, $prepareEnd - $prepareStart)
    $stageIndex = $prepareText.IndexOf('Result := StagePayload();', [System.StringComparison]::Ordinal)
    $closeIndex = $prepareText.IndexOf('CloseRunningThinkControl();', [System.StringComparison]::Ordinal)
    if ($stageIndex -lt 0 -or $closeIndex -lt 0 -or $stageIndex -ge $closeIndex) {
        throw 'Installer must completely stage the verified payload before closing the running ThinkControl UI.'
    }
    if ($installerText -notmatch 'ShouldRelaunchAfterSilentUpdate') {
        throw 'Installer no longer carries the silent-update relaunch gate.'
    }
    if ($installerText -match "taskkill\.exe'\),\s*'/IM \{#UiExeName\} /T") {
        throw 'Installer recursively kills the UI process tree, which also kills Setup because the updater launched it.'
    }
    if ($installerText -notmatch 'Name:\s*"startwithwindows"' -or
        $installerText -notmatch 'CurrentVersion\\Run' -or
        $installerText -notmatch 'ValueData:\s*"""\{app\}\\ui\\\{#UiExeName\}"" --tray"') {
        throw 'Installer no longer offers the default Start with Windows task or its tray-only Run entry.'
    }
    if ($installerText -notmatch 'Tasks:\s*not startwithwindows;\s*Flags:\s*deletevalue') {
        throw 'Installer no longer persists an explicit Start with Windows opt-out by removing the Run entry.'
    }

    foreach ($required in @(
        'Type: filesandordirs; Name: "{localappdata}\ThinkControl"',
        'Type: filesandordirs; Name: "{commonappdata}\ThinkControl"',
        'Root: HKCU; Subkey: "Software\ThinkControl"; Flags: uninsdeletekey'
    )) {
        if (-not $installerText.Contains($required)) {
            throw "Installer clean-uninstall contract is missing: $required"
        }
    }

    Write-Host '[smoke] Updater/install lifecycle verified: clean installs expose location choice, updates preserve {app}, Inno owns UAC, staging precedes non-recursive app close, relaunch uses original user, startup opt-out is explicit, and full uninstall owns ThinkControl local state.'
}

function Remove-SmokeService {
    Start-Process -FilePath "$env:SystemRoot\System32\sc.exe" -ArgumentList @('stop', $serviceName) -Wait -PassThru -WindowStyle Hidden | Out-Null
    Start-Sleep -Milliseconds 350
    Start-Process -FilePath "$env:SystemRoot\System32\sc.exe" -ArgumentList @('delete', $serviceName) -Wait -PassThru -WindowStyle Hidden | Out-Null

    $deadline = (Get-Date).AddSeconds(20)
    do {
        if ($null -eq (Get-Service -Name $serviceName -ErrorAction SilentlyContinue)) { return }
        Start-Sleep -Milliseconds 400
    } while ((Get-Date) -lt $deadline)
}

function Wait-ServiceRunning {
    $deadline = (Get-Date).AddSeconds(25)
    do {
        $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
        if ($null -ne $service) {
            $service.Refresh()
            if ($service.Status -eq [System.ServiceProcess.ServiceControllerStatus]::Running) { return }
        }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)

    throw "$serviceName did not reach Running state."
}

function Invoke-ThinkControlPipe([string]$operation) {
    $pipe = [System.IO.Pipes.NamedPipeClientStream]::new(
        '.',
        $pipeName,
        [System.IO.Pipes.PipeDirection]::InOut,
        [System.IO.Pipes.PipeOptions]::None)
    try {
        $pipe.Connect(3000)
        $utf8 = [System.Text.UTF8Encoding]::new($false)
        $writer = [System.IO.StreamWriter]::new($pipe, $utf8, 4096, $true)
        $reader = [System.IO.StreamReader]::new($pipe, $utf8, $false, 4096, $true)
        try {
            $request = @{ version = 1; operation = $operation; value = $null } | ConvertTo-Json -Compress
            $writer.WriteLine($request)
            $writer.Flush()
            $line = $reader.ReadLine()
            if ([string]::IsNullOrWhiteSpace($line)) { throw "$operation returned an empty pipe response." }
            return $line | ConvertFrom-Json
        }
        finally {
            $reader.Dispose()
            $writer.Dispose()
        }
    }
    finally {
        $pipe.Dispose()
    }
}

function Assert-ServiceIpc {
    $ping = Invoke-ThinkControlPipe 'Ping'
    if ($ping.version -ne 1 -or $ping.success -ne $true) {
        throw "Ping failed protocol/readback verification: $($ping | ConvertTo-Json -Compress)"
    }

    $status = Invoke-ThinkControlPipe 'GetStatus'
    if ($status.version -ne 1 -or $status.success -ne $true -or $null -eq $status.telemetry) {
        throw "GetStatus failed protocol/telemetry verification: $($status | ConvertTo-Json -Depth 5 -Compress)"
    }

    Write-Host '[smoke] IPC verified: Ping + GetStatus protocol v1'
}

function Show-InstallerFailureLog([string]$phase, [string]$path) {
    if (-not (Test-Path $path)) {
        Write-Host "[smoke] $phase did not create an Inno Setup log at $path"
        return
    }

    Write-Host "[smoke] ---- $phase installer log tail ----"
    Get-Content $path -Tail 140 | ForEach-Object { Write-Host $_ }
    Write-Host "[smoke] ---- end installer log tail ----"
}

function Install-SmokeCopy([string]$phase, [bool]$legacyUpdateMode = $false, [bool]$passExplicitDir = $true) {
    Write-Host "[smoke] $phase $($setup.Name) with external payload $($payload.Name)"
    $phaseLog = if ($legacyUpdateMode) { $updateLog } else { $cleanInstallLog }
    Remove-Item $phaseLog -Force -ErrorAction SilentlyContinue

    $arguments = @(
        '/VERYSILENT',
        '/SUPPRESSMSGBOXES',
        '/NORESTART',
        '/SP-',
        "/LOG=`"$phaseLog`""
    )

    if ($legacyUpdateMode) {
        $arguments += @('/CLOSEAPPLICATIONS', '/UPDATE=1', '/RELAUNCH=0')
    }

    if ($passExplicitDir) {
        $arguments += "/DIR=`"$smokeDir`""
    }
    $arguments += "/PAYLOAD=`"$($payload.FullName)`""

    $process = Start-Process -FilePath $setup.FullName -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        Show-InstallerFailureLog $phase $phaseLog
        throw "$phase failed with installer exit code $($process.ExitCode)."
    }

    if (-not (Test-Path $phaseLog) -or (Get-Item $phaseLog).Length -lt 100) {
        throw "$phase did not preserve a useful installer log."
    }

    $ui = Join-Path $smokeDir 'ui\ThinkControl.UI.exe'
    $serviceExe = Join-Path $smokeDir 'service\ThinkControl.Service.exe'
    $uninstaller = Join-Path $smokeDir 'unins000.exe'
    if (-not (Test-Path $ui)) { throw "UI executable missing after $phase. The installer may have lost the remembered custom directory." }
    if (-not (Test-Path $serviceExe)) { throw "Service executable missing after $phase." }
    if (-not (Test-Path $uninstaller)) { throw "Uninstaller missing after $phase." }

    Wait-ServiceRunning
    Assert-ServiceIpc
}

function Seed-OwnedDataForUninstall {
    New-Item -ItemType Directory -Path $localDataDir -Force | Out-Null
    New-Item -ItemType Directory -Path $commonDataDir -Force | Out-Null
    Set-Content -Path (Join-Path $localDataDir 'installer-smoke.marker') -Value 'owned local user data'
    Set-Content -Path (Join-Path $commonDataDir 'installer-smoke.marker') -Value 'owned service data'
    New-Item -Path $preferencesRegistry -Force | Out-Null
    New-ItemProperty -Path $preferencesRegistry -Name 'InstallerSmoke' -Value 1 -PropertyType DWord -Force | Out-Null
}

try {
    Write-Host '[smoke] Cleaning previous installer reliability state'
    Assert-UpdaterElevationContract
    Remove-SmokeService
    Remove-Item $smokeDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $cleanInstallLog -Force -ErrorAction SilentlyContinue
    Remove-Item $updateLog -Force -ErrorAction SilentlyContinue
    Remove-Item $localDataDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $commonDataDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $preferencesRegistry -Recurse -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $runRegistry -Name 'ThinkControl' -ErrorAction SilentlyContinue

    Install-SmokeCopy 'clean install' $false $true

    Install-SmokeCopy 'alpha.14-compatible in-place update path' $true $false

    $defaultProgramFilesInstall = Join-Path $env:ProgramFiles 'ThinkControl\ui\ThinkControl.UI.exe'
    if ((Resolve-Path $smokeDir).Path -ne (Join-Path $env:ProgramFiles 'ThinkControl') -and (Test-Path $defaultProgramFilesInstall)) {
        throw 'Update created a second Program Files installation instead of preserving the existing custom location.'
    }

    Seed-OwnedDataForUninstall

    $uninstaller = Join-Path $smokeDir 'unins000.exe'
    Write-Host '[smoke] Uninstalling verified in-place installation'
    $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART'
    ) -Wait -PassThru
    if ($uninstall.ExitCode -ne 0) { throw "Silent uninstall failed with exit code $($uninstall.ExitCode)." }

    $deadline = (Get-Date).AddSeconds(20)
    do {
        $serviceAfter = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
        if ($null -eq $serviceAfter) { break }
        Start-Sleep -Milliseconds 400
    } while ((Get-Date) -lt $deadline)

    if ($null -ne $serviceAfter) { throw "$serviceName remained registered after uninstall." }
    if (Test-Path (Join-Path $smokeDir 'ui\ThinkControl.UI.exe')) { throw 'UI executable remained after uninstall.' }
    if (Test-Path (Join-Path $smokeDir 'service\ThinkControl.Service.exe')) { throw 'Service executable remained after uninstall.' }
    if (Test-Path $localDataDir) { throw '%LOCALAPPDATA%\ThinkControl remained after full uninstall.' }
    if (Test-Path $commonDataDir) { throw '%PROGRAMDATA%\ThinkControl remained after full uninstall.' }
    if (Test-Path $preferencesRegistry) { throw 'HKCU\Software\ThinkControl remained after full uninstall.' }

    $runEntry = Get-ItemProperty -Path $runRegistry -Name 'ThinkControl' -ErrorAction SilentlyContinue
    if ($null -ne $runEntry -and $null -ne $runEntry.ThinkControl) {
        throw 'ThinkControl startup Run entry remained after uninstall.'
    }

    Write-Host '[smoke] Deep installer + custom-location persistence + IPC + alpha.14 update compatibility + clean uninstall lifecycle passed'
}
finally {
    Remove-SmokeService
    Remove-Item $smokeDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $cleanInstallLog -Force -ErrorAction SilentlyContinue
    Remove-Item $updateLog -Force -ErrorAction SilentlyContinue
    Remove-Item $localDataDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $commonDataDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $preferencesRegistry -Recurse -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $runRegistry -Name 'ThinkControl' -ErrorAction SilentlyContinue
}
