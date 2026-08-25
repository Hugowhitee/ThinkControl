param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$serviceName = 'ThinkControlService'
$pipeName = 'ThinkControl.Service.v1'
$smokeDir = Join-Path $env:TEMP 'ThinkControlInstallerReliabilitySmoke'

$setup = Get-ChildItem $ArtifactDirectory -Recurse -File -Filter 'ThinkControl-Setup-*.exe' | Select-Object -First 1
$payload = Get-ChildItem $ArtifactDirectory -Recurse -File -Filter 'ThinkControl-Payload-*.zip' | Select-Object -First 1
if ($null -eq $setup) { throw 'Installer artifact is missing.' }
if ($null -eq $payload) { throw 'Payload artifact is missing.' }

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

function Install-SmokeCopy([string]$phase) {
    Write-Host "[smoke] $phase $($setup.Name) with external payload $($payload.Name)"
    $process = Start-Process -FilePath $setup.FullName -ArgumentList @(
        '/VERYSILENT',
        '/SUPPRESSMSGBOXES',
        '/NORESTART',
        '/SP-',
        "/DIR=`"$smokeDir`"",
        "/PAYLOAD=`"$($payload.FullName)`""
    ) -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "$phase failed with installer exit code $($process.ExitCode)." }

    $ui = Join-Path $smokeDir 'ui\ThinkControl.UI.exe'
    $serviceExe = Join-Path $smokeDir 'service\ThinkControl.Service.exe'
    $uninstaller = Join-Path $smokeDir 'unins000.exe'
    if (-not (Test-Path $ui)) { throw "UI executable missing after $phase." }
    if (-not (Test-Path $serviceExe)) { throw "Service executable missing after $phase." }
    if (-not (Test-Path $uninstaller)) { throw "Uninstaller missing after $phase." }

    Wait-ServiceRunning
    Assert-ServiceIpc
}

try {
    Write-Host '[smoke] Cleaning previous installer reliability state'
    Remove-SmokeService
    Remove-Item $smokeDir -Recurse -Force -ErrorAction SilentlyContinue

    Install-SmokeCopy 'clean install'

    # A second install into the occupied directory exercises the same service/file
    # replacement path used by ThinkControl's updater without needing a mutable old
    # release. This catches locked service binaries, broken restart logic and pipe ACL
    # regressions that a fresh-install-only smoke cannot see.
    Install-SmokeCopy 'in-place reinstall/update path'

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

    Write-Host '[smoke] Deep installer + service IPC + in-place update lifecycle passed'
}
finally {
    Remove-SmokeService
    Remove-Item $smokeDir -Recurse -Force -ErrorAction SilentlyContinue
}
