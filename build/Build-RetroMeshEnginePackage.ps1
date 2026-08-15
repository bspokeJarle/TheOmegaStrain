[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$Version = "",

    [string]$OutputDir = "",

    [switch]$SkipTests,

    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

function Invoke-DotNet {
    param([string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$engineSolution = Join-Path $repoRoot "RetroMesh.Engine.slnx"
$engineProject = Join-Path $repoRoot "RetroMesh.Engine\RetroMesh.Engine.csproj"

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "artifacts\packages"
}

$outputDirectory = (New-Item -ItemType Directory -Force -Path $OutputDir).FullName
$startedAt = Get-Date

$buildArgs = @("build", $engineSolution, "-c", $Configuration)
if ($NoRestore) {
    $buildArgs += "--no-restore"
}

Invoke-DotNet -Arguments $buildArgs

if (-not $SkipTests) {
    Invoke-DotNet -Arguments @("test", $engineSolution, "-c", $Configuration, "--no-build")
}

$packArgs = @("pack", $engineProject, "-c", $Configuration, "--no-build", "-o", $outputDirectory)
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $packArgs += "/p:PackageVersion=$Version"
}

Invoke-DotNet -Arguments $packArgs

$packages = Get-ChildItem -LiteralPath $outputDirectory -Filter "RetroMesh.Engine*.nupkg" -File |
    Where-Object { $_.LastWriteTime -ge $startedAt.AddSeconds(-1) } |
    Sort-Object LastWriteTime, Name

Write-Host "RetroMesh Engine package build completed."
Write-Host "Output directory: $outputDirectory"

foreach ($package in $packages) {
    Write-Host "  $($package.FullName)"
}
