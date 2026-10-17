<#
.SYNOPSIS
    Builds Brady's installer: runs the tests, publishes a self-contained win-x64 build and compiles
    it with Inno Setup 6 into artifacts/Brady-Setup-<version>.exe.

.DESCRIPTION
    The version comes from Directory.Build.props, the one place it is set. Works in Windows
    PowerShell 5.1 and PowerShell 7. Inno Setup is found via $env:ISCC, the per-user and
    machine-wide install folders, or PATH (install it with: winget install JRSoftware.InnoSetup).

.EXAMPLE
    .\build\build-installer.ps1
#>
[CmdletBinding()]
param(
    # Skip the test run (the release workflow tests in its own step).
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Invoke-Step([string] $Name, [scriptblock] $Command) {
    Write-Host "==> $Name" -ForegroundColor Green
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Name failed (exit code $LASTEXITCODE)." }
}

function Find-Iscc {
    $candidates = @(
        $env:ISCC,
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    ) | Where-Object { $_ -and (Test-Path $_) }
    if ($candidates) { return @($candidates)[0] }
    $onPath = Get-Command iscc -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    throw 'Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup'
}

Push-Location $root
try {
    $version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version |
        Where-Object { $_ } | Select-Object -First 1
    if (-not $version) { throw 'No <Version> in Directory.Build.props.' }
    Write-Host "Brady $version" -ForegroundColor Cyan

    if (-not $SkipTests) {
        Invoke-Step 'Test' { dotnet test --project tests/Brady.Core.Tests -c Release }
    }

    $publish = Join-Path $root 'artifacts\publish'
    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
    Invoke-Step 'Publish (self-contained win-x64)' { dotnet publish src/Brady.App/Brady.App.csproj -p:PublishProfile=win-x64 }
    # Project references still copy their .pdb; the installer ships no symbols.
    Get-ChildItem $publish -Filter *.pdb | Remove-Item

    $iscc = Find-Iscc
    Invoke-Step "Compile installer ($iscc)" { & $iscc /Q "/DAppVersion=$version" "/DPublishDir=$publish" (Join-Path $root 'build\Brady.iss') }

    $setup = Get-Item (Join-Path $root "artifacts\Brady-Setup-$version.exe")
    $publishMb = [math]::Round(((Get-ChildItem $publish -Recurse | Measure-Object Length -Sum).Sum / 1MB), 1)
    Write-Host ("Built {0} ({1:N1} MB; {2} MB installed)" -f $setup.FullName, ($setup.Length / 1MB), $publishMb) -ForegroundColor Cyan
}
finally {
    Pop-Location
}
