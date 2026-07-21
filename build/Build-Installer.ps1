<#
.SYNOPSIS
    Bouwt de release-installer (EXE) voor OpenAEC Sheet Exporter.
    Vereist Inno Setup 6 (winget install JRSoftware.InnoSetup).
.OUTPUTS
    installer\output\OpenAEC-SheetExporter-<versie>-Setup.exe
#>
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$PublishDir = Join-Path $RepoRoot "installer\publish"

$Version = ([xml](Get-Content "$RepoRoot\Directory.Build.props")).Project.PropertyGroup.Version
if (-not $Version) { Write-Host "Geen <Version> in Directory.Build.props" -ForegroundColor Red; exit 1 }

$Iscc = @(
    "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $Iscc) { Write-Host "Inno Setup 6 niet gevonden. Installeer via: winget install JRSoftware.InnoSetup" -ForegroundColor Red; exit 1 }

Write-Host "=== Build OpenAEC Sheet Exporter installer v$Version ===" -ForegroundColor Cyan

Write-Host "Publishing..." -ForegroundColor Yellow
dotnet publish "$RepoRoot\src\OpenAEC.Sheets.Revit\OpenAEC.Sheets.Revit.csproj" `
    -c $Configuration -o $PublishDir --no-self-contained --verbosity quiet
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed!" -ForegroundColor Red; exit 1 }

Write-Host "Compiling installer..." -ForegroundColor Yellow
& $Iscc "/DAppVersion=$Version" "/DPublishDir=$PublishDir" "$RepoRoot\installer\OpenAEC.Sheets.iss"
if ($LASTEXITCODE -ne 0) { Write-Host "ISCC failed!" -ForegroundColor Red; exit 1 }

$Setup = Join-Path $RepoRoot "installer\output\OpenAEC-SheetExporter-$Version-Setup.exe"
Write-Host "Klaar: $Setup" -ForegroundColor Green
