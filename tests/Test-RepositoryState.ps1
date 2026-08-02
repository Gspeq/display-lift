[CmdletBinding()]
param(
    [switch]$RequireClean,
    [switch]$RequireRemoteSync
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot

$ExpectedManagedFiles = @(
    '.gitattributes',
    '.github/workflows/build-windows.yml',
    '.gitignore',
    'LICENSE',
    'ONE-CLICK-BUILD.cmd',
    'ONE-CLICK-PUBLISH.cmd',
    'README.md',
    'THIRD-PARTY-NOTICES.md',
    'scripts/build.ps1',
    'scripts/one-click-publish.ps1',
    'src/DisplayLift/AppSettings.cs',
    'src/DisplayLift/app.manifest',
    'src/DisplayLift/ColorEffectController.cs',
    'src/DisplayLift/ColorMatrixBuilder.cs',
    'src/DisplayLift/DisplayEffectEngine.cs',
    'src/DisplayLift/DisplayLift.csproj',
    'src/DisplayLift/ForegroundProcess.cs',
    'src/DisplayLift/GammaController.cs',
    'src/DisplayLift/MainForm.cs',
    'src/DisplayLift/NvidiaVibranceController.cs',
    'src/DisplayLift/Program.cs',
    'src/DisplayLift/RustLocator.cs',
    'src/DisplayLift/RustScene.cs',
    'src/DisplayLift/ScreenSceneDetector.cs',
    'src/DisplayLift/SettingsStore.cs',
    'src/DisplayLift/StartupManager.cs',
    'tests/DisplayLift.Tests/DisplayLift.Tests.csproj',
    'tests/DisplayLift.Tests/Program.cs',
    'tests/Test-RepositoryState.ps1'
) | Sort-Object

function Convert-ToRepoPath {
    param([Parameter(Mandatory)] [string]$Path)
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullRoot = [System.IO.Path]::GetFullPath($RepoRoot).TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the repository: $Path"
    }
    return $fullPath.Substring($fullRoot.Length).Replace('\', '/')
}

function Add-Failure {
    param([Parameter(Mandatory)] [string]$Message)
    $script:Failures.Add($Message) | Out-Null
}

$Failures = New-Object 'System.Collections.Generic.List[string]'
foreach ($relativePath in $ExpectedManagedFiles) {
    $nativePath = Join-Path $RepoRoot ($relativePath.Replace('/', '\'))
    if (-not (Test-Path -LiteralPath $nativePath -PathType Leaf)) { Add-Failure "Missing managed file: $relativePath" }
}

$ActualManagedFiles = New-Object 'System.Collections.Generic.List[string]'
foreach ($managedRoot in @('.github', 'scripts', 'src', 'tests')) {
    $nativeRoot = Join-Path $RepoRoot $managedRoot
    if (-not (Test-Path -LiteralPath $nativeRoot)) { continue }
    Get-ChildItem -LiteralPath $nativeRoot -File -Recurse | ForEach-Object {
        $repoPath = Convert-ToRepoPath $_.FullName
        if ($repoPath -match '(^|/)(bin|obj)(/|$)') { return }
        $ActualManagedFiles.Add($repoPath) | Out-Null
    }
}

foreach ($relativePath in @($ActualManagedFiles | Where-Object { $_ -notin $ExpectedManagedFiles } | Sort-Object -Unique)) {
    Add-Failure "Unexpected file in a managed source directory: $relativePath"
}
foreach ($relativePath in @($ExpectedManagedFiles | Where-Object { $_ -match '^(.github|scripts|src|tests)/' -and $_ -notin $ActualManagedFiles })) {
    Add-Failure "Managed source scan did not find: $relativePath"
}

$FilesForDuplicateNameScan = Get-ChildItem -LiteralPath $RepoRoot -File -Recurse | Where-Object {
    $repoPath = Convert-ToRepoPath $_.FullName
    $repoPath -notmatch '(^|/)(\.git|bin|obj|dist)(/|$)' -and
    $repoPath -notmatch '^DisplayLift-win-[^/]+\.zip(\.sha256)?$'
}
$CopyNamePattern = '(?i)(?:\s+-\s+copy|\s+copy|\s*\(\d+\))(?=\.[^./]+$|$)'
foreach ($file in $FilesForDuplicateNameScan) {
    if ($file.Name -match $CopyNamePattern) { Add-Failure "Possible duplicate-copy filename detected: $(Convert-ToRepoPath $file.FullName)" }
}

foreach ($group in ($ActualManagedFiles | Group-Object { $_.ToLowerInvariant() } | Where-Object { $_.Count -gt 1 })) {
    Add-Failure "Case-colliding managed paths detected: $(($group.Group | Sort-Object) -join ', ')"
}

$HashGroups = $ExpectedManagedFiles | ForEach-Object {
    $nativePath = Join-Path $RepoRoot ($_.Replace('/', '\'))
    if (Test-Path -LiteralPath $nativePath -PathType Leaf) {
        [PSCustomObject]@{ Path = $_; Hash = (Get-FileHash -LiteralPath $nativePath -Algorithm SHA256).Hash }
    }
} | Group-Object Hash | Where-Object { $_.Count -gt 1 }
foreach ($group in $HashGroups) {
    Add-Failure "Byte-for-byte duplicate managed files detected: $(($group.Group.Path | Sort-Object) -join ', ')"
}

if (($RequireClean -or $RequireRemoteSync) -and (Test-Path -LiteralPath (Join-Path $RepoRoot '.git'))) {
    Push-Location $RepoRoot
    try {
        $TrackedFiles = @(git ls-files)
        if ($LASTEXITCODE -ne 0) { Add-Failure 'git ls-files failed.' }
        else {
            foreach ($collision in ($TrackedFiles | Group-Object { $_.ToLowerInvariant() } | Where-Object { $_.Count -gt 1 })) {
                Add-Failure "Git tracks case-colliding duplicate paths: $(($collision.Group | Sort-Object) -join ', ')"
            }
            foreach ($relativePath in $ExpectedManagedFiles) {
                if ($relativePath -notin $TrackedFiles) { Add-Failure "Managed file is not tracked by Git: $relativePath" }
            }
            foreach ($trackedPath in $TrackedFiles) {
                if ($trackedPath -match '(^|/)(bin|obj|dist)(/|$)' -or $trackedPath -match '^DisplayLift-win-[^/]+\.zip(\.sha256)?$') {
                    Add-Failure "Generated build output is incorrectly tracked by Git: $trackedPath"
                }
            }
        }

        if ($RequireClean) {
            $StatusLines = @(git status --porcelain=v1 --untracked-files=all)
            if ($LASTEXITCODE -ne 0) { Add-Failure 'git status failed.' }
            elseif ($StatusLines.Count -gt 0) { Add-Failure "Local repository is not clean: $($StatusLines -join '; ')" }
        }

        if ($RequireRemoteSync) {
            $RemoteNames = @(git remote)
            if ($LASTEXITCODE -ne 0 -or 'origin' -notin $RemoteNames) { Add-Failure "Git remote 'origin' is missing." }
            else {
                git fetch --prune origin
                if ($LASTEXITCODE -ne 0) { Add-Failure 'git fetch origin failed.' }
                else {
                    $Branch = (git branch --show-current).Trim()
                    if ([string]::IsNullOrWhiteSpace($Branch)) { Add-Failure 'The local repository is not on a named branch.' }
                    else {
                        $LocalHead = (git rev-parse HEAD).Trim()
                        $RemoteHead = (git rev-parse "refs/remotes/origin/$Branch").Trim()
                        if ($LASTEXITCODE -ne 0) { Add-Failure "Remote branch origin/$Branch does not exist." }
                        elseif ($LocalHead -ne $RemoteHead) {
                            $Counts = (git rev-list --left-right --count "$Branch...origin/$Branch").Trim()
                            Add-Failure "Local $Branch and origin/$Branch are not synchronized (local/remote counts: $Counts)."
                        }
                        $Upstream = (git rev-parse --abbrev-ref --symbolic-full-name '@{u}').Trim()
                        if ($LASTEXITCODE -ne 0 -or $Upstream -ne "origin/$Branch") { Add-Failure "Local branch $Branch is not tracking origin/$Branch." }
                    }
                }
            }
        }
    }
    finally { Pop-Location }
}
elseif ($RequireClean -or $RequireRemoteSync) {
    Add-Failure 'Git repository metadata is missing.'
}

if ($Failures.Count -gt 0) {
    Write-Host ''
    Write-Host 'Repository verification failed:' -ForegroundColor Red
    foreach ($failure in $Failures) { Write-Host "  - $failure" -ForegroundColor Red }
    throw "DisplayLift repository verification failed with $($Failures.Count) problem(s)."
}

Write-Host 'Repository verification passed: managed files are unique and repository state is valid.' -ForegroundColor Green
