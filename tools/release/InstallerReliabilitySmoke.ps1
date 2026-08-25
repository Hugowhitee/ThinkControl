param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$serviceName = 'ThinkControlService'
$pipeName = 'ThinkControl.Service.v1'
$smokeDir = Join-Path $env:TEMP 'ThinkControlInstallerReliabilitySmoke'
$updateLog = Join-Path $env:TEMP 'ThinkControlInstallerReliabilityUpdate.log'

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

    # Inno Setup owns elevation. Pre-elevating Setup from the UI can lose the
    # original desktop token and break the normal-user relaunch after updating.
    if ($updateText -match 'Verb\s*=\s*"runas"') {
        throw 'UpdateService pre-elevates Setup. Let Inno Setup own the UAC transition instead.'
    }
    if ($installerText -notmatch 'runasoriginaluser') {
        throw 'Installer no longer guarantees a normal-user ThinkControl relaunch after elevation.'
    }
    if ($installerText -notmatch 'PrivilegesRequired=admin') {
        throw 'Installer no longer owns the expected administrator transition.'
    }

    # Alpha.15.1 regression contract: after user approval the app must not disappear
    # before Setup has actually survived launch, and Setup must stage a complete,
    # verified payload before it closes the running ThinkControl UI.
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

    Write-Host '[smoke] Updater lifecycle verified: Inno owns UAC, Setup survival is checked, staging precedes app close, relaunch uses original user.'
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

function Install-SmokeCopy([string]$phase, [bool]$legacyUpdateMode = $false) {
    Write-Host "[smoke] $phase $($setup.Name) with external payload $($payload.Name)"
    $arguments = @(
        '/VERYSILENT',
        '/SUPPRESSMSGBOXES',
        '/NORESTART',
        '/SP-'
    )

    if ($legacyUpdateMode) {
        # Alpha.14/15 used this hidden updater shape. The new Setup must remain
        # compatible with it so old installs cannot end up with UAC -> close ->
        # nothing. Relaunch is disabled only on CI; source assertions above verify
        # the real updater's normal-user relaunch and handoff contract separately.
        $arguments += @('/CLOSEAPPLICATIONS', '/UPDATE=1', '/RELAUNCH=0', "/LOG=`"$updateLog`"")
    }

    $arguments += @(
        "/DIR=`"$smokeDir`"",
        "/PAYLOAD=`"$($payload.FullName)`""
    )

    $process = Start-Process -FilePath $setup.FullName -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "$phase failed with installer exit code $($process.ExitCode)." }

    $ui = Join-Path $smokeDir 'ui\ThinkControl.UI.exe'
    $serviceExe = Join-Path $smokeDir 'service\ThinkControl.Service.exe'
    $uninstaller = Join-Path $smokeDir 'unins000.exe'
    if (-not (Test-Path $ui)) { throw "UI executable missing after $phase." }
    if (-not (Test-Path $serviceExe)) { throw "Service executable missing after $phase." }
    if (-not (Test-Path $uninstaller)) { throw "Uninstaller missing after $phase." }

    if ($legacyUpdateMode) {
        if (-not (Test-Path $updateLog)) { throw 'Legacy-style update did not create its requested installer log.' }
        if ((Get-Item $updateLog).Length -lt 100) { throw 'Legacy-style update log is unexpectedly empty.' }
    }

    Wait-ServiceRunning
    Assert-ServiceIpc
}

try {
    Write-Host '[smoke] Cleaning previous installer reliability state'
    Assert-UpdaterElevationContract
    Remove-SmokeService
    Remove-Item $smokeDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $updateLog -Force -ErrorAction SilentlyContinue

    Install-SmokeCopy 'clean install'

    # Exercise the exact hidden argument family used by older in-app updaters. This
    # catches locked service binaries, broken staging/restart logic, ignored /UPDATE
    # parameters and regressions a fresh-install-only smoke cannot see.
    Install-SmokeCopy 'alpha.14-compatible in-place update path' $true

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

    Write-Host '[smoke] Deep installer + IPC + alpha.14 update compatibility + uninstall lifecycle passed'
}
finally {
    Remove-SmokeService
    Remove-Item $smokeDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $updateLog -Force -ErrorAction SilentlyContinue
}
