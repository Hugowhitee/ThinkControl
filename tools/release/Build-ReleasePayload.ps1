$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$appVersion = [string]$env:APP_VERSION
if ([string]::IsNullOrWhiteSpace($appVersion)) {
    throw 'APP_VERSION is required.'
}
if ([string]::IsNullOrWhiteSpace($env:GITHUB_ENV)) {
    throw 'GITHUB_ENV is required so packaging metadata can be passed to later workflow steps.'
}

$releaseTagVersion = if ([string]::IsNullOrWhiteSpace($env:SOURCE_VERSION)) {
    $appVersion
}
else {
    [string]$env:SOURCE_VERSION
}

Push-Location $repoRoot
try {
    foreach ($path in @('artifacts/ui', 'artifacts/service', 'artifacts/payload-root')) {
        Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
    }

    dotnet publish src/ThinkControl.UI/ThinkControl.UI.csproj `
        -c Release -r win-x64 --self-contained false `
        -p:Version=$appVersion `
        -p:InformationalVersion=$appVersion `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o artifacts/ui
    if ($LASTEXITCODE -ne 0) {
        throw "UI publish failed with exit code $LASTEXITCODE."
    }

    dotnet publish src/ThinkControl.Service/ThinkControl.Service.csproj `
        -c Release -r win-x64 --self-contained false `
        -p:Version=$appVersion `
        -p:InformationalVersion=$appVersion `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o artifacts/service
    if ($LASTEXITCODE -ne 0) {
        throw "Service publish failed with exit code $LASTEXITCODE."
    }

    $ui = Get-Item 'artifacts/ui/ThinkControl.UI.exe' -ErrorAction Stop
    $service = Get-Item 'artifacts/service/ThinkControl.Service.exe' -ErrorAction Stop
    if ($ui.Length -lt 100000) {
        throw 'UI executable is unexpectedly small.'
    }
    if ($service.Length -lt 100000) {
        throw 'Service executable is unexpectedly small.'
    }

    $uiBytes = (Get-ChildItem artifacts/ui -Recurse -File | Measure-Object Length -Sum).Sum
    $serviceBytes = (Get-ChildItem artifacts/service -Recurse -File | Measure-Object Length -Sum).Sum
    $total = $uiBytes + $serviceBytes
    Write-Host ("UI payload: {0:N2} MB" -f ($uiBytes / 1MB))
    Write-Host ("Service payload: {0:N2} MB" -f ($serviceBytes / 1MB))
    Write-Host ("Combined installed payload: {0:N2} MB" -f ($total / 1MB))
    if ($total -gt 65MB) {
        throw 'Framework-dependent installed payload unexpectedly exceeds 65 MB.'
    }

    $root = 'artifacts/payload-root'
    $installerDir = 'artifacts/installer'
    New-Item "$root/ui" -ItemType Directory -Force | Out-Null
    New-Item "$root/service" -ItemType Directory -Force | Out-Null
    New-Item $installerDir -ItemType Directory -Force | Out-Null
    Copy-Item 'artifacts/ui/*' "$root/ui" -Recurse -Force
    Copy-Item 'artifacts/service/*' "$root/service" -Recurse -Force

    $payloadFile = "ThinkControl-Payload-$appVersion.zip"
    $payloadPath = Join-Path $installerDir $payloadFile
    Remove-Item $payloadPath -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path "$root/*" -DestinationPath $payloadPath -CompressionLevel Optimal

    $payload = Get-Item $payloadPath
    Write-Host "Payload: $payloadFile - $([math]::Round($payload.Length / 1MB, 2)) MB"
    if ($payload.Length -gt 20MB) {
        throw 'Compressed ThinkControl payload exceeds the 20 MB budget.'
    }

    $hash = (Get-FileHash $payload.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $url = "https://github.com/Hugowhitee/ThinkControl/releases/download/v$releaseTagVersion/$payloadFile"

    "PAYLOAD_FILE=$payloadFile" | Out-File $env:GITHUB_ENV -Encoding utf8 -Append
    "PAYLOAD_PATH=$($payload.FullName)" | Out-File $env:GITHUB_ENV -Encoding utf8 -Append
    "PAYLOAD_SHA256=$hash" | Out-File $env:GITHUB_ENV -Encoding utf8 -Append
    "PAYLOAD_URL=$url" | Out-File $env:GITHUB_ENV -Encoding utf8 -Append
}
finally {
    Pop-Location
}
