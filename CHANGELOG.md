# Changelog — OpenAEC Sheet Exporter

Formaat: [Keep a Changelog](https://keepachangelog.com/nl/1.1.0/), versienummer uit `Directory.Build.props`.

## [0.2.0] — 2026-08-31

### Toegevoegd
- **PDF "Combineer per parameterwaarde" → waarde splitsen.** Nieuwe optie *Waarde splitsen (blad in meerdere boekjes)*: de parameterwaarde wordt gesplitst op scheidingstekens (standaard `;` en `,`, instelbaar) en per deel ontstaat een eigen gecombineerde PDF. Eén blad kan zo in meerdere boekjes belanden, bijv. `plattegronden;plattegronden-noord` → `<prefix>_plattegronden.pdf` én `<prefix>_plattegronden-noord.pdf`.
- Statusregel toont in deze modus het aantal boekjes, het totaal aantal bladpagina's (incl. dubbeltellingen) en het aantal unieke sheets.
- Profielvelden `pdf.split_group_values` (default `false`) en `pdf.group_value_separators` (default `";,"`). Oude profielen zonder deze velden laden ongewijzigd.

### Ongewijzigd (bewust)
- Gedrag zonder splitsen (1 blad = 1 groep), DWF/XML/losse bestanden en het profielformaat.

## [0.1.0] — 2026-07-21
- Eerste release: batch-export naar PDF/DWG/DGN/DWF/NWC/IFC/IMG/XML, profielen, PDF combine-all / combine-per-parameter, standalone installer.
