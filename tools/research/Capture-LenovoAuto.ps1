param(
    [ValidateRange(20, 600)]
    [int]$DurationSeconds = 90,

    [ValidateRange(1, 10)]
    [int]$SampleIntervalSeconds = 2,

    [ValidateRange(2, 30)]
    [int]$OemQueryIntervalSeconds = 4,

    [string]$Label = "lenovo-auto",

    [string]$OutputPath = "",

    [switch]$BundleRelevantOemBinaries
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# This script is deliberately observational. It does not write EC registers,
# Lenovo registry values, power modes, services, ACPI methods or firmware.
# EnergyDrv is queried only through two historical read/query contracts:
#   0x83102570 QueryFanSpeed(index)
#   0x831020C4 query fan state with input 14
# It NEVER invokes the known write contracts 0x8310257C (ChangeFanSpeed) or
# 0x831020C0 (dust-removal/high-speed write). Binary scanning below merely looks
# for those constants/strings in installed Lenovo files; it never executes them.
# The optional ThinkControl pipe request is GetStatus only and cannot submit a raw
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

function Convert-SafePath {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    $text = $Value
    $home = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    if (-not [string]::IsNullOrWhiteSpace($home)) {
        $text = $text.Replace($home, "[user]")
    }
    $text = $text.Replace([Environment]::UserName, "[redacted]")
    $text = $text.Replace([Environment]::MachineName, "[redacted]")
    return $text
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
    $text = Convert-SafePath $text
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

$probeSource = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ThinkControlResearch
{
    public sealed class EnergyDrvQueryResult
    {
        public bool Success { get; set; }
        public uint Value { get; set; }
        public uint BytesReturned { get; set; }
        public int Win32Error { get; set; }
        public string OpenAccess { get; set; }
    }

    public static class ReadOnlyEnergyDrvProbe
    {
        private const uint OpenExisting = 3;
        private const uint FileShareRead = 1;
        private const uint FileShareWrite = 2;
        private const uint GenericRead = 0x80000000;
        private const uint QueryFanSpeedIoctl = 0x83102570;
        private const uint QueryFanStateIoctl = 0x831020C4;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(
            SafeFileHandle device,
            uint ioControlCode,
            ref uint inBuffer,
            uint inBufferSize,
            out uint outBuffer,
            uint outBufferSize,
            out uint bytesReturned,
            IntPtr overlapped);

        public static EnergyDrvQueryResult QueryFanSpeed(uint index)
        {
            if (index > 1) throw new ArgumentOutOfRangeException("index");
            return Query(QueryFanSpeedIoctl, index);
        }

        public static EnergyDrvQueryResult QueryFanState()
        {
            return Query(QueryFanStateIoctl, 14u);
        }

        private static EnergyDrvQueryResult Query(uint ioctl, uint input)
        {
            SafeFileHandle handle = Open(0u);
            string access = "no-access query handle";
            if (handle == null || handle.IsInvalid)
            {
                if (handle != null) handle.Dispose();
                handle = Open(GenericRead);
                access = "GENERIC_READ";
            }

            using (handle)
            {
                if (handle == null || handle.IsInvalid)
                {
                    return new EnergyDrvQueryResult
                    {
                        Success = false,
                        Win32Error = Marshal.GetLastWin32Error(),
                        OpenAccess = access
                    };
                }

                uint output;
                uint returned;
                bool ok = DeviceIoControl(
                    handle,
                    ioctl,
                    ref input,
                    sizeof(uint),
                    out output,
                    sizeof(uint),
                    out returned,
                    IntPtr.Zero);
                return new EnergyDrvQueryResult
                {
                    Success = ok && returned >= sizeof(uint),
                    Value = output,
                    BytesReturned = returned,
                    Win32Error = ok ? 0 : Marshal.GetLastWin32Error(),
                    OpenAccess = access
                };
            }
        }

        private static SafeFileHandle Open(uint access)
        {
            return CreateFile(
                @"\\.\EnergyDrv",
                access,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                0u,
                IntPtr.Zero);
        }
    }

    public static class BinaryEvidence
    {
        public static bool ContainsUInt32LittleEndian(string path, uint value)
        {
            byte[] data = File.ReadAllBytes(path);
            byte[] needle = BitConverter.GetBytes(value);
            for (int i = 0; i <= data.Length - needle.Length; i++)
            {
                if (data[i] == needle[0] && data[i + 1] == needle[1] &&
                    data[i + 2] == needle[2] && data[i + 3] == needle[3])
                    return true;
            }
            return false;
        }

        public static string[] ExtractRelevantStrings(string path, string[] keywords, int maxResults)
        {
            byte[] data = File.ReadAllBytes(path);
            HashSet<string> results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ExtractAscii(data, keywords, maxResults, results);
            if (results.Count < maxResults)
                ExtractUtf16(data, keywords, maxResults, results);
            string[] output = new string[results.Count];
            results.CopyTo(output);
            Array.Sort(output, StringComparer.OrdinalIgnoreCase);
            return output;
        }

        private static void ExtractAscii(byte[] data, string[] keywords, int maxResults, HashSet<string> results)
        {
            StringBuilder current = new StringBuilder();
            for (int i = 0; i <= data.Length; i++)
            {
                bool printable = i < data.Length && data[i] >= 32 && data[i] <= 126;
                if (printable && current.Length < 320)
                {
                    current.Append((char)data[i]);
                    continue;
                }

                AddIfRelevant(current.ToString(), keywords, maxResults, results);
                current.Length = 0;
                if (results.Count >= maxResults) return;
            }
        }

        private static void ExtractUtf16(byte[] data, string[] keywords, int maxResults, HashSet<string> results)
        {
            StringBuilder current = new StringBuilder();
            for (int i = 0; i + 1 < data.Length; i += 2)
            {
                bool printable = data[i + 1] == 0 && data[i] >= 32 && data[i] <= 126;
                if (printable && current.Length < 320)
                {
                    current.Append((char)data[i]);
                    continue;
                }

                AddIfRelevant(current.ToString(), keywords, maxResults, results);
                current.Length = 0;
                if (results.Count >= maxResults) return;
            }
        }

        private static void AddIfRelevant(string value, string[] keywords, int maxResults, HashSet<string> results)
        {
            if (value == null || value.Length < 4 || results.Count >= maxResults) return;
            for (int i = 0; i < keywords.Length; i++)
            {
                if (value.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    results.Add(value.Length > 300 ? value.Substring(0, 300) : value);
                    return;
                }
            }
        }
    }
}
'@

try {
    Add-Type -TypeDefinition $probeSource -Language CSharp -ErrorAction Stop
}
catch {
    Write-Warning ("Read-only OEM probe helper could not compile: " + $_.Exception.Message)
}

function Convert-EnergyDrvResult {
    param([object]$Result)
    if ($null -eq $Result) { return $null }
    return [pscustomobject]@{
        success = [bool]$Result.Success
        rawValue = if ($Result.Success) { [uint32]$Result.Value } else { $null }
        bytesReturned = [uint32]$Result.BytesReturned
        win32Error = [int]$Result.Win32Error
        openAccess = [string]$Result.OpenAccess
    }
}

function Get-EnergyDrvSnapshot {
    $type = [Type]::GetType("ThinkControlResearch.ReadOnlyEnergyDrvProbe")
    if ($null -eq $type) {
        # Add-Type types are not always discoverable through Type.GetType without an
        # assembly-qualified name. Resolve through PowerShell's type accelerator next.
        try { $type = [ThinkControlResearch.ReadOnlyEnergyDrvProbe] } catch { return [pscustomobject]@{ available = $false; error = "probe-helper-unavailable" } }
    }

    try {
        $fan0 = [ThinkControlResearch.ReadOnlyEnergyDrvProbe]::QueryFanSpeed(0)
        $fan1 = [ThinkControlResearch.ReadOnlyEnergyDrvProbe]::QueryFanSpeed(1)
        $state = [ThinkControlResearch.ReadOnlyEnergyDrvProbe]::QueryFanState()
        return [pscustomobject]@{
            available = ($fan0.Success -or $fan1.Success -or $state.Success)
            capturedLocal = [DateTimeOffset]::Now.ToString("o")
            queryFanSpeedIoctl = "0x83102570"
            fan0 = $(Convert-EnergyDrvResult $fan0)
            fan1 = $(Convert-EnergyDrvResult $fan1)
            fanStateQueryIoctl = "0x831020C4"
            fanStateQueryInput = 14
            fanState = $(Convert-EnergyDrvResult $state)
            valueSemantics = "Raw OEM query values only. They are not called RPM until exact-X9 physical correlation proves the unit."
        }
    }
    catch {
        return [pscustomobject]@{
            available = $false
            capturedLocal = [DateTimeOffset]::Now.ToString("o")
            error = $_.Exception.GetType().Name
        }
    }
}

function Resolve-ExecutablePath {
    param([string]$RawPath)
    if ([string]::IsNullOrWhiteSpace($RawPath)) { return "" }
    $text = $RawPath.Trim()
    if ($text.StartsWith('"')) {
        $closing = $text.IndexOf('"', 1)
        if ($closing -gt 1) { $text = $text.Substring(1, $closing - 1) }
    }
    else {
        $match = [regex]::Match($text, '^[^ ]+\.(exe|sys|dll)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($match.Success) { $text = $match.Value }
    }
    if ($text.StartsWith("\??\")) { $text = $text.Substring(4) }
    if ($text.StartsWith("\SystemRoot\", [StringComparison]::OrdinalIgnoreCase)) {
        $text = Join-Path $env:SystemRoot $text.Substring("\SystemRoot\".Length)
    }
    return $text
}

function Get-OemBinaryCandidates {
    param([string]$LitsExecutable)

    $files = New-Object 'System.Collections.Generic.List[string]'
    if (-not [string]::IsNullOrWhiteSpace($LitsExecutable) -and (Test-Path -LiteralPath $LitsExecutable -PathType Leaf)) {
        $files.Add((Resolve-Path -LiteralPath $LitsExecutable).Path)
    }

    $energyService = Get-CimInstance Win32_SystemDriver -Filter "Name='EnergyDrv'" -ErrorAction SilentlyContinue
    $energyPath = Resolve-ExecutablePath (Get-ObjectPropertyString $energyService "PathName")
    if (-not [string]::IsNullOrWhiteSpace($energyPath) -and (Test-Path -LiteralPath $energyPath -PathType Leaf)) {
        $files.Add((Resolve-Path -LiteralPath $energyPath).Path)
    }

    $smartSenseRoots = @(
        (Join-Path $env:ProgramData "Lenovo\Vantage\Addins\ThinkSmartSenseAddin"),
        (Join-Path $env:ProgramData "Lenovo\VantageService\Addins\ThinkSmartSenseAddin"),
        (Join-Path $env:ProgramData "Lenovo\ImController\Plugins\ThinkSmartSenseAddin"),
        (Join-Path $env:ProgramData "Lenovo\ImController\Plugins\ThinkSmartSensePlugin")
    )
    foreach ($root in $smartSenseRoots) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
        try {
            foreach ($item in Get-ChildItem -LiteralPath $root -File -Recurse -ErrorAction Stop) {
                if ($item.Extension -in @('.dll', '.exe', '.sys')) { $files.Add($item.FullName) }
            }
        }
        catch { }
    }

    foreach ($base in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, (Join-Path $env:ProgramData "Lenovo"))) {
        if ([string]::IsNullOrWhiteSpace($base) -or -not (Test-Path -LiteralPath $base -PathType Container)) { continue }
        try {
            foreach ($item in Get-ChildItem -LiteralPath $base -Filter "LenovoEmExpandedAPI.dll" -File -Recurse -ErrorAction SilentlyContinue) {
                $files.Add($item.FullName)
            }
        }
        catch { }
    }

    return @($files | Sort-Object -Unique | Select-Object -First 80)
}

function Get-OemBinaryEvidence {
    param([string[]]$Candidates)

    $keywords = @(
        "FanSpeed", "ChangeFan", "QueryFan", "FanCtrl", "CleanDust", "EnergyDrv",
        "Thermal", "Cooling", "IntelligentCooling", "ThinkSmartSense", "Dynamic App Tuning",
        "DTT", "IPF"
    )
    $knownIoctls = [ordered]@{
        dustRemovalWrite = [uint32]2198872256       # 0x831020C0 - NEVER invoked here
        fanStateQuery = [uint32]2198872260          # 0x831020C4 - read/query only
        queryFanSpeed = [uint32]2198873456          # 0x83102570 - read/query only
        changeFanSpeed = [uint32]2198873468         # 0x8310257C - NEVER invoked here
    }

    $evidence = New-Object 'System.Collections.Generic.List[object]'
    foreach ($path in $Candidates) {
        try {
            $item = Get-Item -LiteralPath $path -ErrorAction Stop
            if ($item.Length -gt 33554432) { continue }
            $info = $item.VersionInfo
            $hits = [ordered]@{}
            foreach ($entry in $knownIoctls.GetEnumerator()) {
                $hits[$entry.Key] = [ThinkControlResearch.BinaryEvidence]::ContainsUInt32LittleEndian($path, [uint32]$entry.Value)
            }
            $strings = @([ThinkControlResearch.BinaryEvidence]::ExtractRelevantStrings($path, $keywords, 80))
            $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
            $evidence.Add([pscustomobject]@{
                fileName = $item.Name
                path = $(Convert-SafePath $item.FullName)
                sizeBytes = [long]$item.Length
                fileVersion = [string]$info.FileVersion
                productName = [string]$info.ProductName
                companyName = [string]$info.CompanyName
                sha256 = $hash
                knownIoctlConstants = [pscustomobject]$hits
                relevantStrings = $strings
            })
        }
        catch {
            $evidence.Add([pscustomobject]@{
                fileName = [IO.Path]::GetFileName($path)
                path = $(Convert-SafePath $path)
                error = $_.Exception.GetType().Name
            })
        }
    }
    return @($evidence)
}

function Export-RelevantOemBinaries {
    param([object[]]$Evidence, [string[]]$Candidates, [string]$JsonPath)

    if (-not $BundleRelevantOemBinaries) { return $null }
    $selected = New-Object 'System.Collections.Generic.List[string]'
    $totalBytes = [long]0
    foreach ($row in $Evidence) {
        $hasIoctlHit = $false
        if ($null -ne $row.knownIoctlConstants) {
            foreach ($property in $row.knownIoctlConstants.PSObject.Properties) {
                if ($property.Value -eq $true) { $hasIoctlHit = $true; break }
            }
        }
        $hasStrings = $null -ne $row.relevantStrings -and @($row.relevantStrings).Count -gt 0
        if (-not $hasIoctlHit -and -not $hasStrings) { continue }

        $candidate = $Candidates | Where-Object { [IO.Path]::GetFileName($_) -eq $row.fileName } | Select-Object -First 1
        if ([string]::IsNullOrWhiteSpace($candidate) -or -not (Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
        $length = (Get-Item -LiteralPath $candidate).Length
        if ($totalBytes + $length -gt 104857600) { continue }
        $selected.Add($candidate)
        $totalBytes += $length
    }

    if ($selected.Count -eq 0) { return $null }
    $temp = Join-Path ([IO.Path]::GetTempPath()) ("ThinkControl-OemFanEvidence-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $temp -Force | Out-Null
    try {
        $index = 0
        foreach ($source in $selected) {
            $index++
            $leaf = [IO.Path]::GetFileName($source)
            $destination = Join-Path $temp ("{0:D2}-{1}" -f $index, $leaf)
            Copy-Item -LiteralPath $source -Destination $destination -Force
        }
        $zipPath = [IO.Path]::ChangeExtension($JsonPath, $null) + "-oem-binaries.zip"
        if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
        Compress-Archive -Path (Join-Path $temp '*') -DestinationPath $zipPath -CompressionLevel Optimal
        return $zipPath
    }
    finally {
        Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$computer = Get-CimInstance Win32_ComputerSystem -ErrorAction SilentlyContinue
$product = Get-CimInstance Win32_ComputerSystemProduct -ErrorAction SilentlyContinue
$bios = Get-CimInstance Win32_BIOS -ErrorAction SilentlyContinue
$litsService = Get-CimInstance Win32_Service -Filter "Name='LITSSVC'" -ErrorAction SilentlyContinue
$litsVersion = $null
$litsPath = Get-ObjectPropertyString $litsService "PathName"
$litsExecutable = Resolve-ExecutablePath $litsPath
if (-not [string]::IsNullOrWhiteSpace($litsExecutable)) {
    try {
        if (Test-Path -LiteralPath $litsExecutable) { $litsVersion = (Get-Item -LiteralPath $litsExecutable).VersionInfo.FileVersion }
    }
    catch { }
}

$modelText = ((Get-ObjectPropertyString $computer "Model") + " " + (Get-ObjectPropertyString $product "Name") + " " + (Get-ObjectPropertyString $product "Version"))
$machineTypeMatch = [regex]::Match($modelText, '(?i)\b(21Q6|21Q7)\b')
$machineType = if ($machineTypeMatch.Success) { $machineTypeMatch.Groups[1].Value.ToUpperInvariant() } else { "" }

Write-Host "Inspecting installed Lenovo OEM fan interfaces read-only..."
$oemCandidates = @(Get-OemBinaryCandidates $litsExecutable)
$oemBinaryEvidence = @(Get-OemBinaryEvidence $oemCandidates)
$initialEnergyDrv = Get-EnergyDrvSnapshot

$meta = [pscustomobject]@{
    schemaVersion = 2
    captureLabel = $Label
    startedLocal = $started.ToString("o")
    durationSeconds = $DurationSeconds
    sampleIntervalSeconds = $SampleIntervalSeconds
    oemQueryIntervalSeconds = $OemQueryIntervalSeconds
    readOnly = $true
    manufacturer = $(Get-ObjectPropertyString $computer "Manufacturer")
    model = $(Get-ObjectPropertyString $computer "Model")
    machineType = $machineType
    biosVersion = $(Get-ObjectPropertyString $bios "SMBIOSBIOSVersion")
    litsServiceState = $(Get-ObjectPropertyString $litsService "State")
    litsServiceVersion = $litsVersion
    litsExecutable = $(Convert-SafePath $litsExecutable)
    powerAtStart = $(Get-PowerSnapshot)
    note = "Observational capture only. Known fan-write IOCTLs are scanned as byte constants but never invoked. EnergyDrv calls are limited to QueryFanSpeed 0x83102570 and fan-state query 0x831020C4."
}

$samples = New-Object 'System.Collections.Generic.List[object]'
$deadline = [DateTimeOffset]::Now.AddSeconds($DurationSeconds)
$lastEnergyDrv = $initialEnergyDrv
$lastEnergyDrvAt = [DateTimeOffset]::Now
Write-Host "Capturing Lenovo Auto evidence for $DurationSeconds seconds..."
Write-Host "Do not change fan settings just for the script. Reproduce the state you want to compare naturally."
Write-Host "Output: $OutputPath"

while ([DateTimeOffset]::Now -lt $deadline) {
    $sampleStarted = [DateTimeOffset]::Now
    if (($sampleStarted - $lastEnergyDrvAt).TotalSeconds -ge $OemQueryIntervalSeconds) {
        $lastEnergyDrv = Get-EnergyDrvSnapshot
        $lastEnergyDrvAt = $sampleStarted
    }

    $samples.Add([pscustomobject]@{
        timestampLocal = $sampleStarted.ToString("o")
        thinkControl = $(Get-ThinkControlStatus)
        energyDrv = $lastEnergyDrv
        lits = $(Get-LitsSnapshot)
    })

    $remaining = $SampleIntervalSeconds - ([DateTimeOffset]::Now - $sampleStarted).TotalSeconds
    if ($remaining -gt 0) {
        Start-Sleep -Milliseconds ([int][Math]::Round($remaining * 1000))
    }
}

$document = [pscustomobject]@{
    meta = $meta
    energyDrvInitial = $initialEnergyDrv
    oemBinaryEvidence = $oemBinaryEvidence
    powerAtEnd = $(Get-PowerSnapshot)
    samples = $samples
}

$folder = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($folder)) {
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
}
$document | ConvertTo-Json -Depth 40 | Set-Content -Path $OutputPath -Encoding UTF8
$binaryZip = Export-RelevantOemBinaries $oemBinaryEvidence $oemCandidates $OutputPath

Write-Host ""
Write-Host "Capture complete: $OutputPath"
if (-not [string]::IsNullOrWhiteSpace($binaryZip)) {
    Write-Host "Optional OEM binary evidence bundle: $binaryZip"
    Write-Host "The ZIP stays local until you choose to share it. It contains only selected Lenovo/driver binaries with relevant fan evidence."
}
Write-Host "You can review the JSON before sharing it."
