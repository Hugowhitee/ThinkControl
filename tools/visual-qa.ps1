param(
    [string]$Output = "artifacts/visual-qa",
    [switch]$NoBuild,
    [switch]$NoOpen
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    $outputPath = [System.IO.Path]::GetFullPath((Join-Path $repo $Output))
    New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

    if (-not $NoBuild) {
        dotnet restore ThinkControl.slnx
        if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed' }
        dotnet build ThinkControl.slnx -c Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }
    }

    dotnet run --project tools/ThinkControl.Snapshots/ThinkControl.Snapshots.csproj `
        -c Release `
        --no-build `
        -- $outputPath
    if ($LASTEXITCODE -ne 0) { throw 'Visual QA renderer failed' }

    $gallery = Join-Path $outputPath 'gallery.html'
    Write-Host "Visual QA gallery: $gallery"
    if (-not $NoOpen) {
        Start-Process $gallery
    }
}
finally {
    Pop-Location
}
