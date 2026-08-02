# DisplayLift-Repository-Test-Version: 4
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
    'scripts/build.ps1',
    'scripts/one-click-publish.ps1',
    'src/DisplayLift/app.manifest',
    'src/DisplayLift/DisplayLift.csproj',
    'src/DisplayLift/DisplayPreset.cs',
    'src/DisplayLift/GammaController.cs',
    'src/DisplayLift/MainForm.cs',
    'src/DisplayLift/Program.cs',
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

$PublisherScript = Join-Path $RepoRoot 'scripts\one-click-publish.ps1'
if ((Test-Path -LiteralPath $PublisherScript -PathType Leaf) -and
    -not (Select-String -LiteralPath $PublisherScript -SimpleMatch '# DisplayLift-Publisher-Version: 4' -Quiet)) {
    Add-Failure 'The publisher script is stale; V4 was not installed.'
}

foreach ($relativePath in $ExpectedManagedFiles) {
    $nativePath = Join-Path $RepoRoot ($relativePath.Replace('/', '\'))
    if (-not (Test-Path -LiteralPath $nativePath -PathType Leaf)) {
        Add-Failure "Missing managed file: $relativePath"
    }
}

$ActualManagedFiles = New-Object 'System.Collections.Generic.List[string]'
foreach ($managedRoot in @('.github', 'scripts', 'src', 'tests')) {
    $nativeRoot = Join-Path $RepoRoot $managedRoot
    if (-not (Test-Path -LiteralPath $nativeRoot)) {
        continue
    }

    Get-ChildItem -LiteralPath $nativeRoot -File -Recurse | ForEach-Object {
        $repoPath = Convert-ToRepoPath $_.FullName
        if ($repoPath -match '(^|/)(bin|obj)(/|$)') {
            return
        }
        $ActualManagedFiles.Add($repoPath) | Out-Null
    }
}

$UnexpectedManagedFiles = @($ActualManagedFiles | Where-Object { $_ -notin $ExpectedManagedFiles } | Sort-Object -Unique)
foreach ($relativePath in $UnexpectedManagedFiles) {
    Add-Failure "Unexpected file in a managed source directory: $relativePath"
}

$MissingFromManagedScan = @($ExpectedManagedFiles | Where-Object {
    $_ -match '^(.github|scripts|src|tests)/' -and $_ -notin $ActualManagedFiles
})
foreach ($relativePath in $MissingFromManagedScan) {
    Add-Failure "Managed source scan did not find: $relativePath"
}

$FilesForDuplicateNameScan = Get-ChildItem -LiteralPath $RepoRoot -File -Recurse | Where-Object {
    $repoPath = Convert-ToRepoPath $_.FullName
    $repoPath -notmatch '(^|/)(\.git|bin|obj|dist)(/|$)' -and
    $repoPath -notmatch '^DisplayLift-win-[^/]+\.zip(\.sha256)?$'
}

$CopyNamePattern = '(?i)(?:\s+-\s+copy|\s+copy|\s*\(\d+\))(?=\.[^./]+$|$)'
foreach ($file in $FilesForDuplicateNameScan) {
    if ($file.Name -match $CopyNamePattern) {
        Add-Failure "Possible duplicate-copy filename detected: $(Convert-ToRepoPath $file.FullName)"
    }
}

$HashGroups = $ExpectedManagedFiles | ForEach-Object {
    $nativePath = Join-Path $RepoRoot ($_.Replace('/', '\'))
    if (Test-Path -LiteralPath $nativePath -PathType Leaf) {
        [PSCustomObject]@{
            Path = $_
            Hash = (Get-FileHash -LiteralPath $nativePath -Algorithm SHA256).Hash
        }
    }
} | Group-Object Hash | Where-Object { $_.Count -gt 1 }

foreach ($group in $HashGroups) {
    $paths = ($group.Group.Path | Sort-Object) -join ', '
    Add-Failure "Byte-for-byte duplicate managed files detected: $paths"
}

if (($RequireClean -or $RequireRemoteSync) -and (Test-Path -LiteralPath (Join-Path $RepoRoot '.git'))) {
    Push-Location $RepoRoot
    try {
        $TrackedFiles = @(git ls-files)
        if ($LASTEXITCODE -ne 0) {
            Add-Failure 'git ls-files failed.'
        }
        else {
            $CaseCollisions = $TrackedFiles | Group-Object { $_.ToLowerInvariant() } | Where-Object { $_.Count -gt 1 }
            foreach ($collision in $CaseCollisions) {
                Add-Failure "Git tracks case-colliding duplicate paths: $(($collision.Group | Sort-Object) -join ', ')"
            }

            foreach ($relativePath in $ExpectedManagedFiles) {
                if ($relativePath -notin $TrackedFiles) {
                    Add-Failure "Managed file is not tracked by Git: $relativePath"
                }
            }

            foreach ($trackedPath in $TrackedFiles) {
                if ($trackedPath -match '(^|/)(bin|obj|dist)(/|$)' -or $trackedPath -match '^DisplayLift-win-[^/]+\.zip(\.sha256)?$') {
                    Add-Failure "Generated build output is incorrectly tracked by Git: $trackedPath"
                }
            }
        }

        if ($RequireClean) {
            $StatusLines = @(git status --porcelain=v1 --untracked-files=all)
            if ($LASTEXITCODE -ne 0) {
                Add-Failure 'git status failed.'
            }
            elseif ($StatusLines.Count -gt 0) {
                Add-Failure "Local repository is not clean: $($StatusLines -join '; ')"
            }
        }

        if ($RequireRemoteSync) {
            $RemoteNames = @(git remote)
            if ($LASTEXITCODE -ne 0 -or 'origin' -notin $RemoteNames) {
                Add-Failure "Git remote 'origin' is missing."
            }
            else {
                git fetch --prune origin
                if ($LASTEXITCODE -ne 0) {
                    Add-Failure 'git fetch origin failed.'
                }
                else {
                    $Branch = (git branch --show-current).Trim()
                    if ([string]::IsNullOrWhiteSpace($Branch)) {
                        Add-Failure 'The local repository is not on a named branch.'
                    }
                    else {
                        $LocalHead = (git rev-parse HEAD).Trim()
                        if ($LASTEXITCODE -ne 0) {
                            Add-Failure 'Could not resolve local HEAD.'
                        }

                        $RemoteHead = (git rev-parse "refs/remotes/origin/$Branch").Trim()
                        if ($LASTEXITCODE -ne 0) {
                            Add-Failure "Remote branch origin/$Branch does not exist."
                        }
                        elseif ($LocalHead -ne $RemoteHead) {
                            $Counts = (git rev-list --left-right --count "$Branch...origin/$Branch").Trim()
                            Add-Failure "Local $Branch and origin/$Branch are not synchronized (local/remote counts: $Counts)."
                        }

                        $Upstream = (git rev-parse --abbrev-ref --symbolic-full-name '@{u}').Trim()
                        if ($LASTEXITCODE -ne 0 -or $Upstream -ne "origin/$Branch") {
                            Add-Failure "Local branch $Branch is not tracking origin/$Branch."
                        }
                    }
                }
            }
        }
    }
    finally {
        Pop-Location
    }
}
elseif ($RequireClean -or $RequireRemoteSync) {
    Add-Failure 'Git repository metadata is missing.'
}

if ($Failures.Count -gt 0) {
    Write-Host ''
    Write-Host 'Repository verification failed:' -ForegroundColor Red
    foreach ($failure in $Failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }
    throw "DisplayLift repository verification failed with $($Failures.Count) problem(s)."
}

Write-Host 'Repository verification passed: managed files are unique and repository state is valid.' -ForegroundColor Green
