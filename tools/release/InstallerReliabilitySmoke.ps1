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

    # Inno Setup must own elevation. If the app pre-elevates Setup with Verb=runas,
    # Inno can lose the original unelevated desktop token and relaunch ThinkControl
    # elevated, which breaks PowerToys/FancyZones/AlwaysOnTop interaction.
    if ($updateText -match 'Verb\s*=\s*"runas"') {
        throw 'UpdateService pre-elevates Setup. Let Inno Setup own the UAC transition instead.'
    }
    if ($installerText -notmatch 'runasoriginaluser') {
        throw 'Installer no longer guarantees a normal-user ThinkControl relaunch after elevation.'
    }
    if ($installerText -notmatch 'PrivilegesRequired=admin') {
        throw 'Installer no longer owns the expected administrator transition.'
    }

    Write-Host '[smoke] Updater elevation contract verified: Inno owns UAC + app relaunches as original user.'
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

    Write-Host "[smoke] IPC verified: Ping + GetStatus protocol v1"
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
        # Alpha.14 used this hidden updater shape. The new Setup must remain compatible
        # with it so users upgrading from that build cannot end up with "UAC → close →
        # nothing" just because the launcher itself is old. Relaunch is disabled only
        # for CI so a desktop window cannot make the runner flaky; the source assertion
        # above separately guarantees runasoriginaluser for real /RELAUNCH=1 updates.
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

    # Exercise the exact hidden argument family used by alpha.14's in-app updater.
    # This catches locked service binaries, broken staging/restart logic, ignored
    # /UPDATE parameters and regressions that a fresh-install-only smoke cannot see.
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
