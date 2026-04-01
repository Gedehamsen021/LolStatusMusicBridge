param(
    [string]$Version = "1.0.0",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$SelfContained = $true
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "LolStatusMusicBridge\LolStatusMusicBridge.csproj"
$artifactRoot = Join-Path $repoRoot "artifacts"
$stagingRoot = Join-Path $artifactRoot "publish"
$releaseRoot = Join-Path $artifactRoot "release"
$publishFolderName = "LolStatusMusicBridge-$RuntimeIdentifier"
$publishOutput = Join-Path $stagingRoot $publishFolderName
$zipName = "LolStatusMusicBridge-$Version-$RuntimeIdentifier.zip"
$zipPath = Join-Path $releaseRoot $zipName

New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null
New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null

if (Test-Path $publishOutput) {
    Remove-Item -LiteralPath $publishOutput -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

$publishArguments = @(
    "publish",
    $projectPath,
    "-c", "Release",
    "-r", $RuntimeIdentifier,
    "--output", $publishOutput,
    "-p:Version=$Version",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
)

Write-Host "Restoring runtime-specific assets..."
dotnet restore $projectPath -r $RuntimeIdentifier

if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

if ($SelfContained) {
    $publishArguments += @("--self-contained", "true", "-p:SelfContained=true")
}
else {
    $publishArguments += @("--self-contained", "false", "-p:SelfContained=false")
}

Write-Host "Publishing release..."
dotnet @publishArguments

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path $publishOutput)) {
    throw "Publish output folder was not created: $publishOutput"
}

$readmeSource = Join-Path $repoRoot "README.md"
$readmeTarget = Join-Path $publishOutput "README.md"
Copy-Item -LiteralPath $readmeSource -Destination $readmeTarget -Force

Compress-Archive -Path (Join-Path $publishOutput "*") -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "Publish output: $publishOutput"
Write-Host "Release zip:    $zipPath"
