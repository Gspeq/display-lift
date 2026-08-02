[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $RepoRoot 'src\DisplayLift\DisplayLift.csproj'
$TestProject = Join-Path $RepoRoot 'tests\DisplayLift.Tests\DisplayLift.Tests.csproj'
$Dist = Join-Path $RepoRoot 'dist'

function Refresh-Path {
    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = "$machinePath;$userPath;C:\Program Files\dotnet"
}

function Ensure-DotNet {
    if (Get-Command dotnet -ErrorAction SilentlyContinue) { return }
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw '.NET 8 SDK is missing and winget is unavailable. Install the .NET 8 SDK, then run this script again.'
    }
    Write-Host 'Installing .NET 8 SDK...'
    winget install --id Microsoft.DotNet.SDK.8 --exact --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) { throw 'The .NET SDK installation failed.' }
    Refresh-Path
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET was installed but is not available in PATH yet. Restart Windows and rerun the installer.' }
}

Ensure-DotNet

$Running = @(Get-Process -Name 'DisplayLift' -ErrorAction SilentlyContinue)
if ($Running.Count -gt 0) {
    Write-Host 'Closing the previous DisplayLift process for the update...'
    $Running | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

Write-Host 'Running V9 auto-scene, recovery, preset and color-matrix tests...'
dotnet run --project $TestProject --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'DisplayLift tests failed.' }

if (Test-Path $Dist) { Remove-Item $Dist -Recurse -Force }
New-Item $Dist -ItemType Directory | Out-Null

Write-Host "Publishing DisplayLift V9 for $Runtime..."
dotnet publish $Project `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $Dist
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$Exe = Join-Path $Dist 'DisplayLift.exe'
if (-not (Test-Path $Exe)) { throw "Build completed without producing $Exe" }
$BuiltExecutables = @(Get-ChildItem -LiteralPath $Dist -Filter 'DisplayLift*.exe' -File)
if ($BuiltExecutables.Count -ne 1) { throw "Expected exactly one DisplayLift executable in dist, but found $($BuiltExecutables.Count)." }

Write-Host 'Running the compiled emergency restore self-test...'
$RestoreProcess = Start-Process -FilePath $Exe -ArgumentList '--restore-only' -Wait -PassThru
if ($RestoreProcess.ExitCode -ne 0) { throw 'The compiled restore-only self-test failed.' }

$Zip = Join-Path $RepoRoot "DisplayLift-$Runtime.zip"
if (Test-Path $Zip) { Remove-Item $Zip -Force }
Compress-Archive -Path (Join-Path $Dist '*') -DestinationPath $Zip -CompressionLevel Optimal
$Hash = (Get-FileHash $Zip -Algorithm SHA256).Hash
Set-Content -Path "$Zip.sha256" -Value "$Hash  $(Split-Path $Zip -Leaf)" -Encoding ascii

Write-Host ''
Write-Host 'Build complete:' -ForegroundColor Green
Write-Host "  EXE: $Exe"
Write-Host "  ZIP: $Zip"
Write-Host "  SHA256: $Hash"
