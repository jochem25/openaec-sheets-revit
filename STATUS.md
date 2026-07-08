# OpenAEC Sheet Exporter — Status

> Laatst bijgewerkt: 2026-07-08

## Huidige fase: v0.1 — eerste werkende versie, wacht op Revit runtime-test

### Voltooid (8 juli)
- Repo-opzet naar het patroon van openaec-bcf-revit (Core/UI/Revit, .NET 8, Revit 2025+)
- **Core:** ExportProfile + 8 formaat-settings, SheetItem, JobBuilder (combine-logica), NamingEngine ({token}-templates + sanitize), ProfileStore (JSON, %APPDATA%)
- **UI:** MainWindow met 3 tabs (Selectie / Formaten / Exporteren), MVVM, OpenAEC-theme, profielenbeheer, zoek/V-S-set-filter, custom bestandsnaam per rij, live exportvoortgang + annuleren
- **Revit:** ribbon-knop op OpenAEC-tab (panel "Sheets"), modeless window + ExternalEvent-bridge, exporters voor PDF (native, vector), DWG/DGN (model-setups), DWF(x), NWC, IFC (incl. psets/AddOptions), IMG, XML (parameters per sheet)
- Tests: 20/20 groen (NamingEngine, ProfileStore, JobBuilder)
- Build: 0 errors

## Bewust buiten scope
- Scheduling assistant

## Volgende stap
- Runtime-test in Revit 2025 met echt 3BM-model (Deploy-Dev.ps1 → knop → export)
- IMG-export: Revit plakt view-suffix aan bestandsnaam — rename-stap toevoegen
- V/S Sets opslaan vanuit de tool (nu alleen filteren)
- Transmittal/manifest-export (JSON naar %TEMP%\3bm_exchange) voor koppeling BM Reports

## Build status
- Core: compileert, 20/20 tests
- UI: compileert (WPF)
- Revit: compileert tegen lokale Revit 2025 API
