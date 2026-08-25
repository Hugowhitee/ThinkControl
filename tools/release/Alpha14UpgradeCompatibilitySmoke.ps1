param(
    [Parameter(Mandatory = $true)]
    [string]$CandidateArtifactDirectory,

    [Parameter(Mandatory = $true)]
    [string]$LegacyArtifactDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$serviceName = 'ThinkControlService'
$pipeName = 'ThinkControl.Service.v1'
$installDir = Join-Path $env:TEMP 'ThinkControlAlpha14UpgradeSmoke'
$updateLog = Join-Path $env:TEMP 'ThinkControlAlpha14UpgradeSmoke.log'

$legacySetup = Get-ChildItem $LegacyArtifactDirectory -File -Filter 'ThinkControl-Setup-0.1.0-alpha.14.1.exe' | Select-Object -First 1
$legacyPayload = Get-ChildItem $LegacyArtifactDirectory -File -Filter 'ThinkControl-Payload-0.1.0-alpha.14.1.zip' | Select-Object -First 1
$candidateSetup = Get-ChildItem $CandidateArtifactDirectory -File -Filter 'ThinkControl-Setup-*.exe' | Select-Object -First 1
$candidatePayload = Get-ChildItem $CandidateArtifactDirectory -File -Filter 'ThinkControl-Payload-*.zip' | Select-Object -First 1

if ($null -eq $legacySetup -or $null -eq $legacyPayload) { throw 'Published alpha.14.1 fixture is incomplete.' }
if ($null -eq $candidateSetup -or $null -eq $candidatePayload) { throw 'Candidate installer fixture is incomplete.' }

function Stop-UiProcesses {
    Get-Process -Name 'ThinkControl.UI' -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
}

function Remove-SmokeState {
    Stop-UiProcesses
    Start-Process -FilePath "$env:SystemRoot\System32\sc.exe" -ArgumentList @('stop', $serviceName) -Wait -PassThru -WindowStyle Hidden | Out-Null
    Start-Sleep -Milliseconds 350
    Start-Process -FilePath "$env:SystemRoot\System32\sc.exe" -ArgumentList @('delete', $serviceName) -Wait -PassThru -WindowStyle Hidden | Out-Null
    Start-Sleep -Milliseconds 350
    Remove-Item $installDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $updateLog -Force -ErrorAction SilentlyContinue
}

function Start-AndWait([string]$file, [string[]]$arguments, [int]$timeoutSeconds, [string]$label) {
    $process = Start-Process -FilePath $file -ArgumentList $arguments -PassThru
    if (-not $process.WaitForExit($timeoutSeconds * 1000)) {
        try { $process.Kill($true) } catch { }
        throw "$label did not exit within $timeoutSeconds seconds."
    }
    return $process
}

function Wait-ServiceRunning {
    $deadline = (Get-Date).AddSeconds(30)
    do {
        $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
        if ($null -ne $service) {
            $service.Refresh()
            if ($service.Status -eq [System.ServiceProcess.ServiceControllerStatus]::Running) { return }
        }
        Start-Sleep -Milliseconds 400
    } while ((Get-Date) -lt $deadline)
    throw "$serviceName did not reach Running state."
}

function Invoke-ThinkControlPipe([string]$operation) {
    $pipe = [System.IO.Pipes.NamedPipeClientStream]::new('.', $pipeName, [System.IO.Pipes.PipeDirection]::InOut)
    try {
        $pipe.Connect(3500)
        $utf8 = [System.Text.UTF8Encoding]::new($false)
        $writer = [System.IO.StreamWriter]::new($pipe, $utf8, 4096, $true)
        $reader = [System.IO.StreamReader]::new($pipe, $utf8, $false, 4096, $true)
        try {
            $writer.WriteLine((@{ version = 1; operation = $operation; value = $null } | ConvertTo-Json -Compress))
            $writer.Flush()
            $line = $reader.ReadLine()
            if ([string]::IsNullOrWhiteSpace($line)) { throw "$operation returned an empty response." }
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

function Assert-Ipc {
    $ping = Invoke-ThinkControlPipe 'Ping'
    if ($ping.version -ne 1 -or $ping.success -ne $true) { throw 'ThinkControl Ping failed after upgrade.' }
    $status = Invoke-ThinkControlPipe 'GetStatus'
    if ($status.version -ne 1 -or $status.success -ne $true -or $null -eq $status.telemetry) {
        throw 'ThinkControl GetStatus telemetry failed after upgrade.'
    }
}

try {
    Remove-SmokeState

    Write-Host '[alpha14-upgrade] Installing the real immutable alpha.14.1 release fixture'
    $legacy = Start-AndWait $legacySetup.FullName @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-',
        "/DIR=`"$installDir`"",
        "/PAYLOAD=`"$($legacyPayload.FullName)`""
    ) 90 'alpha.14.1 fixture installer'
    if ($legacy.ExitCode -ne 0) { throw "alpha.14.1 fixture install failed with exit code $($legacy.ExitCode)." }

    $legacyUi = Join-Path $installDir 'ui\ThinkControl.UI.exe'
    $legacyService = Join-Path $installDir 'service\ThinkControl.Service.exe'
    if (-not (Test-Path $legacyUi) -or -not (Test-Path $legacyService)) { throw 'alpha.14.1 did not install the expected runtime files.' }
    Wait-ServiceRunning
    $legacyUiHash = (Get-FileHash $legacyUi -Algorithm SHA256).Hash
    $legacyServiceHash = (Get-FileHash $legacyService -Algorithm SHA256).Hash

    Write-Host '[alpha14-upgrade] Invoking candidate with the exact hidden flags used by alpha.14.1'
    $candidate = Start-AndWait $candidateSetup.FullName @(
        '/VERYSILENT',
        '/SUPPRESSMSGBOXES',
        '/NORESTART',
        '/CLOSEAPPLICATIONS',
        '/UPDATE=1',
        '/RELAUNCH=1',
        "/PAYLOAD=`"$($candidatePayload.FullName)`"",
        "/LOG=`"$updateLog`""
    ) 90 'alpha.14.1-compatible candidate updater'
    if ($candidate.ExitCode -ne 0) {
        $tail = if (Test-Path $updateLog) { (Get-Content $updateLog -Tail 40) -join [Environment]::NewLine } else { '<no installer log>' }
        throw "alpha.14.1-compatible candidate update exited with code $($candidate.ExitCode).`n$tail"
    }

    Wait-ServiceRunning
    Assert-Ipc

    $updatedUi = Join-Path $installDir 'ui\ThinkControl.UI.exe'
    $updatedService = Join-Path $installDir 'service\ThinkControl.Service.exe'
    if (-not (Test-Path $updatedUi) -or -not (Test-Path $updatedService)) { throw 'Candidate update removed required runtime files.' }

    $newUiHash = (Get-FileHash $updatedUi -Algorithm SHA256).Hash
    $newServiceHash = (Get-FileHash $updatedService -Algorithm SHA256).Hash
    if ($newUiHash -eq $legacyUiHash) { throw 'UI binary did not change; the update silently left alpha.14.1 installed.' }
    if ($newServiceHash -eq $legacyServiceHash) { throw 'Service binary did not change; the update silently left alpha.14.1 installed.' }

    $productVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($updatedUi).ProductVersion
    if ([string]::IsNullOrWhiteSpace($productVersion) -or $productVersion -notlike "$ExpectedVersion*") {
        throw "Updated UI reports '$productVersion' instead of expected '$ExpectedVersion'."
    }

    if (-not (Test-Path $updateLog)) { throw 'Legacy-compatible update did not preserve an installer log.' }
    $logText = Get-Content $updateLog -Raw
    if ($logText.Length -lt 200) { throw 'Legacy-compatible update log is unexpectedly empty or truncated.' }

    Write-Host "[alpha14-upgrade] PASS: real alpha.14.1 -> $ExpectedVersion swapped UI/service and restored IPC"
}
finally {
    Stop-UiProcesses
    $uninstaller = Join-Path $installDir 'unins000.exe'
    if (Test-Path $uninstaller) {
        try {
            $cleanup = Start-AndWait $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') 45 'cleanup uninstaller'
            if ($cleanup.ExitCode -ne 0) { Write-Warning "Cleanup uninstaller returned $($cleanup.ExitCode)." }
        } catch { Write-Warning $_ }
    }
    Remove-SmokeState
}
