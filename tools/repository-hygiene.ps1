$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$failures = [System.Collections.Generic.List[string]]::new()

Push-Location $repoRoot
try {
    $tracked = @(& git ls-files)
    if ($LASTEXITCODE -ne 0) {
        throw 'git ls-files failed'
    }

    # Build output, local IDE state and runtime artifacts must never be committed.
    $generatedPattern = '(^|/)(bin|obj|artifacts|TestResults|coverage|\.vs|\.idea)(/|$)|\.(user|suo|cache|log)$'
    foreach ($path in $tracked) {
        if ($path -match $generatedPattern) {
            $failures.Add("Tracked generated/local file: $path")
        }
    }

    # Release-specific one-off partials are deliberately consolidated into their
    # canonical owners. Keep them from quietly reappearing in a later hotfix.
    foreach ($legacyPartial in @(
        'src/ThinkControl.UI/AdvancedWindow.Alpha30HomePolish.cs',
        'src/ThinkControl.UI/Controls/CompactDashboard.Alpha30Polish.cs',
        'src/ThinkControl.UI/Controls/TouchpadPanel.ReleasePolish.cs'
    )) {
        if (Test-Path -LiteralPath $legacyPartial) {
            $failures.Add("Release-specific UI partial must stay consolidated: $legacyPartial")
        }
    }

    # The two high-level user/developer entry points must describe the same version
    # that packaging consumes from version.json.
    $metadata = Get-Content 'version.json' -Raw | ConvertFrom-Json
    $version = [string]$metadata.version
    if ([string]::IsNullOrWhiteSpace($version)) {
        $failures.Add('version.json does not contain a version')
    }
    else {
        $versionToken = "v$version"
        foreach ($document in @('README.md', 'docs/PRODUCT.md')) {
            $text = Get-Content $document -Raw
            if (-not $text.Contains($versionToken, [StringComparison]::OrdinalIgnoreCase)) {
                $failures.Add("$document does not reference current version $versionToken")
            }
        }
    }

    # Validate repository-local Markdown targets. External URLs, mail links and pure
    # anchors are intentionally outside this gate. Fenced code blocks are removed
    # first so examples do not become fake filesystem dependencies.
    $markdownFiles = @(& git ls-files -- '*.md')
    $linkPattern = '(?m)!?\[[^\]]*\]\((?<target>[^)\r\n]+)\)'
    foreach ($relative in $markdownFiles) {
        $full = Join-Path $repoRoot $relative
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
            $failures.Add("Tracked Markdown file is missing from checkout: $relative")
            continue
        }

        $content = Get-Content -LiteralPath $full -Raw
        $content = [regex]::Replace($content, '(?ms)```.*?```', '')
        foreach ($match in [regex]::Matches($content, $linkPattern)) {
            $rawTarget = $match.Groups['target'].Value.Trim()
            if ([string]::IsNullOrWhiteSpace($rawTarget)) { continue }

            $target = if ($rawTarget.StartsWith('<')) {
                $end = $rawTarget.IndexOf('>')
                if ($end -gt 1) { $rawTarget.Substring(1, $end - 1) } else { $rawTarget }
            }
            else {
                ([regex]::Match($rawTarget, '^\S+')).Value
            }

            if ([string]::IsNullOrWhiteSpace($target) -or
                $target.StartsWith('#') -or
                $target -match '^[a-zA-Z][a-zA-Z0-9+.-]*:') {
                continue
            }

            $target = ($target -split '#', 2)[0]
            $target = ($target -split '\?', 2)[0]
            if ([string]::IsNullOrWhiteSpace($target)) { continue }

            try { $target = [Uri]::UnescapeDataString($target) } catch { }
            $resolved = if ($target.StartsWith('/')) {
                Join-Path $repoRoot $target.TrimStart('/')
            }
            else {
                Join-Path (Split-Path $full -Parent) $target
            }

            if (-not (Test-Path -LiteralPath $resolved)) {
                $failures.Add("Broken local Markdown link in $relative -> $target")
            }
        }
    }

    if ($failures.Count -gt 0) {
        Write-Host 'Repository hygiene failed:' -ForegroundColor Red
        $failures | Sort-Object -Unique | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
        exit 1
    }

    Write-Host "Repository hygiene passed: $($tracked.Count) tracked paths, $($markdownFiles.Count) Markdown files."
}
finally {
    Pop-Location
}
