$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

Push-Location $repoRoot
try {
    $pairs = @(
        @('assets/brand/v3/windows/ThinkControl.ico', 'src/ThinkControl.UI/Assets/ThinkControl.ico'),
        @('assets/brand/v3/windows/ThinkControl_mark.ico', 'src/ThinkControl.UI/Assets/tray.ico')
    )
    foreach ($pair in $pairs) {
        $source = (Get-FileHash $pair[0] -Algorithm SHA256).Hash
        $target = (Get-FileHash $pair[1] -Algorithm SHA256).Hash
        if ($source -ne $target) {
            throw "Branding drift: $($pair[1]) is not the exact v3 source asset."
        }
    }

    $wordmarkDir = 'assets/brand/v3/wordmark'
    $canonicalWordmarks = @(
        "$wordmarkDir/ThinkControl_wordmark_dark.svg",
        "$wordmarkDir/ThinkControl_wordmark_light.svg"
    )
    foreach ($wordmarkPath in $canonicalWordmarks) {
        if (-not (Test-Path $wordmarkPath)) {
            throw "Canonical wordmark is missing: $wordmarkPath"
        }

        $wordmark = Get-Content $wordmarkPath -Raw
        if ($wordmark -notmatch 'viewBox="0 0 455 144"' -or
            $wordmark -notmatch 'translate\(203\.500947 33\.452652\)' -or
            $wordmark -notmatch 'translate\(-6 -0\.5\)') {
            throw "Canonical wordmark alignment drifted: $wordmarkPath"
        }
    }

    $duplicateWordmarks = Get-ChildItem $wordmarkDir -File | Where-Object {
        $_.Name -match '^ThinkControl_wordmark_(outlined|refined)_' -or $_.Name -eq 'REFINEMENT.md'
    }
    if ($duplicateWordmarks) {
        throw "Legacy or duplicate wordmark assets remain: $($duplicateWordmarks.Name -join ', ')"
    }

    $legacyDocAssets = @(
        'docs/assets/thinkcontrol-logo-dark.svg',
        'docs/assets/thinkcontrol-logo-light.svg'
    ) | Where-Object { Test-Path $_ }
    if ($legacyDocAssets) {
        throw "Legacy documentation logo copies remain: $($legacyDocAssets -join ', ')"
    }

    $readme = Get-Content 'README.md' -Raw
    if ($readme -notmatch 'ThinkControl_wordmark_dark\.svg' -or
        $readme -notmatch 'ThinkControl_wordmark_light\.svg' -or
        $readme -match 'wordmark_(outlined|refined)' -or
        $readme -match 'docs/assets/thinkcontrol-logo') {
        throw 'README does not use the canonical wordmark assets exclusively.'
    }

    $brandMark = Get-Content 'src/ThinkControl.UI/Controls/BrandMark.xaml' -Raw
    if ($brandMark -match 'Canvas Width="64"' -or $brandMark -match 'M7,9') {
        throw 'Legacy hand-drawn TC geometry is still present in BrandMark.xaml.'
    }
    if ($brandMark -notmatch 'Canvas Width="1536"' -or $brandMark -notmatch 'Canvas.Left="1219.07"') {
        throw 'BrandMark.xaml does not contain the exact v3 master geometry.'
    }

    $brandWordmark = Get-Content 'src/ThinkControl.UI/Controls/BrandWordmark.xaml' -Raw
    if ($brandWordmark -notmatch 'Canvas Width="455"' -or
        $brandWordmark -notmatch 'Canvas.Left="203\.500947"' -or
        $brandWordmark -notmatch 'Canvas.Top="33\.452652"' -or
        $brandWordmark -notmatch 'TranslateTransform X="-6" Y="-0\.5"') {
        throw 'WPF wordmark layout does not match the canonical v3 production alignment.'
    }
}
finally {
    Pop-Location
}
