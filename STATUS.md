# OpenAEC Sheet Exporter — Status

> Laatst bijgewerkt: 2026-08-31 (v0.2.0 — PDF waarde splitsen: blad in meerdere boekjes)

### Voltooid (31 augustus) — v0.2.0, branch `feature/pdf-split-group-values`
- PDF "Combineer per parameterwaarde" kan de parameterwaarde nu splitsen op scheidingstekens (default `;,`, instelbaar): per token een eigen gecombineerde PDF, één blad kan in meerdere boekjes komen (case 2459 Parkview: `plattegronden;plattegronden-noord`)
- Core: `PdfSettings.SplitGroupValues` / `GroupValueSeparators` (profiel-JSON `split_group_values`, `group_value_separators`; oude profielen laden met defaults), `JobBuilder.GroupedJobs` publiek met split-pad naast het ongewijzigde exclusieve pad
- UI: checkbox + scheidingstekens-veld onder de groepeer-combobox (enabled bij combine-per-parameter); statusregel toont `N boekjes, M bladpagina's (K unieke sheets)`
- Exportlaag ongewijzigd: `RevitGateway` exporteert per job, geen aanname "blad max 1×"
- Tests: 43/43 groen (17 nieuwe: regressie split-uit, tokens/trim/leeg/duplicaat/separators/volgorde/prefix/sanitize/andere formaten, profiel round-trip + legacy-profiel)
- CHANGELOG.md aangemaakt; versie 0.1.0 → 0.2.0 (`Directory.Build.props`, `.iss`)
- Handmatig testscript: `docs/TEST-pdf-split-boekjes.md`
- Deploy-Dev: build + publish OK (0.2.0.0 in `installer/publish`), kopie naar Addins geblokkeerd doordat Revit open stond → herhalen na sluiten Revit
- **Runtime-getest in Revit 2025 op 2459 Parkview Gouda (user-ack): boekjes-split én naamgeving werken.** Boekjesnaam-veld nu ook op de Exporteren-tab naast de template (`5e4b89f`).
- **Naamgeving uitgebreid (zelfde dag, zelfde branch):** vaste tekst + tokens was al mogelijk in de template maar onvindbaar → token-kiezer (Invoegen op cursor), live voorbeeld, betere hint. Nieuwe tokens `{Project Name}` `{Project Number}` `{Document Title}` `{Sheet Set}`; titleblock-instance-parameters ook als token (sheet wint). Tokens ook in boekjesnaam/prefix, DWF-combine en XML; `{Group}` plaatst de groepswaarde. `JobBuilder.Build` kreeg `projectNumber`; `ModelSnapshot.ProjectNumber` nieuw. Tests: 63/63

### Voltooid (21 juli)
- Repo publiek gemaakt op GitHub (MIT-licentie toegevoegd; history vooraf gescand: geen secrets/binaries)
- Standalone installer (Inno Setup 6, per-user, geen admin): `build\Build-Installer.ps1` → `installer\output\OpenAEC-SheetExporter-<versie>-Setup.exe`
- Componenten Revit 2025/2026, auto-aangevinkt op basis van aanwezige Addins-map; uninstaller in Windows Apps-lijst
- Revit-open-detectie (WMI): interactief "sluit Revit"-prompt, silent mode breekt netjes af (getest: exit 1 met Revit open)
- Nog open: volledige install-test zodra Revit dicht is; code-signing voor externe verspreiding

## Huidige fase: v0.1 — runtime-feedback verwerkt + formaat-opties op ProSheets-niveau incl. PDF group-by, IFC fase + category mapping

### Fixes na eerste test in Revit
- Init las het model in 6+ losse ExternalEvent-rondreizen (traag, elk wacht op Revit-idle) → nu één `GetSnapshotAsync()`-call voor sheets, views, sets en setups
- Formaat-tabs waren niet aanklikbaar: de CheckBox-header consumeerde de klik → header is nu checkbox + tekst, en klik op de checkbox selecteert ook de tab

### Voltooid (18 juli)
- Set-keuze in Selectie-tab selecteert nu automatisch precies de inhoud van die set (voorheen alleen filteren)
- Gecombineerd PDF-boekje: bestandsnaam = basisnaam + `_<Project Name>_<printset-naam>` (lege velden vervallen); Project Name uit Project Information meegenomen in de model-snapshot
- Tests: 24/24 groen

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
- Core: compileert, 24/24 tests
- UI: compileert (WPF)
- Revit: compileert tegen lokale Revit 2025 API
