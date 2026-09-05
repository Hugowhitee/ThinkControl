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

# READ-ONLY RESEARCH TOOL.
#
# This script never writes EC registers, Lenovo registry values, power policy,
# services, ACPI methods or firmware fan targets. It may invoke only:
#   * ThinkControl named-pipe GetStatus
#   * LENOVO_OTHER_METHOD.GetFeatureValue for 0x04030001..0x04030004
#   * EnergyDrv QueryFanSpeed 0x83102570 for fan indices 0/1
#   * EnergyDrv read-only fan-state query 0x831020C4 with input 14
#
# It NEVER invokes LENOVO_OTHER_METHOD.SetFeatureValue, EnergyDrv ChangeFanSpeed
# 0x8310257C, dust/high-speed 0x831020C0, arbitrary IOCTLs, or arbitrary EC writes.
# Optional OEM binary bundling copies selected Lenovo binaries for OFFLINE analysis;
# it does not load or execute them.

$pipeName = "ThinkControl.Service.v1"
$litsRoot = "HKLM:\SYSTEM\CurrentControlSet\Services\LITSSVC\IC"
$wmiNamespace = "root/WMI"
$otherModeMethodClass = "LENOVO_OTHER_METHOD"
$otherModeCapabilityClass = "LENOVO_CAPABILITY_DATA_00"
$otherModeFanTestClass = "LENOVO_FAN_TEST_DATA"
$started = [DateTimeOffset]::Now
$maximumBundleBytes = [long]104857600
$maximumCandidateBytes = [long]33554432

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $safeLabel = ($Label -replace '[^A-Za-z0-9._-]', '_')
    $desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
    if ([string]::IsNullOrWhiteSpace($desktop)) { $desktop = $PWD.Path }
    $OutputPath = Join-Path $desktop ("ThinkControl-LenovoAuto-{0}-{1}.json" -f $safeLabel, $started.ToString("yyyyMMdd-HHmmss"))
}

function Get-ObjectPropertyValue {
    param([object]$InputObject, [string]$Name)
    if ($null -eq $InputObject) { return $null }
    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-ObjectPropertyString {
    param([object]$InputObject, [string]$Name)
    $value = Get-ObjectPropertyValue $InputObject $Name
    if ($null -eq $value) { return "" }
    return [string]$value
}

function Convert-SafePath {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    $text = $Value
    $home = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    if (-not [string]::IsNullOrWhiteSpace($home)) { $text = $text.Replace($home, "[user]") }
    $text = $text.Replace([Environment]::UserName, "[redacted]")
    $text = $text.Replace([Environment]::MachineName, "[redacted]")
    return $text
}

function Convert-SafeRegistryValue {
    param([object]$Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [byte[]]) {
        $bytes = [byte[]]$Value
        $take = [Math]::Min($bytes.Length, 64)
        $hex = if ($take -gt 0) { ([BitConverter]::ToString($bytes, 0, $take)).Replace("-", "") } else { "" }
        if ($bytes.Length -gt $take) { $hex += "..." }
        return $hex
    }
    if ($Value -is [Array]) {
        return @($Value | Select-Object -First 32 | ForEach-Object { [string]$_ })
    }
    $text = Convert-SafePath ([string]$Value)
    if ($text.Length -gt 240) { $text = $text.Substring(0, 240) + "..." }
    return $text
}

function Get-RegistryKeyValues {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try {
        $item = Get-ItemProperty -LiteralPath $Path -ErrorAction Stop
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
    if (-not (Test-Path -LiteralPath $litsRoot)) { return [pscustomobject]@{ available = $false } }
    $result = [ordered]@{ available = $true; IC = $(Get-RegistryKeyValues $litsRoot) }
    try {
        foreach ($key in Get-ChildItem -LiteralPath $litsRoot -ErrorAction Stop | Sort-Object PSChildName) {
            $result[$key.PSChildName] = Get-RegistryKeyValues $key.PSPath
        }
    }
    catch { $result["enumerationError"] = $_.Exception.GetType().Name }
    return [pscustomobject]$result
}

function Get-ThinkControlStatus {
    $pipe = $null; $writer = $null; $reader = $null
    try {
        $pipe = New-Object System.IO.Pipes.NamedPipeClientStream -ArgumentList @(
            ".", $pipeName, [System.IO.Pipes.PipeDirection]::InOut, [System.IO.Pipes.PipeOptions]::None)
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
                [pscustomobject]@{ id = [string]$_.id; label = [string]$_.label; rpm = [int]$_.rpm; source = [string]$_.source }
            })
        }
        return [pscustomobject]@{
            online = $true; success = $true
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
    catch { return [pscustomobject]@{ online = $false; success = $false; error = $_.Exception.GetType().Name } }
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
        if ($LASTEXITCODE -eq 0 -and $null -ne $output) { $activeScheme = (($output | Out-String).Trim() -replace '\s+', ' ') }
    }
    catch { }
    $lineStatus = "Unknown"
    try { $lineStatus = [System.Windows.Forms.SystemInformation]::PowerStatus.PowerLineStatus.ToString() } catch { }
    return [pscustomobject]@{ activeScheme = $activeScheme; acLineStatus = $lineStatus }
}

function Convert-UInt32Array {
    param([object]$Value)
    if ($null -eq $Value) { return @() }
    $result = New-Object 'System.Collections.Generic.List[uint32]'
    foreach ($item in @($Value)) { try { $result.Add([uint32]$item) } catch { } }
    return @($result)
}

function Get-OtherModeFanAttributeId {
    param([ValidateRange(0, 3)][int]$ZeroBasedIndex)
    return [uint32](0x04030000 -bor ($ZeroBasedIndex + 1))
}

function Test-SaneFanRange {
    param([object]$Minimum, [object]$Maximum)
    if ($null -eq $Minimum -or $null -eq $Maximum) { return $false }
    try {
        $min = [uint32]$Minimum; $max = [uint32]$Maximum
        return $min -ge 100 -and $max -gt $min -and $max -le 20000
    }
    catch { return $false }
}

function Get-LenovoOtherModeSnapshot {
    # Mirrors the production missing-capdata fallback without writing. Explicitly
    # present invalid/read-only records are authoritative; an omitted record can be
    # a direct-ID candidate only with sane Fan Test bounds plus a live GET.
    try { $capabilityRows = @(Get-CimInstance -Namespace $wmiNamespace -ClassName $otherModeCapabilityClass -ErrorAction Stop) }
    catch { $capabilityRows = @(); $capabilityQueryError = $_.Exception.GetType().Name }
    if (-not (Get-Variable capabilityQueryError -Scope Local -ErrorAction SilentlyContinue)) { $capabilityQueryError = $null }
    try { $fanTestRows = @(Get-CimInstance -Namespace $wmiNamespace -ClassName $otherModeFanTestClass -ErrorAction Stop) } catch { $fanTestRows = @() }
    try { $methodRows = @(Get-CimInstance -Namespace $wmiNamespace -ClassName $otherModeMethodClass -ErrorAction Stop) } catch { $methodRows = @() }

    $constraints = @{}
    foreach ($row in $fanTestRows) {
        $ids = @(Convert-UInt32Array (Get-ObjectPropertyValue $row "FanId"))
        $mins = @(Convert-UInt32Array (Get-ObjectPropertyValue $row "FanMinSpeed"))
        $maxes = @(Convert-UInt32Array (Get-ObjectPropertyValue $row "FanMaxSpeed"))
        $count = [Math]::Min($ids.Count, [Math]::Min($mins.Count, $maxes.Count))
        for ($i = 0; $i -lt $count; $i++) {
            if ($ids[$i] -ge 1 -and $ids[$i] -le 4) {
                $constraints[[string][uint32]$ids[$i]] = [pscustomobject]@{ minRpm = [uint32]$mins[$i]; maxRpm = [uint32]$maxes[$i] }
            }
        }
        if ($constraints.Count -gt 0) { break }
    }

    $method = $methodRows | Where-Object {
        $active = Get-ObjectPropertyValue $_ "Active"
        $null -eq $active -or [bool]$active
    } | Select-Object -First 1

    $channels = New-Object 'System.Collections.Generic.List[object]'
    for ($index = 0; $index -lt 4; $index++) {
        $attributeId = Get-OtherModeFanAttributeId $index
        $capRow = $capabilityRows | Where-Object {
            try { [uint32](Get-ObjectPropertyValue $_ "IDs") -eq $attributeId } catch { $false }
        } | Select-Object -First 1
        $capabilityPresent = $null -ne $capRow
        $capability = [uint32]0
        $defaultValue = $null
        if ($capabilityPresent) {
            try { $capability = [uint32](Get-ObjectPropertyValue $capRow "Capability") } catch { }
            try { $defaultValue = [uint32](Get-ObjectPropertyValue $capRow "DefaultValue") } catch { }
        }

        $fanId = [uint32]($index + 1)
        $rangeKey = [string]$fanId
        $range = if ($constraints.ContainsKey($rangeKey)) { $constraints[$rangeKey] } else { $null }
        $minRpm = if ($null -ne $range) { [uint32]$range.minRpm } else { $null }
        $maxRpm = if ($null -ne $range) { [uint32]$range.maxRpm } else { $null }
        $saneRange = Test-SaneFanRange $minRpm $maxRpm
        $valid = $capabilityPresent -and (($capability -band 0x1) -ne 0)
        $canGet = $capabilityPresent -and (($capability -band 0x2) -ne 0)
        $canSet = $capabilityPresent -and (($capability -band 0x4) -ne 0)
        $missingCapFallback = -not $capabilityPresent -and $saneRange
        $allowedLiveRead = ($valid -and $canGet) -or $missingCapFallback
        $liveValue = $null; $liveError = $null

        if ($null -ne $method -and $allowedLiveRead) {
            try {
                $methodResult = Invoke-CimMethod -InputObject $method -MethodName "GetFeatureValue" -Arguments @{ IDs = [uint32]$attributeId } -ErrorAction Stop
                $valueProperty = $methodResult.PSObject.Properties | Where-Object { $_.Name -ieq "value" } | Select-Object -First 1
                if ($null -ne $valueProperty -and $null -ne $valueProperty.Value) { $liveValue = [uint32]$valueProperty.Value }
                else { $liveError = "GetFeatureValue returned no value property" }
            }
            catch { $liveError = $_.Exception.GetType().Name }
        }

        $liveSane = $null -ne $liveValue -and $liveValue -le 20000
        $safeWritableMetadata = ($capabilityPresent -and $valid -and $canGet -and $canSet -and $saneRange) -or $missingCapFallback
        $channels.Add([pscustomobject]@{
            fan = $index + 1
            attributeId = ("0x{0:X8}" -f $attributeId)
            capabilityPresent = $capabilityPresent
            capability = ("0x{0:X8}" -f $capability)
            valid = $valid; canGet = $canGet; canSetTargetRpm = $canSet
            missingCapabilityDirectIdFallback = $missingCapFallback
            defaultValue = $defaultValue; minRpm = $minRpm; maxRpm = $maxRpm
            liveRpm = $liveValue; liveReadSuccess = $liveSane; liveReadError = $liveError
            directTargetRpmCandidate = ($safeWritableMetadata -and $liveSane)
        })
    }

    $writableLive = @($channels | Where-Object { $_.directTargetRpmCandidate }).Count
    return [pscustomobject]@{
        available = @($channels | Where-Object { $_.liveReadSuccess }).Count -gt 0
        capturedLocal = [DateTimeOffset]::Now.ToString("o")
        methodAvailable = $null -ne $method
        capabilityQueryError = $capabilityQueryError
        capabilityRecordCount = $capabilityRows.Count
        fanTestRecordCount = $fanTestRows.Count
        writableLiveChannels = $writableLive
        directTargetRpmCandidate = $writableLive -ge 2
        targetSemantics = "Known Lenovo contract: SetFeatureValue on 0x0403000N writes target RPM; 0 means Lenovo Auto; effective targets use 100-RPM granularity. This capture invokes GetFeatureValue only."
        writeMethodsInvoked = $false
        channels = @($channels)
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
namespace ThinkControlResearch {
    public sealed class EnergyDrvQueryResult {
        public bool Success { get; set; }
        public uint Value { get; set; }
        public uint BytesReturned { get; set; }
        public int Win32Error { get; set; }
        public string OpenAccess { get; set; }
    }
    public static class ReadOnlyEnergyDrvProbe {
        const uint OPEN_EXISTING=3, SHARE_READ=1, SHARE_WRITE=2, GENERIC_READ=0x80000000;
        const uint QUERY_FAN_SPEED=0x83102570, QUERY_FAN_STATE=0x831020C4;
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        static extern SafeFileHandle CreateFile(string n,uint a,uint s,IntPtr sa,uint c,uint f,IntPtr t);
        [DllImport("kernel32.dll", SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
        static extern bool DeviceIoControl(SafeFileHandle h,uint code,ref uint input,uint inSize,out uint output,uint outSize,out uint returned,IntPtr o);
        static SafeFileHandle Open(uint access) { return CreateFile(@"\\.\EnergyDrv",access,SHARE_READ|SHARE_WRITE,IntPtr.Zero,OPEN_EXISTING,0,IntPtr.Zero); }
        static EnergyDrvQueryResult Query(uint ioctl,uint input) {
            SafeFileHandle h=Open(0); string access="no-access query handle";
            if(h==null || h.IsInvalid){ if(h!=null)h.Dispose(); h=Open(GENERIC_READ); access="GENERIC_READ"; }
            using(h){
                if(h==null || h.IsInvalid) return new EnergyDrvQueryResult{Success=false,Win32Error=Marshal.GetLastWin32Error(),OpenAccess=access};
                uint output,returned; bool ok=DeviceIoControl(h,ioctl,ref input,4,out output,4,out returned,IntPtr.Zero);
                return new EnergyDrvQueryResult{Success=ok && returned>=4,Value=output,BytesReturned=returned,Win32Error=ok?0:Marshal.GetLastWin32Error(),OpenAccess=access};
            }
        }
        public static EnergyDrvQueryResult QueryFanSpeed(uint index){ if(index>1)throw new ArgumentOutOfRangeException("index"); return Query(QUERY_FAN_SPEED,index); }
        public static EnergyDrvQueryResult QueryFanState(){ return Query(QUERY_FAN_STATE,14); }
    }
    public static class BinaryEvidence {
        public static bool ContainsUInt32LittleEndian(string path,uint value){
            byte[] data=File.ReadAllBytes(path), n=BitConverter.GetBytes(value);
            for(int i=0;i<=data.Length-4;i++) if(data[i]==n[0]&&data[i+1]==n[1]&&data[i+2]==n[2]&&data[i+3]==n[3]) return true;
            return false;
        }
        public static string[] ExtractRelevantStrings(string path,string[] keywords,int maxResults){
            byte[] data=File.ReadAllBytes(path); var set=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ExtractAscii(data,keywords,maxResults,set); if(set.Count<maxResults)ExtractUtf16(data,keywords,maxResults,set);
            string[] a=new string[set.Count]; set.CopyTo(a); Array.Sort(a,StringComparer.OrdinalIgnoreCase); return a;
        }
        static void Add(string value,string[] keywords,int max,HashSet<string> set){
            if(value==null||value.Length<4||set.Count>=max)return;
            foreach(string k in keywords) if(value.IndexOf(k,StringComparison.OrdinalIgnoreCase)>=0){ set.Add(value.Length>300?value.Substring(0,300):value); return; }
        }
        static void ExtractAscii(byte[] data,string[] keywords,int max,HashSet<string> set){
            var b=new StringBuilder();
            for(int i=0;i<=data.Length;i++){
                bool p=i<data.Length&&data[i]>=32&&data[i]<=126;
                if(p&&b.Length<320){b.Append((char)data[i]);continue;} Add(b.ToString(),keywords,max,set);b.Length=0;if(set.Count>=max)return;
            }
        }
        static void ExtractUtf16(byte[] data,string[] keywords,int max,HashSet<string> set){
            var b=new StringBuilder();
            for(int i=0;i+1<data.Length;i+=2){
                bool p=data[i+1]==0&&data[i]>=32&&data[i]<=126;
                if(p&&b.Length<320){b.Append((char)data[i]);continue;} Add(b.ToString(),keywords,max,set);b.Length=0;if(set.Count>=max)return;
            }
        }
    }
}
'@
try { Add-Type -TypeDefinition $probeSource -Language CSharp -ErrorAction Stop }
catch { Write-Warning ("Read-only OEM probe helper could not compile: " + $_.Exception.Message) }

function Convert-EnergyDrvResult {
    param([object]$Result)
    if ($null -eq $Result) { return $null }
    return [pscustomobject]@{
        success=[bool]$Result.Success
        rawValue=if($Result.Success){[uint32]$Result.Value}else{$null}
        bytesReturned=[uint32]$Result.BytesReturned
        win32Error=[int]$Result.Win32Error
        openAccess=[string]$Result.OpenAccess
    }
}

function Get-EnergyDrvSnapshot {
    try {
        $fan0=[ThinkControlResearch.ReadOnlyEnergyDrvProbe]::QueryFanSpeed(0)
        $fan1=[ThinkControlResearch.ReadOnlyEnergyDrvProbe]::QueryFanSpeed(1)
        $state=[ThinkControlResearch.ReadOnlyEnergyDrvProbe]::QueryFanState()
        return [pscustomobject]@{
            available=($fan0.Success -or $fan1.Success -or $state.Success)
            capturedLocal=[DateTimeOffset]::Now.ToString("o")
            queryFanSpeedIoctl="0x83102570"; fan0=$(Convert-EnergyDrvResult $fan0); fan1=$(Convert-EnergyDrvResult $fan1)
            fanStateQueryIoctl="0x831020C4"; fanStateQueryInput=14; fanState=$(Convert-EnergyDrvResult $state)
            valueSemantics="Raw OEM query values only; exact-X9 physical correlation determines whether the returned unit is RPM."
            writeIoctlsInvoked=$false
        }
    }
    catch { return [pscustomobject]@{available=$false;capturedLocal=[DateTimeOffset]::Now.ToString("o");error=$_.Exception.GetType().Name;writeIoctlsInvoked=$false} }
}

function Resolve-ExecutablePath {
    param([string]$RawPath)
    if ([string]::IsNullOrWhiteSpace($RawPath)) { return "" }
    $text=$RawPath.Trim()
    if($text.StartsWith('"')){
        $closing=$text.IndexOf('"',1)
        if($closing -gt 1){$text=$text.Substring(1,$closing-1)}
    } else {
        # Win32_Service.PathName is sometimes an unquoted path containing spaces.
        # Capture through the first executable suffix, then discard arguments.
        $match=[regex]::Match($text,'^(?<exe>.+?\.(?:exe|sys|dll))(?=\s|$)',[Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if($match.Success){$text=$match.Groups['exe'].Value}
    }
    if($text.StartsWith("\??\")){$text=$text.Substring(4)}
    if($text.StartsWith("\SystemRoot\",[StringComparison]::OrdinalIgnoreCase)){$text=Join-Path $env:SystemRoot $text.Substring("\SystemRoot\".Length)}
    return $text
}

function Get-OemBinaryCandidates {
    param([string]$LitsExecutable)
    $files=New-Object 'System.Collections.Generic.List[string]'
    if(-not [string]::IsNullOrWhiteSpace($LitsExecutable)-and(Test-Path -LiteralPath $LitsExecutable -PathType Leaf)){$files.Add((Resolve-Path -LiteralPath $LitsExecutable).Path)}
    $energy=Get-CimInstance Win32_SystemDriver -Filter "Name='EnergyDrv'" -ErrorAction SilentlyContinue
    $energyPath=Resolve-ExecutablePath (Get-ObjectPropertyString $energy "PathName")
    if(-not [string]::IsNullOrWhiteSpace($energyPath)-and(Test-Path -LiteralPath $energyPath -PathType Leaf)){$files.Add((Resolve-Path -LiteralPath $energyPath).Path)}
    $roots=@(
        (Join-Path $env:ProgramData "Lenovo\Vantage\Addins\ThinkSmartSenseAddin"),
        (Join-Path $env:ProgramData "Lenovo\VantageService\Addins\ThinkSmartSenseAddin"),
        (Join-Path $env:ProgramData "Lenovo\ImController\Plugins\ThinkSmartSenseAddin"),
        (Join-Path $env:ProgramData "Lenovo\ImController\Plugins\ThinkSmartSensePlugin"))
    foreach($root in $roots){
        if(-not(Test-Path -LiteralPath $root -PathType Container)){continue}
        try{foreach($item in Get-ChildItem -LiteralPath $root -File -Recurse -ErrorAction Stop){if($item.Extension -in @('.dll','.exe','.sys')){$files.Add($item.FullName)}}}catch{}
    }
    foreach($base in @($env:ProgramFiles,${env:ProgramFiles(x86)},(Join-Path $env:ProgramData "Lenovo"))){
        if([string]::IsNullOrWhiteSpace($base)-or -not(Test-Path -LiteralPath $base -PathType Container)){continue}
        try{foreach($item in Get-ChildItem -LiteralPath $base -Filter "LenovoEmExpandedAPI.dll" -File -Recurse -ErrorAction SilentlyContinue){$files.Add($item.FullName)}}catch{}
    }
    return @($files|Sort-Object -Unique|Select-Object -First 80)
}

function Get-OemBinaryEvidence {
    param([string[]]$Candidates,[hashtable]$PathByHash)
    $keywords=@("FanSpeed","ChangeFan","QueryFan","FanCtrl","CleanDust","EnergyDrv","Thermal","Cooling","IntelligentCooling","ThinkSmartSense","Dynamic App Tuning","LENOVO_OTHER_METHOD","LENOVO_CAPABILITY_DATA_00","LENOVO_FAN_TEST_DATA","DTT","IPF")
    $known=[ordered]@{dustRemovalWrite=[uint32]0x831020C0;fanStateQuery=[uint32]0x831020C4;queryFanSpeed=[uint32]0x83102570;changeFanSpeed=[uint32]0x8310257C}
    $rows=New-Object 'System.Collections.Generic.List[object]'
    foreach($path in $Candidates){
        try{
            $item=Get-Item -LiteralPath $path -ErrorAction Stop
            if($item.Length -gt $maximumCandidateBytes){continue}
            $hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
            if(-not $PathByHash.ContainsKey($hash)){$PathByHash[$hash]=$item.FullName}
            $hits=[ordered]@{};foreach($entry in $known.GetEnumerator()){$hits[$entry.Key]=[ThinkControlResearch.BinaryEvidence]::ContainsUInt32LittleEndian($path,[uint32]$entry.Value)}
            $strings=@([ThinkControlResearch.BinaryEvidence]::ExtractRelevantStrings($path,$keywords,80));$info=$item.VersionInfo
            $rows.Add([pscustomobject]@{fileName=$item.Name;path=$(Convert-SafePath $item.FullName);sizeBytes=[long]$item.Length;fileVersion=[string]$info.FileVersion;productName=[string]$info.ProductName;companyName=[string]$info.CompanyName;sha256=$hash;knownIoctlConstants=[pscustomobject]$hits;relevantStrings=$strings;error=$null})
        }catch{
            $rows.Add([pscustomobject]@{fileName=[IO.Path]::GetFileName($path);path=$(Convert-SafePath $path);sizeBytes=$null;fileVersion="";productName="";companyName="";sha256=$null;knownIoctlConstants=$null;relevantStrings=@();error=$_.Exception.GetType().Name})
        }
    }
    return @($rows)
}

function Export-RelevantOemBinaries {
    param([object[]]$Evidence,[hashtable]$PathByHash,[string]$JsonPath)
    if(-not $BundleRelevantOemBinaries){return $null}
    $selected=New-Object 'System.Collections.Generic.List[string]';$seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase);$total=[long]0
    foreach($row in $Evidence){
        $ioctls=Get-ObjectPropertyValue $row "knownIoctlConstants";$strings=Get-ObjectPropertyValue $row "relevantStrings";$hash=[string](Get-ObjectPropertyValue $row "sha256")
        $hasHit=$false
        if($null -ne $ioctls){foreach($p in $ioctls.PSObject.Properties){if($p.Value -eq $true){$hasHit=$true;break}}}
        $hasStrings=$null -ne $strings -and @($strings).Count -gt 0
        if((-not $hasHit -and -not $hasStrings)-or [string]::IsNullOrWhiteSpace($hash)-or -not $PathByHash.ContainsKey($hash)){continue}
        $candidate=[string]$PathByHash[$hash]
        if(-not(Test-Path -LiteralPath $candidate -PathType Leaf)-or -not $seen.Add($candidate)){continue}
        $length=(Get-Item -LiteralPath $candidate).Length;if($total+$length -gt $maximumBundleBytes){continue}
        $selected.Add($candidate);$total+=$length
    }
    if($selected.Count -eq 0){return $null}
    $temp=Join-Path ([IO.Path]::GetTempPath()) ("ThinkControl-OemFanEvidence-"+[Guid]::NewGuid().ToString("N"));New-Item -ItemType Directory -Path $temp -Force|Out-Null
    try{
        $i=0;foreach($source in $selected){$i++;Copy-Item -LiteralPath $source -Destination (Join-Path $temp ("{0:D2}-{1}" -f $i,[IO.Path]::GetFileName($source))) -Force}
        $zip=[IO.Path]::ChangeExtension($JsonPath,$null)+"-oem-binaries.zip";if(Test-Path -LiteralPath $zip){Remove-Item -LiteralPath $zip -Force};Compress-Archive -Path (Join-Path $temp '*') -DestinationPath $zip -CompressionLevel Optimal;return $zip
    }finally{Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue}
}

$computer=Get-CimInstance Win32_ComputerSystem -ErrorAction SilentlyContinue
$product=Get-CimInstance Win32_ComputerSystemProduct -ErrorAction SilentlyContinue
$bios=Get-CimInstance Win32_BIOS -ErrorAction SilentlyContinue
$litsService=Get-CimInstance Win32_Service -Filter "Name='LITSSVC'" -ErrorAction SilentlyContinue
$litsExecutable=Resolve-ExecutablePath (Get-ObjectPropertyString $litsService "PathName")
$litsVersion=$null
try{if(-not[string]::IsNullOrWhiteSpace($litsExecutable)-and(Test-Path -LiteralPath $litsExecutable)){$litsVersion=(Get-Item -LiteralPath $litsExecutable).VersionInfo.FileVersion}}catch{}
$modelText=((Get-ObjectPropertyString $computer "Model")+" "+(Get-ObjectPropertyString $product "Name")+" "+(Get-ObjectPropertyString $product "Version"))
$machineTypeMatch=[regex]::Match($modelText,'(?i)\b(21Q6|21Q7)\b');$machineType=if($machineTypeMatch.Success){$machineTypeMatch.Groups[1].Value.ToUpperInvariant()}else{""}

Write-Host "Inspecting installed Lenovo OEM fan interfaces read-only..."
$pathByHash=@{};$oemCandidates=@(Get-OemBinaryCandidates $litsExecutable);$oemBinaryEvidence=@(Get-OemBinaryEvidence $oemCandidates $pathByHash)
$initialEnergyDrv=Get-EnergyDrvSnapshot;$initialOtherMode=Get-LenovoOtherModeSnapshot
$meta=[pscustomobject]@{schemaVersion=4;captureLabel=$Label;startedLocal=$started.ToString("o");durationSeconds=$DurationSeconds;sampleIntervalSeconds=$SampleIntervalSeconds;oemQueryIntervalSeconds=$OemQueryIntervalSeconds;readOnly=$true;manufacturer=$(Get-ObjectPropertyString $computer "Manufacturer");model=$(Get-ObjectPropertyString $computer "Model");machineType=$machineType;biosVersion=$(Get-ObjectPropertyString $bios "SMBIOSBIOSVersion");litsServiceState=$(Get-ObjectPropertyString $litsService "State");litsServiceVersion=$litsVersion;litsExecutable=$(Convert-SafePath $litsExecutable);powerAtStart=$(Get-PowerSnapshot);note="Observational capture only. Other Mode uses GetFeatureValue only; EnergyDrv calls are QueryFanSpeed 0x83102570 and fan-state query 0x831020C4 only. No fan writer is invoked."}

$samples=New-Object 'System.Collections.Generic.List[object]';$deadline=[DateTimeOffset]::Now.AddSeconds($DurationSeconds);$lastEnergyDrv=$initialEnergyDrv;$lastOtherMode=$initialOtherMode;$lastOemQueryAt=[DateTimeOffset]::Now
Write-Host "Capturing Lenovo Auto evidence for $DurationSeconds seconds...";Write-Host "Output: $OutputPath"
while([DateTimeOffset]::Now -lt $deadline){
    $sampleStarted=[DateTimeOffset]::Now
    if(($sampleStarted-$lastOemQueryAt).TotalSeconds -ge $OemQueryIntervalSeconds){$lastEnergyDrv=Get-EnergyDrvSnapshot;$lastOtherMode=Get-LenovoOtherModeSnapshot;$lastOemQueryAt=$sampleStarted}
    $samples.Add([pscustomobject]@{timestampLocal=$sampleStarted.ToString("o");thinkControl=$(Get-ThinkControlStatus);energyDrv=$lastEnergyDrv;lenovoOtherMode=$lastOtherMode;lits=$(Get-LitsSnapshot)})
    $remaining=$SampleIntervalSeconds-([DateTimeOffset]::Now-$sampleStarted).TotalSeconds;if($remaining -gt 0){Start-Sleep -Milliseconds ([int][Math]::Round($remaining*1000))}
}

$document=[pscustomobject]@{meta=$meta;energyDrvInitial=$initialEnergyDrv;lenovoOtherModeInitial=$initialOtherMode;oemBinaryEvidence=$oemBinaryEvidence;powerAtEnd=$(Get-PowerSnapshot);samples=$samples}
$folder=Split-Path -Parent $OutputPath;if(-not[string]::IsNullOrWhiteSpace($folder)){New-Item -ItemType Directory -Path $folder -Force|Out-Null}
$document|ConvertTo-Json -Depth 40|Set-Content -LiteralPath $OutputPath -Encoding UTF8
$binaryZip=Export-RelevantOemBinaries $oemBinaryEvidence $pathByHash $OutputPath
Write-Host "";Write-Host "Capture complete: $OutputPath"
if(-not[string]::IsNullOrWhiteSpace($binaryZip)){Write-Host "Optional OEM binary evidence bundle: $binaryZip";Write-Host "The ZIP stays local until you choose to share it."}
Write-Host "You can review the JSON before sharing it."
