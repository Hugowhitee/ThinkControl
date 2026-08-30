param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,

    [string]$OutputPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Static/offline research helper only.
#
# This script NEVER opens \\.\EnergyDrv, never calls DeviceIoControl and never
# executes a captured OEM binary. It inspects bytes from the optional evidence ZIP
# produced by Capture-LenovoAuto.ps1 (or a directory of explicitly supplied files)
# so an exact X9 command path can be reverse-engineered before any new writer exists.

$knownIoctls = [ordered]@{
    dustRemovalWrite = [uint32]0x831020C0
    fanStateQuery = [uint32]0x831020C4
    legacyItsFullSpeed = [uint32]0x8310213C
    queryFanSpeed = [uint32]0x83102570
    changeFanSpeed = [uint32]0x8310257C
}

$keywords = @(
    "ChangeFanSpeed",
    "ChangeFanxSpeed",
    "QueryFanSpeed",
    "FanCtrl",
    "dwFanCtrlCmd",
    "CleanDust",
    "EnergyDrv",
    "FanSpeed",
    "Thermal",
    "IntelligentCooling",
    "ThinkSmartSense",
    "ChangeITSsetting",
    "com.lenovo.its.pipe.setting",
    "ENABLE_AC_COOL",
    "ENABLE_DC_COOL",
    "ImprovedCoolingEfficiency",
    "IMPROVED_COOLING_EFFICIENCY",
    "BALANCED_MODE_LCM",
    "PERFORMANCE_MODE_LCM"
)

$maximumFiles = 120
$maximumFileBytes = 33554432
$maximumHitsPerNeedle = 24
$contextBefore = 48
$contextAfter = 96
$nearbyStringRadius = 768
$maximumNearbyStrings = 32

$resolvedInput = (Resolve-Path -LiteralPath $InputPath).Path
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    if ((Get-Item -LiteralPath $resolvedInput) -is [IO.DirectoryInfo]) {
        $OutputPath = Join-Path $resolvedInput "ThinkControl-OemFanBinaryAnalysis.json"
    }
    else {
        $directory = Split-Path -Parent $resolvedInput
        $stem = [IO.Path]::GetFileNameWithoutExtension($resolvedInput)
        $OutputPath = Join-Path $directory ($stem + "-analysis.json")
    }
}

$temp = $null
$root = $resolvedInput
if ([IO.Path]::GetExtension($resolvedInput).Equals(".zip", [StringComparison]::OrdinalIgnoreCase)) {
    $temp = Join-Path ([IO.Path]::GetTempPath()) ("ThinkControl-OemFanAnalysis-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $temp -Force | Out-Null
    Expand-Archive -LiteralPath $resolvedInput -DestinationPath $temp -Force
    $root = $temp
}
elseif (-not (Test-Path -LiteralPath $resolvedInput -PathType Container)) {
    throw "InputPath must be a capture evidence ZIP or a directory."
}

function Find-BytePatternOffsets {
    param(
        [byte[]]$Data,
        [byte[]]$Needle,
        [int]$Limit
    )

    $hits = New-Object 'System.Collections.Generic.List[int]'
    if ($Needle.Length -eq 0 -or $Data.Length -lt $Needle.Length) { return @() }

    for ($offset = 0; $offset -le $Data.Length - $Needle.Length; $offset++) {
        $match = $true
        for ($i = 0; $i -lt $Needle.Length; $i++) {
            if ($Data[$offset + $i] -ne $Needle[$i]) {
                $match = $false
                break
            }
        }
        if (-not $match) { continue }
        $hits.Add($offset)
        if ($hits.Count -ge $Limit) { break }
    }
    return @($hits)
}

function Convert-HexContext {
    param(
        [byte[]]$Data,
        [int]$Offset,
        [int]$Before,
        [int]$After
    )

    $start = [Math]::Max(0, $Offset - $Before)
    $endExclusive = [Math]::Min($Data.Length, $Offset + 4 + $After)
    $count = [Math]::Max(0, $endExclusive - $start)
    $hex = if ($count -gt 0) {
        ([BitConverter]::ToString($Data, $start, $count)).Replace("-", " ")
    }
    else { "" }

    return [pscustomobject]@{
        startOffset = $start
        hitOffset = $Offset
        hitOffsetHex = ("0x{0:X}" -f $Offset)
        bytes = $count
        hex = $hex
    }
}

function Get-PrintableStrings {
    param(
        [byte[]]$Data,
        [int]$Start,
        [int]$Length,
        [string[]]$Keywords,
        [int]$Limit
    )

    $results = New-Object 'System.Collections.Generic.List[object]'
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $end = [Math]::Min($Data.Length, $Start + $Length)

    for ($mode = 0; $mode -lt 2 -and $results.Count -lt $Limit; $mode++) {
        $step = if ($mode -eq 0) { 1 } else { 2 }
        $offset = $Start
        while ($offset -lt $end -and $results.Count -lt $Limit) {
            $stringStart = $offset
            $builder = New-Object Text.StringBuilder
            while ($offset -lt $end -and $builder.Length -lt 320) {
                if ($mode -eq 0) {
                    $value = $Data[$offset]
                    if ($value -lt 32 -or $value -gt 126) { break }
                    [void]$builder.Append([char]$value)
                    $offset++
                }
                else {
                    if ($offset + 1 -ge $end) { break }
                    $value = $Data[$offset]
                    if ($Data[$offset + 1] -ne 0 -or $value -lt 32 -or $value -gt 126) { break }
                    [void]$builder.Append([char]$value)
                    $offset += 2
                }
            }

            $text = $builder.ToString()
            if ($text.Length -ge 4) {
                $relevant = $false
                foreach ($keyword in $Keywords) {
                    if ($text.IndexOf($keyword, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                        $relevant = $true
                        break
                    }
                }
                if ($relevant) {
                    $key = "$mode|$stringStart|$text"
                    if ($seen.Add($key)) {
                        $results.Add([pscustomobject]@{
                            offset = $stringStart
                            offsetHex = ("0x{0:X}" -f $stringStart)
                            encoding = if ($mode -eq 0) { "ascii" } else { "utf16le" }
                            value = $text
                        })
                    }
                }
            }

            if ($offset -le $stringStart) { $offset = $stringStart + $step }
            else { $offset += $step }
        }
    }

    return @($results)
}

function Get-NearbyRelevantStrings {
    param(
        [byte[]]$Data,
        [int]$HitOffset
    )

    $start = [Math]::Max(0, $HitOffset - $nearbyStringRadius)
    $endExclusive = [Math]::Min($Data.Length, $HitOffset + 4 + $nearbyStringRadius)
    return @(Get-PrintableStrings $Data $start ($endExclusive - $start) $keywords $maximumNearbyStrings)
}

function Get-FileAnalysis {
    param([IO.FileInfo]$File, [string]$Root)

    $relative = [IO.Path]::GetRelativePath($Root, $File.FullName)
    if ($File.Length -gt $maximumFileBytes) {
        return [pscustomobject]@{
            file = $relative
            sizeBytes = [long]$File.Length
            skipped = "file exceeds 32 MiB static-analysis limit"
        }
    }

    $data = [IO.File]::ReadAllBytes($File.FullName)
    $ioctlHits = [ordered]@{}
    foreach ($entry in $knownIoctls.GetEnumerator()) {
        $needle = [BitConverter]::GetBytes([uint32]$entry.Value)
        $offsets = @(Find-BytePatternOffsets $data $needle $maximumHitsPerNeedle)
        $ioctlHits[$entry.Key] = [pscustomobject]@{
            value = ("0x{0:X8}" -f [uint32]$entry.Value)
            countCaptured = $offsets.Count
            truncated = $offsets.Count -ge $maximumHitsPerNeedle
            hits = @($offsets | ForEach-Object {
                [pscustomobject]@{
                    offset = $_
                    offsetHex = ("0x{0:X}" -f $_)
                    context = $(Convert-HexContext $data $_ $contextBefore $contextAfter)
                    nearbyRelevantStrings = @(Get-NearbyRelevantStrings $data $_)
                }
            })
        }
    }

    $wholeFileStrings = @(Get-PrintableStrings $data 0 $data.Length $keywords 80)
    $version = $null
    try { $version = $File.VersionInfo } catch { }

    return [pscustomobject]@{
        file = $relative
        sizeBytes = [long]$File.Length
        sha256 = (Get-FileHash -LiteralPath $File.FullName -Algorithm SHA256).Hash
        fileVersion = if ($null -ne $version) { [string]$version.FileVersion } else { "" }
        productName = if ($null -ne $version) { [string]$version.ProductName } else { "" }
        companyName = if ($null -ne $version) { [string]$version.CompanyName } else { "" }
        knownIoctls = [pscustomobject]$ioctlHits
        relevantStrings = $wholeFileStrings
    }
}

try {
    $files = @(Get-ChildItem -LiteralPath $root -File -Recurse -ErrorAction Stop |
        Where-Object { $_.Extension -in @('.dll', '.exe', '.sys') } |
        Sort-Object FullName |
        Select-Object -First $maximumFiles)

    Write-Host ("Static-analyzing {0} OEM candidate files. No captured binary will be executed." -f $files.Count)
    $analyses = New-Object 'System.Collections.Generic.List[object]'
    foreach ($file in $files) {
        try { $analyses.Add((Get-FileAnalysis $file $root)) }
        catch {
            $analyses.Add([pscustomobject]@{
                file = [IO.Path]::GetRelativePath($root, $file.FullName)
                error = $_.Exception.GetType().Name
            })
        }
    }

    $interesting = @($analyses | Where-Object {
        $row = $_
        if ($null -eq $row.knownIoctls) { return $false }
        foreach ($property in $row.knownIoctls.PSObject.Properties) {
            if ($null -ne $property.Value -and $property.Value.countCaptured -gt 0) { return $true }
        }
        return @($row.relevantStrings).Count -gt 0
    })

    $document = [pscustomobject]@{
        schemaVersion = 1
        generatedLocal = [DateTimeOffset]::Now.ToString("o")
        staticOnly = $true
        safety = "No OEM binary execution, no driver handles, no DeviceIoControl, no registry/service/power/EC writes."
        knownContracts = [pscustomobject]@{
            queryFanSpeed = "0x83102570 · one UInt32 zero-based fan index -> one UInt32 speed value"
            changeFanSpeed = "0x8310257C · one UInt32 dwFanCtrlCmd -> one UInt32 action status · X9 command encoding still unverified"
            dustRemoval = "0x831020C0/0x831020C4 · separate maintenance/query family; not a smooth target-RPM backend"
            legacyItsOverlay = "0x8310213C · family-specific overlay evidence; not generalized to X9"
            litsPolicy = "ThinkSmartSense/LITSSvc policy strings are research evidence only; AC/DC Cool, Improved Cooling Efficiency and LCM commands are not treated as direct fan-speed contracts"
        }
        scannedFiles = $files.Count
        interestingFiles = $interesting.Count
        files = $interesting
    }

    $outputDirectory = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
        New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    }
    $document | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
    Write-Host "Static OEM fan analysis complete: $OutputPath"
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($temp)) {
        Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
}
