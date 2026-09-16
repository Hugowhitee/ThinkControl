param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\release-previews'
}
else {
    $OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory, $repoRoot)
}

Push-Location $repoRoot
try {
    dotnet run --project tools/ThinkControl.Snapshots/ThinkControl.Snapshots.csproj --configuration Release --no-build -- $OutputDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Snapshot renderer failed with exit code $LASTEXITCODE"
    }

    $previewNames = @(
        'compact-dark.png',
        'advanced-home.png',
        'advanced-touchpad.png',
        'sensor-details.png',
        'advanced-fans.png',
        'advanced-audio.png',
        'advanced-battery.png',
        'hardware-setup-ready.png'
    )
    foreach ($name in $previewNames) {
        $path = Join-Path $OutputDirectory $name
        if (-not (Test-Path $path)) {
            throw "Missing release preview: $name"
        }
        if ((Get-Item $path).Length -lt 5000) {
            throw "Release preview is unexpectedly small: $name"
        }
    }

    Add-Type -AssemblyName System.Drawing
    $overview = New-Object System.Drawing.Bitmap 1642, 1532
    $graphics = [System.Drawing.Graphics]::FromImage($overview)
    try {
        $graphics.Clear([System.Drawing.Color]::FromArgb(17, 19, 21))
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        function Draw-Preview([string]$name, [int]$x, [int]$y, [int]$width, [int]$height) {
            $image = [System.Drawing.Image]::FromFile((Join-Path $OutputDirectory $name))
            try {
                $scale = [math]::Min(([double]$width / $image.Width), ([double]$height / $image.Height))
                $drawWidth = [int][math]::Round($image.Width * $scale)
                $drawHeight = [int][math]::Round($image.Height * $scale)
                $drawX = $x + [int][math]::Floor(($width - $drawWidth) / 2)
                $drawY = $y + [int][math]::Floor(($height - $drawHeight) / 2)
                $graphics.DrawImage($image, $drawX, $drawY, $drawWidth, $drawHeight)
            }
            finally {
                $image.Dispose()
            }
        }

        Draw-Preview 'compact-dark.png' 24 84 410 640
        Draw-Preview 'advanced-home.png' 458 24 1160 760

        $grid = @(
            'advanced-touchpad.png',
            'sensor-details.png',
            'advanced-fans.png',
            'advanced-audio.png',
            'advanced-battery.png',
            'hardware-setup-ready.png'
        )
        for ($i = 0; $i -lt $grid.Count; $i++) {
            $column = $i % 3
            $row = [math]::Floor($i / 3)
            $x = 24 + ($column * 540)
            $y = 808 + ($row * 363)
            Draw-Preview $grid[$i] $x $y 514 337
        }

        $overviewPath = Join-Path $OutputDirectory 'ui-overview.png'
        $overview.Save($overviewPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $overview.Dispose()
    }

    $overviewFile = Get-Item (Join-Path $OutputDirectory 'ui-overview.png')
    if ($overviewFile.Length -lt 20000) {
        throw 'Release overview is unexpectedly small'
    }

    Write-Host "Release overview: $([math]::Round($overviewFile.Length / 1KB)) KB"
}
finally {
    Pop-Location
}
