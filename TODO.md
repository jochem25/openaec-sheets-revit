# TODO — OpenAEC Sheet Exporter

## v0.2 — PDF waarde splitsen (branch `feature/pdf-split-group-values`, 2026-08-31)
- [x] Core: `SplitGroupValues` + `GroupValueSeparators` in PdfSettings, split-pad in `JobBuilder.GroupedJobs`
- [x] UI: checkbox + scheidingstekens, statusregel met boekjes-telling
- [x] Tests 43/43, CHANGELOG, versie 0.2.0
- [ ] `Deploy-Dev.ps1` herhalen met Revit gesloten (kopie naar Addins was geblokkeerd)
- [ ] Handmatige test volgens `docs/TEST-pdf-split-boekjes.md` op 2459 Parkview Gouda
- [ ] Na runtime-test: merge naar master + installer bouwen (`Build-Installer.ps1`)
- [ ] Optioneel: groepeer-combobox via VM-property laten lopen zodat de boekjes-telling ook bij parameterwissel direct ververst (nu bij selectie/checkbox/Vernieuwen)

## v0.1 → v0.2
- [ ] Runtime-test in Revit 2025 met 3BM-model (alle 8 formaten)
- [ ] Runtime-test set-autoselectie + boekjes-naamgeving (2026-07-18; deploy wachtte op gesloten Revit)
- [ ] IMG-export: bestandsnaam-suffix van Revit wegwerken (rename na export)
- [ ] PDF paper size/orientation override per sheet (nu: sheet-formaat = Default)
- [ ] Foutafhandeling NWC zonder Navisworks-exporter → nette melding
- [ ] Iconen per formaat-tab (nu alleen tekst)
- [ ] Live preview van bestandsnaam in Selectie-tab

## v0.3+
- [ ] V/S Sets aanmaken/opslaan vanuit de tool
- [ ] Revisie-filter ("alleen sheets met revisie X")
- [ ] Export-manifest JSON naar %TEMP%\3bm_exchange voor BM Reports transmittal

- [x] Installer (EXE, Inno Setup, per-user) — `build\Build-Installer.ps1` (2026-07-21)
- [ ] Installer: volledige install-test na sluiten Revit (Revit-open-check getest, file-deploy nog niet)
- [ ] Versie-check / update-notificatie in de plugin
- [ ] Installer code-signing (nu unsigned → SmartScreen-waarschuwing bij externe verspreiding)
- [ ] Profielen delen via repo/share i.p.v. alleen %APPDATA%

## Bewust niet
- Scheduling assistant (buiten scope besloten 2026-07-08)
