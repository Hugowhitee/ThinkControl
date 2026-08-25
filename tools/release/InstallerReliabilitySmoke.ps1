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

    if ($updateText -match 'Verb\s*=\s*"runas"') {
        throw 'UpdateService pre-elevates Setup. Let Inno Setup own the UAC transition instead.'
    }
    if ($installerText -notmatch 'runasoriginaluser') {
        throw 'Installer no longer guarantees a normal-user ThinkControl relaunch after elevation.'
    }
    if ($installerText -notmatch 'PrivilegesRequired=admin') {
        throw 'Installer no longer owns the expected administrator transition.'
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

    Write-Host '[smoke] Updater handoff verified: Inno owns UAC, Setup survival is checked, staging precedes app close, relaunch uses original user.'
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
            $writer.Dispose()
            $reader.Dispose()
        }
    }
    finally { $pipe.Dispose() }
}

function Wait-PipeReady {
    $deadline = (Get-Date).AddSeconds(20)
    do {
        try {
            $response = Invoke-ThinkControlPipe 'Ping'
            if ($response.success -eq $true) { return }
        }
        catch { }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)

    throw 'ThinkControl service pipe did not become ready.'
}

function Assert-NoUiProcess {
    Get-Process -Name 'ThinkControl.UI' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300
}

function Uninstall-SmokeInstall {
    $uninstaller = Join-Path $smokeDir 'unins000.exe'
    if (Test-Path $uninstaller) {
        $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -Wait -PassThru
        if ($uninstall.ExitCode -ne 0) { throw "Smoke uninstall failed with exit code $($uninstall.ExitCode)." }
    }
    Remove-SmokeService
    if (Test-Path $smokeDir) { Remove-Item $smokeDir -Recurse -Force -ErrorAction SilentlyContinue }
}

try {
    Write-Host '[smoke] Cleaning previous installer reliability state'
    Assert-UpdaterElevationContract
    Uninstall-SmokeInstall
    Assert-NoUiProcess

    Write-Host '[smoke] Installing staged payload through bootstrapper'
    $install = Start-Process -FilePath $setup.FullName -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART',
        "/DIR=$smokeDir", "/PAYLOAD=$($payload.FullName)", '/RELAUNCH=0'
    ) -Wait -PassThru
    if ($install.ExitCode -ne 0) { throw "Smoke install failed with exit code $($install.ExitCode)." }

    $uiExe = Join-Path $smokeDir 'ui\ThinkControl.UI.exe'
    $serviceExe = Join-Path $smokeDir 'service\ThinkControl.Service.exe'
    if (-not (Test-Path $uiExe)) { throw 'Installed UI executable is missing.' }
    if (-not (Test-Path $serviceExe)) { throw 'Installed service executable is missing.' }

    Wait-ServiceRunning
    Wait-PipeReady

    $ping = Invoke-ThinkControlPipe 'Ping'
    if ($ping.success -ne $true) { throw 'Ping did not return success.' }
    $status = Invoke-ThinkControlPipe 'GetStatus'
    if ($status.success -ne $true) { throw 'GetStatus did not return success.' }

    Write-Host '[smoke] Verifying in-place update while service is installed'
    $update = Start-Process -FilePath $setup.FullName -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/UPDATE',
        "/DIR=$smokeDir", "/PAYLOAD=$($payload.FullName)", '/RELAUNCH=0'
    ) -Wait -PassThru
    if ($update.ExitCode -ne 0) { throw "Smoke update failed with exit code $($update.ExitCode)." }

    Wait-ServiceRunning
    Wait-PipeReady
    $postUpdate = Invoke-ThinkControlPipe 'GetStatus'
    if ($postUpdate.success -ne $true) { throw 'Post-update GetStatus did not return success.' }

    Write-Host '[smoke] Verifying update can replace a running UI without pre-closing it'
    $ui = Start-Process -FilePath $uiExe -PassThru
    Start-Sleep -Seconds 2
    if ($ui.HasExited) { throw 'Smoke UI exited before update lifecycle validation.' }

    $runningUpdate = Start-Process -FilePath $setup.FullName -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/UPDATE',
        "/DIR=$smokeDir", "/PAYLOAD=$($payload.FullName)", '/RELAUNCH=0'
    ) -Wait -PassThru
    if ($runningUpdate.ExitCode -ne 0) { throw "Running-UI update failed with exit code $($runningUpdate.ExitCode)." }

    Wait-ServiceRunning
    Wait-PipeReady
    if (-not $ui.HasExited) { throw 'Installer did not release the running ThinkControl UI during the staged swap.' }

    Write-Host '[smoke] Installer and service lifecycle passed'
}
finally {
    Assert-NoUiProcess
    Uninstall-SmokeInstall
}
