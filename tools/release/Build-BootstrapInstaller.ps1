$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
foreach ($name in @('APP_VERSION', 'NUMERIC_VERSION', 'PAYLOAD_FILE', 'PAYLOAD_URL', 'PAYLOAD_SHA256')) {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
        throw "$name is required."
    }
}

Push-Location $repoRoot
try {
    $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $iscc)) {
        throw 'Inno Setup compiler not found.'
    }

    Get-ChildItem 'artifacts/installer/ThinkControl-Setup-*.exe' -ErrorAction SilentlyContinue |
        Remove-Item -Force

    & $iscc `
        "/DAppVersion=$env:APP_VERSION" `
        "/DNumericVersion=$env:NUMERIC_VERSION" `
        "/DPayloadFile=$env:PAYLOAD_FILE" `
        "/DPayloadUrl=$env:PAYLOAD_URL" `
        "/DPayloadSha256=$env:PAYLOAD_SHA256" `
        'installer/ThinkControl.iss'
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }

    $setups = @(Get-ChildItem 'artifacts/installer/ThinkControl-Setup-*.exe' -File)
    if ($setups.Count -ne 1) {
        throw "Expected exactly one bootstrap installer, found $($setups.Count)."
    }

    $setup = $setups[0]
    Write-Host "Bootstrap installer: $($setup.Name) - $([math]::Round($setup.Length / 1MB, 2)) MB"
    if ($setup.Length -gt 5MB) {
        throw 'ThinkControl bootstrap installer exceeds the 5 MB hard budget. The application payload must not be embedded.'
    }
}
finally {
    Pop-Location
}
