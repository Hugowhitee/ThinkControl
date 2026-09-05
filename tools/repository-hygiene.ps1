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
        'src/ThinkControl.UI/AdvancedWindow.ColorCompat.cs',
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

    # Current hosted runners force older Node-20 action builds onto Node 24. Keep
    # ThinkControl on the maintained official action majors so CI logs stay clean
    # and release workflows do not accumulate avoidable platform debt.
    $workflowFiles = @(& git ls-files -- '.github/workflows/*.yml' '.github/workflows/*.yaml')
    foreach ($workflow in $workflowFiles) {
        $workflowText = Get-Content $workflow -Raw
        foreach ($legacyAction in @('actions/checkout@v4', 'actions/setup-dotnet@v4', 'actions/upload-artifact@v4')) {
            if ($workflowText.Contains($legacyAction, [StringComparison]::OrdinalIgnoreCase)) {
                $failures.Add("Deprecated GitHub Action major in $workflow -> $legacyAction")
            }
        }
    }

    # Research scripts are part of the hardware-validation workflow and are often
    # executed only on a physical Windows device. Parse them in hosted CI so a
    # research-only syntax error cannot reach a tester just because normal product
    # compilation does not load PowerShell files.
    $researchScripts = @(& git ls-files -- 'tools/research/*.ps1')
    foreach ($script in $researchScripts) {
        $tokens = $null
        $parseErrors = $null
        [void][System.Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $repoRoot $script),
            [ref]$tokens,
            [ref]$parseErrors)
        foreach ($parseError in @($parseErrors)) {
            $failures.Add("PowerShell parse error in $script -> $($parseError.Message)")
        }
    }

    # These are the active product/release documents, so all of them must follow
    # version.json rather than silently describing an older alpha as current.
    $metadata = Get-Content 'version.json' -Raw | ConvertFrom-Json
    $version = [string]$metadata.version
    if ([string]::IsNullOrWhiteSpace($version)) {
        $failures.Add('version.json does not contain a version')
    }
    else {
        $versionToken = "v$version"
        foreach ($document in @(
            'README.md',
            'docs/PRODUCT.md',
            'docs/ARCHITECTURE.md',
            'docs/ALPHA-TESTING.md',
            'docs/DEVICE-SUPPORT.md'
        )) {
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
