# TODO — OpenAEC Sheet Exporter

## v0.2 — PDF waarde splitsen (branch `feature/pdf-split-group-values`, 2026-08-31)
- [x] Core: `SplitGroupValues` + `GroupValueSeparators` in PdfSettings, split-pad in `JobBuilder.GroupedJobs`
- [x] UI: checkbox + scheidingstekens, statusregel met boekjes-telling
- [x] Tests 43/43, CHANGELOG, versie 0.2.0
- [x] Geïnstalleerd in `%APPDATA%\Autodesk\Revit\Addins\2025\OpenAEC.Sheets` (0.2.0.0, hash-geverifieerd) terwijl Revit open stond
- [ ] Na Revit-herstart: `*.old.<pid>`-restanten in de plugin-map verwijderen of gewoon `Deploy-Dev.ps1` draaien
- [x] Naamgeving: vaste tekst + tokens zichtbaar gemaakt (token-kiezer, live voorbeeld), document-tokens, titleblock-parameters als token, tokens in boekjesnaam (`{Group}`) — 63/63 tests
- [x] Runtime-test op 2459 Parkview Gouda: boekjes-split + naamtemplate/boekjesnaam met tokens — user-ack "werkt goed" (2026-08-31)
- [x] Wildcards (`*`, `?`) in de groepswaarde: voorblad in elk boekje, `Z_*` in alle woningboekjes; melding bij patroon zonder match — 85/85 tests
- [x] Wildcards runtime-getest door user ("werkt perfect")
- [x] Bladen 1× renderen + boekjes samenstellen (PDFsharp) i.p.v. 77× hetzelfde blad exporteren — 94/94 tests
- [x] Samenstellen runtime-getest door user ("werkt allemaal goed")
- [x] Batch-render: unieke bladen in één Export-call (naming rule sheetnummer) + exporttimer in statusregel
- [x] Batch+timer runtime-getest door user ("het gaat nu wel sneller")
- [x] Token-kiezer volgt Sheets/Views: viewparameters lazy ingelezen bij eerste wissel naar Views
- [x] View-tokens runtime-getest door user ("het werkt")
- [ ] Na runtime-test: merge naar master + installer bouwen (`Build-Installer.ps1`)
- [ ] Optioneel: groepeer-combobox via VM-property laten lopen zodat de boekjes-telling ook bij parameterwissel direct ververst (nu bij selectie/checkbox/Vernieuwen)

## v0.1 → v0.2
- [ ] Runtime-test in Revit 2025 met 3BM-model (alle 8 formaten)
- [ ] Runtime-test set-autoselectie + boekjes-naamgeving (2026-07-18; deploy wachtte op gesloten Revit)
- [ ] IMG-export: bestandsnaam-suffix van Revit wegwerken (rename na export)
- [ ] PDF paper size/orientation override per sheet (nu: sheet-formaat = Default)
- [ ] Foutafhandeling NWC zonder Navisworks-exporter → nette melding
- [ ] Iconen per formaat-tab (nu alleen tekst)
- [x] ~~Live preview van bestandsnaam~~ → voorbeeldregel onder de naamtemplate (tab Exporteren, 2026-08-31)

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
