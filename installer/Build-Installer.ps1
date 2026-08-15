[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.0",
    [string]$SecretsPath = "",
    [string]$InnoCompiler = "",
    [switch]$SkipPublish,
    [switch]$SkipInno
)

$ErrorActionPreference = "Stop"

function Resolve-InnoCompiler {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) {
            throw "Inno compiler not found at '$ExplicitPath'."
        }

        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidatePaths = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    )

    foreach ($candidate in $candidatePaths) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw "Inno Setup 6 was not found. Install Inno Setup, or pass -InnoCompiler with the full path to ISCC.exe."
}

function Test-SecretsFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "secrets.json was not found at '$Path'."
    }

    $json = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace($json.SupabaseUrl)) {
        throw "secrets.json is missing SupabaseUrl."
    }

    if ([string]::IsNullOrWhiteSpace($json.SupabaseAnonKey)) {
        throw "secrets.json is missing SupabaseAnonKey."
    }
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "TheOmegaStrain.Wpf\TheOmegaStrain.Wpf.csproj"
$installerScript = Join-Path $PSScriptRoot "TheOmegaStrain.iss"
$publishDir = Join-Path $repoRoot "artifacts\installer\publish\$Runtime"
$outputDir = Join-Path $repoRoot "artifacts\installer\output"
$stagingDir = Join-Path $repoRoot "artifacts\installer\staging"

if ([string]::IsNullOrWhiteSpace($SecretsPath)) {
    $SecretsPath = Join-Path $env:APPDATA "OmegaStrain\secrets.json"
}

$SecretsPath = (Resolve-Path -LiteralPath $SecretsPath).Path
Test-SecretsFile -Path $SecretsPath

New-Item -ItemType Directory -Force -Path $publishDir, $outputDir, $stagingDir | Out-Null

if (-not $SkipPublish) {
    dotnet publish $projectPath `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -o $publishDir `
        /p:Version=$Version `
        /p:PublishSingleFile=false `
        /p:PublishReadyToRun=false
}

$mainExe = Join-Path $publishDir "TheOmegaStrain.exe"
if (-not (Test-Path -LiteralPath $mainExe)) {
    throw "Published executable not found at '$mainExe'. Run without -SkipPublish first."
}

$stagedSecrets = Join-Path $stagingDir "secrets.json"
if ($SecretsPath -ne (Resolve-Path -LiteralPath $stagedSecrets -ErrorAction SilentlyContinue).Path) {
    Copy-Item -LiteralPath $SecretsPath -Destination $stagedSecrets -Force
}

if ($SkipInno) {
    Write-Host "Publish and secrets staging completed."
    Write-Host "PublishDir: $publishDir"
    Write-Host "StagedSecrets: $stagedSecrets"
    Write-Host "Skipped Inno compilation."
    return
}

$iscc = Resolve-InnoCompiler -ExplicitPath $InnoCompiler

$isccArgs = @(
    "/DAppVersion=$Version",
    "/DPublishDir=$publishDir",
    "/DOutputDir=$outputDir",
    "/DSecretsSource=$stagedSecrets",
    $installerScript
)

& $iscc @isccArgs
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compiler failed with exit code $LASTEXITCODE."
}

Write-Host "Installer created in: $outputDir"

$outputBaseName = "TheOmegaStrainSetup-$Version"
$installerFiles = Get-ChildItem -LiteralPath $outputDir -File |
    Where-Object { $_.BaseName -eq $outputBaseName -or $_.BaseName -like "$outputBaseName-*" } |
    Sort-Object Name

if ($installerFiles) {
    Write-Host "Installer files:"
    foreach ($file in $installerFiles) {
        Write-Host "  $($file.FullName)"
    }
}
