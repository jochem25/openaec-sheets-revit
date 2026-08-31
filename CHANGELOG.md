# Changelog — OpenAEC Sheet Exporter

Formaat: [Keep a Changelog](https://keepachangelog.com/nl/1.1.0/), versienummer uit `Directory.Build.props`.

## [0.2.0] — 2026-08-31

### Toegevoegd
- **PDF "Combineer per parameterwaarde" → waarde splitsen.** Nieuwe optie *Waarde splitsen (blad in meerdere boekjes)*: de parameterwaarde wordt gesplitst op scheidingstekens (standaard `;` en `,`, instelbaar) en per deel ontstaat een eigen gecombineerde PDF. Eén blad kan zo in meerdere boekjes belanden, bijv. `plattegronden;plattegronden-noord` → `<prefix>_plattegronden.pdf` én `<prefix>_plattegronden-noord.pdf`.
- Statusregel toont in deze modus het aantal boekjes, het totaal aantal bladpagina's (incl. dubbeltellingen) en het aantal unieke sheets.
- Profielvelden `pdf.split_group_values` (default `false`) en `pdf.group_value_separators` (default `";,"`). Oude profielen zonder deze velden laden ongewijzigd.
- **Naamgeving: vaste tekst + tokens overal.** De naamtemplate combineert vrij vaste tekst en `{tokens}` (bijv. `TO_{Sheet Number}_{Sheet Name}`); dat kon al, maar is nu zichtbaar met een token-kiezer (*Invoegen* op de cursorpositie), een live voorbeeld voor het eerste geselecteerde blad en een duidelijkere hint.
- **Nieuwe tokens.** Document-tokens `{Project Name}`, `{Project Number}`, `{Document Title}`, `{Sheet Set}` (sheetparameter met dezelfde naam wint). Instance-parameters van het titleblock zijn nu ook token (bijv. een fase- of stempelparameter die op het titleblock staat i.p.v. op de sheet); de sheet wint bij gelijke naam.
- **Bladen 1× renderen, boekjes samenstellen.** Zit een blad in meerdere boekjes (voorblad in 77 woningboekjes), dan rendert Revit elk blad nog maar één keer naar een tijdelijke PDF; de boekjes worden daarna samengesteld door pagina's te mergen (PDFsharp, MIT), met een bookmark per blad. Zonder overlap verandert er niets (native export); mislukt het samenstellen van een boekje, dan wordt dat boekje alsnog native door Revit geëxporteerd. Instelling `pdf.assemble_booklets` (default `true`), checkbox *Bladen 1× renderen, boekjes samenstellen*. De jobs-grid toont `PDF (blad 1×)` en `PDF (boekje)`.
- **Wildcards in de groepswaarde (blad in meerdere/alle boekjes).** Met *Waarde splitsen* mag een gesplitst deel een glob-patroon zijn: `*` = 0 of meer tekens, `?` = 1 teken. Patronen worden geëxpandeerd tegen de concrete boekjesnamen van de selectie (hoofdletterongevoelig): een voorblad met `*` komt in elk boekje, een situatietekening met `Z_*` in alle woningboekjes, `*_E1;*_E2` in de E1- en E2-boekjes. Een patroon maakt nooit zelf een boekje aan; zonder match volgt de melding "patroon 'X' matcht geen enkel boekje" in de statusregel en valt het blad in *overig*. Instelling `pdf.expand_wildcards` (default `true`), UI-checkbox *Wildcards expanderen (\* en ?)*.
- **Boekjesnaam naast de naamtemplate.** Op de Exporteren-tab staat nu ook het veld *Boekjesnaam* (hetzelfde als "Bestandsnaam / prefix" op de PDF-tab; actief bij gecombineerde PDF's). Losse bestanden volgen de naamtemplate, gecombineerde PDF's de boekjesnaam — het voorbeeld toont beide. De token-kiezer voegt in het veld in waar je het laatst typte; `{Group}` staat in de lijst.
- **Tokens in boekjesnamen.** "Bestandsnaam / prefix" bij *Combineer alles* en *Combineer per parameterwaarde*, DWF-combine en XML accepteren tokens (opgelost via het eerste blad). Bevat de boekjesnaam tokens, dan wordt niets automatisch achtergevoegd; met `{Group}` bepaal je zelf waar de groepswaarde in de naam komt (anders `prefix_groep`).

### Ongewijzigd (bewust)
- Gedrag zonder splitsen (1 blad = 1 groep), DWF/XML/losse bestanden en het profielformaat.

## [0.1.0] — 2026-07-21
- Eerste release: batch-export naar PDF/DWG/DGN/DWF/NWC/IFC/IMG/XML, profielen, PDF combine-all / combine-per-parameter, standalone installer.
