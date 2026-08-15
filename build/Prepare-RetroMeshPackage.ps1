[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$EngineRepoUrl = "https://github.com/bspokeJarle/RetroMesh.git",

    [string]$EnginePath = "",

    [switch]$RestoreOmega,

    [switch]$SkipEngineTests
)

$ErrorActionPreference = "Stop"

function Invoke-CommandChecked {
    param(
        [string]$Command,
        [string[]]$Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Command $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$propsPath = Join-Path $repoRoot "Directory.Build.props"
$solutionPath = Join-Path $repoRoot "TheOmegaStrain.sln"

if (-not (Test-Path -LiteralPath $propsPath)) {
    throw "Directory.Build.props was not found at '$propsPath'."
}

[xml]$props = Get-Content -LiteralPath $propsPath -Raw
$packageVersion = [string]$props.Project.PropertyGroup.RetroMeshEnginePackageVersion
if ([string]::IsNullOrWhiteSpace($packageVersion)) {
    throw "RetroMeshEnginePackageVersion is missing from Directory.Build.props."
}

if ([string]::IsNullOrWhiteSpace($EnginePath)) {
    $EnginePath = Join-Path $repoRoot "RetroMesh"
}
elseif (-not [System.IO.Path]::IsPathRooted($EnginePath)) {
    $EnginePath = Join-Path $repoRoot $EnginePath
}

if (-not (Test-Path -LiteralPath $EnginePath)) {
    Write-Host "RetroMesh checkout not found. Cloning from $EngineRepoUrl..."
    Invoke-CommandChecked -Command "git" -Arguments @("clone", $EngineRepoUrl, $EnginePath)
}

$packageScript = Join-Path $EnginePath "build\Build-RetroMeshEnginePackage.ps1"
if (-not (Test-Path -LiteralPath $packageScript)) {
    throw "RetroMesh package build script was not found at '$packageScript'."
}

$packageArgs = @{
    Configuration = $Configuration
    Version = $packageVersion
}

if ($SkipEngineTests) {
    $packageArgs["SkipTests"] = $true
}

Write-Host "Building RetroMesh.Engine package $packageVersion from '$EnginePath'..."
& $packageScript @packageArgs

$expectedPackage = Join-Path $EnginePath "artifacts\packages\RetroMesh.Engine.$packageVersion.nupkg"
if (-not (Test-Path -LiteralPath $expectedPackage)) {
    throw "Expected package was not produced: '$expectedPackage'."
}

Write-Host "RetroMesh package ready: $expectedPackage"

if ($RestoreOmega) {
    Write-Host "Restoring The Omega Strain solution..."
    Invoke-CommandChecked -Command "dotnet" -Arguments @("restore", $solutionPath)
}
