$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($env:PAYLOAD_PATH)) {
    throw 'PAYLOAD_PATH is required.'
}

Push-Location $repoRoot
try {
    $setups = @(Get-ChildItem 'artifacts/installer/ThinkControl-Setup-*.exe' -File)
    if ($setups.Count -ne 1) {
        throw "Expected exactly one bootstrap installer, found $($setups.Count)."
    }

    $payload = Get-Item $env:PAYLOAD_PATH -ErrorAction Stop
    $lines = foreach ($file in @($setups[0], $payload)) {
        $hash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $($file.Name)"
    }
    $lines | Set-Content 'artifacts/installer/SHA256SUMS.txt' -Encoding ascii
}
finally {
    Pop-Location
}
