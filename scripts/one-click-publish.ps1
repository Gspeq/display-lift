# DisplayLift-Publisher-Version: 8
[CmdletBinding()]
param(
    [string]$RepoName = 'display-lift',
    [ValidateSet('public', 'private')]
    [string]$Visibility = 'public'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot

if ($RepoName -notmatch '^[A-Za-z0-9._-]+$') {
    throw 'RepoName may contain only letters, numbers, periods, underscores and hyphens.'
}

function Refresh-Path {
    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = "$machinePath;$userPath;C:\Program Files\dotnet"
}

function Ensure-WingetPackage {
    param(
        [Parameter(Mandatory)] [string]$Command,
        [Parameter(Mandatory)] [string]$PackageId,
        [Parameter(Mandatory)] [string]$DisplayName
    )
    if (Get-Command $Command -ErrorAction SilentlyContinue) { return }
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw "$DisplayName is missing and winget is unavailable. Install it manually, then rerun this script."
    }
    Write-Host "Installing $DisplayName..."
    winget install --id $PackageId --exact --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) { throw "$DisplayName installation failed." }
    Refresh-Path
    if (-not (Get-Command $Command -ErrorAction SilentlyContinue)) {
        throw "$DisplayName was installed but is not available in PATH yet. Restart Windows and rerun the installer."
    }
}

function Invoke-CapturedProcess {
    param(
        [Parameter(Mandatory)] [string]$FilePath,
        [string[]]$ArgumentList = @()
    )
    $stdoutPath = [System.IO.Path]::GetTempFileName()
    $stderrPath = [System.IO.Path]::GetTempFileName()
    try {
        $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -NoNewWindow -Wait -PassThru `
            -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
        $stdout = if (Test-Path $stdoutPath) { Get-Content -LiteralPath $stdoutPath -Raw -ErrorAction SilentlyContinue } else { '' }
        $stderr = if (Test-Path $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw -ErrorAction SilentlyContinue } else { '' }
        return [PSCustomObject]@{ ExitCode = $process.ExitCode; StdOut = [string]$stdout; StdErr = [string]$stderr }
    }
    finally {
        Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Assert-LastExitCode {
    param([Parameter(Mandatory)] [string]$Message)
    if ($LASTEXITCODE -ne 0) { throw $Message }
}

function New-DesktopShortcut {
    param([Parameter(Mandatory)] [string]$TargetPath)
    $desktop = [Environment]::GetFolderPath('Desktop')
    $shortcutPath = Join-Path $desktop 'DisplayLift Rust Auto.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.WorkingDirectory = Split-Path -Parent $TargetPath
    $shortcut.Description = 'Rust automatic scene and display color manager'
    $shortcut.IconLocation = "$TargetPath,0"
    $shortcut.Save()
    Write-Host "Desktop shortcut: $shortcutPath" -ForegroundColor Green
}

Ensure-WingetPackage -Command git -PackageId Git.Git -DisplayName 'Git'
Ensure-WingetPackage -Command gh -PackageId GitHub.cli -DisplayName 'GitHub CLI'

& (Join-Path $PSScriptRoot 'build.ps1')
& (Join-Path $RepoRoot 'tests\Test-RepositoryState.ps1')

Push-Location $RepoRoot
try {
    $AuthStatus = Invoke-CapturedProcess -FilePath 'gh' -ArgumentList @('auth', 'status')
    if ($AuthStatus.ExitCode -ne 0) {
        Write-Host 'Sign in to GitHub in the browser window that opens.'
        gh auth login --web --git-protocol https
        Assert-LastExitCode 'GitHub authentication failed.'
    }

    $LoginResult = Invoke-CapturedProcess -FilePath 'gh' -ArgumentList @('api', 'user', '--jq', '.login')
    if ($LoginResult.ExitCode -ne 0) { throw "Could not read the authenticated GitHub username. $($LoginResult.StdErr.Trim())" }
    $Login = $LoginResult.StdOut.Trim()
    $UserIdResult = Invoke-CapturedProcess -FilePath 'gh' -ArgumentList @('api', 'user', '--jq', '.id')
    if ($UserIdResult.ExitCode -ne 0) { throw "Could not read the authenticated GitHub user ID. $($UserIdResult.StdErr.Trim())" }
    $UserId = $UserIdResult.StdOut.Trim()

    if (-not (Test-Path -LiteralPath '.git')) {
        git init -b main
        Assert-LastExitCode 'git init failed.'
    }

    git config --local core.autocrlf false
    Assert-LastExitCode 'Could not disable automatic line-ending conversion.'
    git config --local core.eol lf
    Assert-LastExitCode 'Could not configure LF repository line endings.'

    $NameResult = Invoke-CapturedProcess -FilePath 'git' -ArgumentList @('config', '--local', '--get', 'user.name')
    if ($NameResult.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($NameResult.StdOut)) {
        git config --local user.name $Login
        Assert-LastExitCode 'Could not configure the local Git username.'
    }
    $EmailResult = Invoke-CapturedProcess -FilePath 'git' -ArgumentList @('config', '--local', '--get', 'user.email')
    if ($EmailResult.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($EmailResult.StdOut)) {
        git config --local user.email "$UserId+$Login@users.noreply.github.com"
        Assert-LastExitCode 'Could not configure the local Git email.'
    }

    $BranchResult = Invoke-CapturedProcess -FilePath 'git' -ArgumentList @('branch', '--show-current')
    $Branch = $BranchResult.StdOut.Trim()
    if ([string]::IsNullOrWhiteSpace($Branch)) {
        git switch -C main
        Assert-LastExitCode 'Could not switch to the main branch.'
        $Branch = 'main'
    }

    git add -A -- .gitattributes .github .gitignore LICENSE THIRD-PARTY-NOTICES.md ONE-CLICK-BUILD.cmd ONE-CLICK-PUBLISH.cmd README.md scripts src tests
    Assert-LastExitCode 'git add failed.'

    $StagedDiff = Invoke-CapturedProcess -FilePath 'git' -ArgumentList @('diff', '--cached', '--quiet')
    if ($StagedDiff.ExitCode -eq 1) {
        git commit -m 'Add Rust auto region visual detection'
        Assert-LastExitCode 'git commit failed.'
    }
    elseif ($StagedDiff.ExitCode -ne 0) {
        throw "Could not inspect staged Git changes. $($StagedDiff.StdErr.Trim())"
    }
    else {
        Write-Host 'No source changes to commit; reusing the existing local commit.'
    }

    & (Join-Path $RepoRoot 'tests\Test-RepositoryState.ps1') -RequireClean

    $RemoteListResult = Invoke-CapturedProcess -FilePath 'git' -ArgumentList @('remote')
    if ($RemoteListResult.ExitCode -ne 0) { throw "Could not list Git remotes. $($RemoteListResult.StdErr.Trim())" }
    $RemoteNames = @($RemoteListResult.StdOut -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    $FullRepo = "$Login/$RepoName"
    if ('origin' -notin $RemoteNames) {
        $RepoView = Invoke-CapturedProcess -FilePath 'gh' -ArgumentList @('repo', 'view', $FullRepo, '--json', 'nameWithOwner', '--jq', '.nameWithOwner')
        if ($RepoView.ExitCode -ne 0) {
            $VisibilityFlag = "--$Visibility"
            gh repo create $FullRepo $VisibilityFlag --description 'An external Windows Rust visual utility with automatic screen-color scene detection and biome-tuned display settings.'
            Assert-LastExitCode 'GitHub repository creation failed.'
        }
        else {
            Write-Host "Using existing GitHub repository $FullRepo."
        }
        git remote add origin "https://github.com/$FullRepo.git"
        Assert-LastExitCode "Could not add Git remote 'origin'."
    }

    $OriginUrlResult = Invoke-CapturedProcess -FilePath 'git' -ArgumentList @('remote', 'get-url', 'origin')
    if ($OriginUrlResult.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($OriginUrlResult.StdOut)) {
        throw "Git remote 'origin' exists but has no usable URL."
    }
    $OriginUrl = $OriginUrlResult.StdOut.Trim()
    $escapedFullRepo = [regex]::Escape($FullRepo)
    if ($OriginUrl -notmatch "[/:]$escapedFullRepo(?:\.git)?$") {
        throw "Git remote 'origin' points to an unrelated repository: $OriginUrl. Expected $FullRepo."
    }
    Write-Host "Origin remote: $OriginUrl"

    git fetch --prune origin
    Assert-LastExitCode 'git fetch origin failed.'

    $RemoteRefResult = Invoke-CapturedProcess -FilePath 'git' -ArgumentList @('show-ref', '--verify', '--quiet', "refs/remotes/origin/$Branch")
    if ($RemoteRefResult.ExitCode -eq 0) {
        $CountsResult = Invoke-CapturedProcess -FilePath 'git' -ArgumentList @('rev-list', '--left-right', '--count', "origin/$Branch...$Branch")
        if ($CountsResult.ExitCode -ne 0) { throw "Could not compare local and remote branches. $($CountsResult.StdErr.Trim())" }
        $Counts = @($CountsResult.StdOut.Trim() -split '\s+')
        $RemoteOnly = [int]$Counts[0]
        $LocalOnly = [int]$Counts[1]
        if ($RemoteOnly -gt 0 -and $LocalOnly -gt 0) {
            throw "Local $Branch and origin/$Branch have diverged. Nothing was force-pushed."
        }
        if ($RemoteOnly -gt 0 -and $LocalOnly -eq 0) {
            git merge --ff-only "origin/$Branch"
            Assert-LastExitCode "Could not fast-forward local $Branch from origin/$Branch."
        }
    }
    elseif ($RemoteRefResult.ExitCode -ne 1) {
        throw "Could not inspect remote branch origin/$Branch. $($RemoteRefResult.StdErr.Trim())"
    }

    git push -u origin $Branch
    Assert-LastExitCode 'git push failed.'
    & (Join-Path $RepoRoot 'tests\Test-RepositoryState.ps1') -RequireClean -RequireRemoteSync

    $Exe = Join-Path $RepoRoot 'dist\DisplayLift.exe'
    New-DesktopShortcut -TargetPath $Exe

    Write-Host ''
    Write-Host "Published and verified: $FullRepo" -ForegroundColor Green
    Write-Host "Local HEAD and origin/$Branch are identical." -ForegroundColor Green
    Write-Host 'Launching DisplayLift Rust Auto Visuals...' -ForegroundColor Green
    Start-Process -FilePath $Exe -WorkingDirectory (Split-Path -Parent $Exe)
    gh repo view --web
}
finally {
    Pop-Location
}
