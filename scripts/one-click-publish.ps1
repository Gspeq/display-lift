[CmdletBinding()]
param(
    [string]$RepoName = 'display-lift',
    [ValidateSet('public', 'private')]
    [string]$Visibility = 'public'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot

function Refresh-Path {
    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = "$machinePath;$userPath"
}

function Ensure-WingetPackage {
    param(
        [Parameter(Mandatory)] [string]$Command,
        [Parameter(Mandatory)] [string]$PackageId,
        [Parameter(Mandatory)] [string]$DisplayName
    )

    if (Get-Command $Command -ErrorAction SilentlyContinue) {
        return
    }

    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw "$DisplayName is missing and winget is unavailable. Install it manually, then rerun this script."
    }

    Write-Host "Installing $DisplayName..."
    winget install --id $PackageId --exact --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) {
        throw "$DisplayName installation failed."
    }
    Refresh-Path
}

Ensure-WingetPackage -Command git -PackageId Git.Git -DisplayName 'Git'
Ensure-WingetPackage -Command gh -PackageId GitHub.cli -DisplayName 'GitHub CLI'

& (Join-Path $PSScriptRoot 'build.ps1')

Push-Location $RepoRoot
try {
    gh auth status *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Sign in to GitHub in the browser window that opens.'
        gh auth login --web --git-protocol https
        if ($LASTEXITCODE -ne 0) {
            throw 'GitHub authentication failed.'
        }
    }

    $Login = (gh api user --jq '.login').Trim()
    $UserId = (gh api user --jq '.id').Trim()

    if (-not (Test-Path '.git')) {
        git init -b main
    }

    if (-not (git config --local user.name)) {
        git config --local user.name $Login
    }
    if (-not (git config --local user.email)) {
        git config --local user.email "$UserId+$Login@users.noreply.github.com"
    }

    git add .
    git diff --cached --quiet
    if ($LASTEXITCODE -ne 0) {
        git commit -m 'Build DisplayLift Windows utility'
    }

    $Remote = git remote get-url origin 2>$null
    if (-not $Remote) {
        $VisibilityFlag = if ($Visibility -eq 'private') { '--private' } else { '--public' }
        gh repo create $RepoName $VisibilityFlag --source . --remote origin --push --description 'A small Windows display-gamma preset utility with safe restore controls.'
        if ($LASTEXITCODE -ne 0) {
            throw 'GitHub repository creation or initial push failed.'
        }
    }
    else {
        $Branch = (git branch --show-current).Trim()
        git push -u origin $Branch
        if ($LASTEXITCODE -ne 0) {
            throw 'git push failed.'
        }
    }

    Write-Host ''
    Write-Host "Published to GitHub as $Login/$RepoName" -ForegroundColor Green
    gh repo view --web
}
finally {
    Pop-Location
}
