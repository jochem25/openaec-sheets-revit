# OpenAEC Sheet Exporter — Revit Plugin

> Revit 2025+ batch-export van sheets/views naar PDF, DWG, DGN, DWF, NWC, IFC, IMG en XML.
> Open vervanger voor DiRoots ProSheets — zonder exportlimiet of licentie.

## Architectuur

```
OpenAEC.Sheets.Core   — Models, JobBuilder, NamingEngine, ProfileStore, IRevitGateway (geen Revit dep)
OpenAEC.Sheets.UI     — WPF/MVVM: MainWindow (Selectie/Formaten/Exporteren), viewmodels (dep op Core)
OpenAEC.Sheets.Revit  — Revit plugin: App (ribbon), ShowExporterCommand, RevitGateway + exporters
```

## Conventies

- **Target:** .NET 8, Revit 2025+
- **MVVM:** CommunityToolkit.Mvvm (ObservableObject, RelayCommand)
- **JSON:** System.Text.Json, snake_case (profielen in `%APPDATA%\OpenAEC\SheetExporter\Profiles`)
- **Thread safety:** alle Revit API-calls via `ExternalEventHandler` (async bridge, modeless window)
- **Branding:** Amber #D97706, theme in `src/OpenAEC.Sheets.UI/Styles/OpenAecTheme.xaml`
- **Ribbon:** tab "OpenAEC" (gedeeld met BCF-plugin — CreateRibbonTab in try/catch), panel "Sheets"

## Revit API regels

- NOOIT Revit API aanroepen buiten de Revit-thread → ExternalEventHandler
- RevitAPI.dll / RevitAPIUI.dll: CopyLocal=false (lokale install), anders Nice3point NuGet stubs
- IFC-export vereist een open Transaction (commit alleen bij StoreIFCGUID, anders rollback)
- PDF: native `PDFExportOptions` (geen printerdriver); per job Combine=true + FileName voor eigen naamgeving
- Eenheden intern in feet: mm ÷ 304.8

## Naming

`NamingEngine` vervangt `{Parameter Naam}`-tokens door sheetparameters, bijv.
`{Sheet Number}_{Sheet Name}_{Current Revision}`. CustomFileName per rij overschrijft de template.

## Build & deploy

```powershell
dotnet build OpenAEC.Sheets.sln
dotnet test tests/OpenAEC.Sheets.Core.Tests
.\build\Deploy-Dev.ps1          # publish → %APPDATA%\Autodesk\Revit\Addins\2025\OpenAEC.Sheets
```

## Buiten scope (bewust)

- Scheduling assistant (ProSheets-feature) — vereist open Revit-sessie, niet nagebouwd

## Agent Broker
- **project_id:** `sheets-revit`
- **display_name:** `OpenAEC Sheet Exporter`
- **capabilities:** `["revit-plugin", "batch-export", "wpf"]`
- **subscriptions:** `["bim/*", "shared/*"]`
