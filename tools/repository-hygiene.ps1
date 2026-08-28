$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$failures = [System.Collections.Generic.List[string]]::new()

Push-Location $repoRoot
try {
    $tracked = @(& git ls-files)
    if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed' }

    # Build output, local IDE state and runtime artifacts must never be committed.
    $generatedPattern = '(^|/)(bin|obj|artifacts|TestResults|coverage|\.vs|\.idea)(/|$)|\.(user|suo|cache|log|trx|coverage|coveragexml)$'
    foreach ($path in $tracked) {
        if ($path -match $generatedPattern) {
            $failures.Add("Tracked generated/local file: $path")
        }
    }

    # These alpha-era files/names were deliberately consolidated or removed. Keep
    # old Git history from becoming a second source of truth in later cleanup work.
    $obsoleteFiles = @(
        'design-qa.md',
        'docs/RELEASE-CHECKLIST.md',
        'docs/V0.1-ACCEPTANCE.md',
        'docs/UI_EDITING.md',
        'docs/DEPENDENCIES.md',
        'docs/LENOVO-PROVIDERS.md',
        'docs/research/g-helper-fan-ux.md',
        'installer/release-manifest.example.json',
        'tests/README.md',
        'src/ThinkControl.UI/GlobalWpfAliases.cs',
        'src/ThinkControl.UI/WpfTypeAliases.cs',
        'src/ThinkControl.UI/AdvancedWindow.Alpha30HomePolish.cs',
        'src/ThinkControl.UI/AdvancedWindow.CopyPolish.cs',
        'src/ThinkControl.UI/AdvancedWindow.HomeDashboardPolish.cs',
        'src/ThinkControl.UI/AdvancedWindow.InteractionPolish.cs',
        'src/ThinkControl.UI/AdvancedWindow.NavigationPolish.cs',
        'src/ThinkControl.UI/AdvancedWindow.NotificationPolish.cs',
        'src/ThinkControl.UI/AdvancedWindow.ShellChromePolish.cs',
        'src/ThinkControl.UI/AdvancedWindow.TouchpadPolish.cs',
        'src/ThinkControl.UI/App.TrayIconPolish.cs',
        'src/ThinkControl.UI/Controls/CompactDashboard.Alpha30Polish.cs',
        'src/ThinkControl.UI/Controls/TouchpadPanel.ReleasePolish.cs'
    )
    foreach ($obsolete in $obsoleteFiles) {
        if ($tracked -contains $obsolete) {
            $failures.Add("Obsolete duplicate/history file returned: $obsolete")
        }
    }

    foreach ($prefix in @('docs/screenshots/', 'docs/release-verification/')) {
        foreach ($path in $tracked | Where-Object { $_.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) }) {
            $failures.Add("Generated/historical docs artifact must stay out of source: $path")
        }
    }

    # README and PRODUCT are the versioned entry points people/agents are expected
    # to read first, so both must follow version.json.
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
    # anchors are intentionally outside this gate. Remove fenced examples first so
    # code snippets never become fake filesystem dependencies.
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
