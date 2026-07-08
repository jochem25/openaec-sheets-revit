<#
.SYNOPSIS
    Deploy plugin naar per-user Revit Addins folder (geen admin vereist).
    Ideaal voor development en testen.
#>
param(
    [string]$Configuration = "Release",
    [string]$RevitVersion = "2025"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$PublishDir = Join-Path $RepoRoot "installer\publish"
$AddinsDir = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
$PluginDir = Join-Path $AddinsDir "OpenAEC.Sheets"

Write-Host "=== Deploy OpenAEC Sheet Exporter (dev) ===" -ForegroundColor Cyan

Write-Host "Building..." -ForegroundColor Yellow
dotnet publish "$RepoRoot\src\OpenAEC.Sheets.Revit\OpenAEC.Sheets.Revit.csproj" `
    -c $Configuration -o $PublishDir --no-self-contained --verbosity quiet

if ($LASTEXITCODE -ne 0) { Write-Host "Build failed!" -ForegroundColor Red; exit 1 }

New-Item -ItemType Directory -Path $PluginDir -Force | Out-Null

Copy-Item "$RepoRoot\installer\OpenAEC.Sheets.Revit.addin" "$AddinsDir\" -Force
Copy-Item "$PublishDir\*" "$PluginDir\" -Force -Recurse

Write-Host "Deployed to: $PluginDir" -ForegroundColor Green
Write-Host "Start Revit $RevitVersion om te testen." -ForegroundColor Cyan
