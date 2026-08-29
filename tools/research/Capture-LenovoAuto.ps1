param(
    [ValidateRange(20, 600)]
    [int]$DurationSeconds = 90,

    [ValidateRange(1, 10)]
    [int]$SampleIntervalSeconds = 2,

    [string]$Label = "lenovo-auto",

    [string]$OutputPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# This script is deliberately observational. It does not write EC registers,
# Lenovo registry values, power modes, services, ACPI methods, IOCTLs or firmware.
# The optional ThinkControl pipe request is GetStatus only; it cannot submit a raw
# hardware command through the service protocol.

$pipeName = "ThinkControl.Service.v1"
$litsRoot = "HKLM:\SYSTEM\CurrentControlSet\Services\LITSSVC\IC"
$started = [DateTimeOffset]::Now

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $safeLabel = ($Label -replace '[^A-Za-z0-9._-]', '_')
    $desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
    if ([string]::IsNullOrWhiteSpace($desktop)) {
        $desktop = $PWD.Path
    }
    $OutputPath = Join-Path $desktop ("ThinkControl-LenovoAuto-{0}-{1}.json" -f $safeLabel, $started.ToString("yyyyMMdd-HHmmss"))
}

function Get-ObjectPropertyString {
    param([object]$InputObject, [string]$Name)
    if ($null -eq $InputObject) { return "" }
    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return "" }
    return [string]$property.Value
}

function Convert-SafeRegistryValue {
    param([object]$Value)

    if ($null -eq $Value) { return $null }
    if ($Value -is [byte[]]) {
        $bytes = [byte[]]$Value
        if ($bytes.Length -eq 0) { return "" }
        $take = [Math]::Min($bytes.Length, 64)
        $hex = ([BitConverter]::ToString($bytes, 0, $take)).Replace("-", "")
        if ($bytes.Length -gt $take) { $hex += "..." }
        return $hex
    }
    if ($Value -is [Array]) {
        return @($Value | Select-Object -First 32 | ForEach-Object { [string]$_ })
    }

    $text = [string]$Value
    $home = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    if (-not [string]::IsNullOrWhiteSpace($home)) {
        $text = $text.Replace($home, "[user]")
    }
    $text = $text.Replace([Environment]::UserName, "[redacted]")
    $text = $text.Replace([Environment]::MachineName, "[redacted]")
    if ($text.Length -gt 240) { $text = $text.Substring(0, 240) + "..." }
    return $text
}

function Get-RegistryKeyValues {
    param([string]$Path)

    if (-not (Test-Path $Path)) { return $null }
    try {
        $item = Get-ItemProperty -Path $Path -ErrorAction Stop
        $values = [ordered]@{}
        foreach ($property in $item.PSObject.Properties) {
            if ($property.Name -like "PS*") { continue }
            $values[$property.Name] = Convert-SafeRegistryValue $property.Value
        }
        return [pscustomobject]$values
    }
    catch {
        return [pscustomobject]@{ error = $_.Exception.GetType().Name }
    }
}

function Get-LitsSnapshot {
    $result = [ordered]@{}
    if (-not (Test-Path $litsRoot)) {
        return [pscustomobject]@{ available = $false }
    }

    $result["available"] = $true
    $result["IC"] = $(Get-RegistryKeyValues $litsRoot)
    try {
        foreach ($key in Get-ChildItem -Path $litsRoot -ErrorAction Stop | Sort-Object PSChildName) {
            $result[$key.PSChildName] = $(Get-RegistryKeyValues $key.PSPath)
        }
    }
    catch {
        $result["enumerationError"] = $_.Exception.GetType().Name
    }
    return [pscustomobject]$result
}

function Get-ThinkControlStatus {
    $pipe = $null
    $writer = $null
    $reader = $null
    try {
        $pipeArguments = @(
            ".",
            $pipeName,
            [System.IO.Pipes.PipeDirection]::InOut,
            [System.IO.Pipes.PipeOptions]::None
        )
        $pipe = New-Object System.IO.Pipes.NamedPipeClientStream -ArgumentList $pipeArguments
        $pipe.Connect(900)
        $utf8 = New-Object System.Text.UTF8Encoding -ArgumentList $false
        $writer = New-Object System.IO.StreamWriter -ArgumentList @($pipe, $utf8, 4096, $true)
        $reader = New-Object System.IO.StreamReader -ArgumentList @($pipe, $utf8, $false, 8192, $true)
        $writer.AutoFlush = $true
        $writer.WriteLine('{"version":1,"operation":"GetStatus","value":null}')
        $line = $reader.ReadLine()
        if ([string]::IsNullOrWhiteSpace($line)) { return $null }
        $response = $line | ConvertFrom-Json
        if (-not $response.success -or $null -eq $response.telemetry) {
            return [pscustomobject]@{ online = $true; success = $false; error = [string]$response.error }
        }

        $telemetry = $response.telemetry
        $fans = @()
        if ($null -ne $telemetry.fans) {
            $fans = @($telemetry.fans | ForEach-Object {
                [pscustomobject]@{
                    id = [string]$_.id
                    label = [string]$_.label
                    rpm = [int]$_.rpm
                    source = [string]$_.source
                }
            })
        }

        return [pscustomobject]@{
            online = $true
            success = $true
            coolingProfile = [string]$telemetry.coolingProfile
            coolingAppliedLevel = $telemetry.coolingAppliedLevel
            coolingAppliedPercent = $telemetry.coolingAppliedPercent
            controlTemperatureC = $telemetry.controlTemperatureC
            fanState = [string]$telemetry.fanState
            primaryFanRpm = $telemetry.fanRpm
            fanRpmSource = [string]$telemetry.fanRpmSource
            fans = $fans
        }
    }
    catch {
        return [pscustomobject]@{ online = $false; success = $false; error = $_.Exception.GetType().Name }
    }
    finally {
        if ($null -ne $reader) { $reader.Dispose() }
        if ($null -ne $writer) { $writer.Dispose() }
        if ($null -ne $pipe) { $pipe.Dispose() }
    }
}

function Get-PowerSnapshot {
    $activeScheme = "Unavailable"
    try {
        $output = & "$env:SystemRoot\System32\powercfg.exe" /GETACTIVESCHEME 2>$null
        if ($LASTEXITCODE -eq 0 -and $null -ne $output) {
            $activeScheme = (($output | Out-String).Trim() -replace '\s+', ' ')
        }
    }
    catch { }

    return [pscustomobject]@{
        activeScheme = $activeScheme
        acLineStatus = [System.Windows.Forms.SystemInformation]::PowerStatus.PowerLineStatus.ToString()
    }
}

Add-Type -AssemblyName System.Windows.Forms

$computer = Get-CimInstance Win32_ComputerSystem -ErrorAction SilentlyContinue
$bios = Get-CimInstance Win32_BIOS -ErrorAction SilentlyContinue
$litsService = Get-CimInstance Win32_Service -Filter "Name='LITSSVC'" -ErrorAction SilentlyContinue
$litsVersion = $null
$litsPath = Get-ObjectPropertyString $litsService "PathName"
if (-not [string]::IsNullOrWhiteSpace($litsPath)) {
    try {
        $exe = $litsPath.Trim('"').Split('"')[0]
        if (Test-Path $exe) { $litsVersion = (Get-Item $exe).VersionInfo.FileVersion }
    }
    catch { }
}

$meta = [pscustomobject]@{
    schemaVersion = 1
    captureLabel = $Label
    startedLocal = $started.ToString("o")
    durationSeconds = $DurationSeconds
    sampleIntervalSeconds = $SampleIntervalSeconds
    readOnly = $true
    manufacturer = $(Get-ObjectPropertyString $computer "Manufacturer")
    model = $(Get-ObjectPropertyString $computer "Model")
    biosVersion = $(Get-ObjectPropertyString $bios "SMBIOSBIOSVersion")
    litsServiceState = $(Get-ObjectPropertyString $litsService "State")
    litsServiceVersion = $litsVersion
    powerAtStart = $(Get-PowerSnapshot)
    note = "Observational capture only. No EC/register/power/service/firmware writes are performed by this script."
}

$samples = New-Object 'System.Collections.Generic.List[object]'
$deadline = [DateTimeOffset]::Now.AddSeconds($DurationSeconds)
Write-Host "Capturing Lenovo Auto evidence for $DurationSeconds seconds..."
Write-Host "Do not change fan settings just for the script. Reproduce the state you want to compare naturally."
Write-Host "Output: $OutputPath"

while ([DateTimeOffset]::Now -lt $deadline) {
    $sampleStarted = [DateTimeOffset]::Now
    $samples.Add([pscustomobject]@{
        timestampLocal = $sampleStarted.ToString("o")
        thinkControl = $(Get-ThinkControlStatus)
        lits = $(Get-LitsSnapshot)
    })

    $remaining = $SampleIntervalSeconds - ([DateTimeOffset]::Now - $sampleStarted).TotalSeconds
    if ($remaining -gt 0) {
        Start-Sleep -Milliseconds ([int][Math]::Round($remaining * 1000))
    }
}

$document = [pscustomobject]@{
    meta = $meta
    powerAtEnd = $(Get-PowerSnapshot)
    samples = $samples
}

$folder = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($folder)) {
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
}
$document | ConvertTo-Json -Depth 30 | Set-Content -Path $OutputPath -Encoding UTF8

Write-Host ""
Write-Host "Capture complete: $OutputPath"
Write-Host "You can review the JSON before sharing it."
