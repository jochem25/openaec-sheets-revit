# Changelog — OpenAEC Sheet Exporter

Formaat: [Keep a Changelog](https://keepachangelog.com/nl/1.1.0/), versienummer uit `Directory.Build.props`.

## [0.3.0] — 2026-09-04

### Toegevoegd
- **Tabblad "Printsets".** Revit View/Sheet Sets (printsets) aanmaken uit filters op sheetparameters. Filterbouwer met stapelbare regels (parameter | operator | waarde) en EN/OF-combinatie; operators: is (niet) gelijk aan, bevat, begint/eindigt met, wildcard (`*`/`?`), is (niet) leeg, in lijst (`;` of `,`). Filterbron: alle sheet- en titleblock-parameters plus Sheet Number/Name, Current Revision en Formaat.
- Live voorbeeld met alle sheets en checkboxes: afwijkingen van het filter worden als handmatige uitzonderingen op de definitie bewaard.
- **Bulk-generatie:** "set per unieke waarde van parameter X" (bijv. per bouwdeel één set), met waarde-splitsen op `;`/`,` (zelfde logica als de boekjes) en `{Group}` in de naamtemplate.
- Bestaande set **overschrijven** of **alleen aanvullen**; sets verwijderen uit Revit (met bevestiging); "Alle sets verversen" genereert alle opgeslagen definities opnieuw — handig na nieuwe sheets.
- Filterdefinities worden in het exportprofiel bewaard (JSON `print_sets`); oude profielen laden ongewijzigd. Nieuw aangemaakte sets verschijnen direct in de set-keuze op het tabblad Selectie.

## [0.2.1] — 2026-09-04

### Gewijzigd
- PDF-default rasterkwaliteit is nu **Presentation** (was High). Bestaande profielen behouden hun opgeslagen waarde; alleen nieuwe profielen krijgen de nieuwe default.
- Het exportvoorbeeld toont geen `PDF (blad 1×)`-rijen meer (tijdelijke per-blad renders bij boekjes-assemblage). De regel "(X bladen worden 1× gerenderd en tot boekjes samengesteld)" blijft als uitleg staan; aan de export zelf verandert niets.

## [0.2.0] — 2026-08-31

### Toegevoegd
- **PDF "Combineer per parameterwaarde" → waarde splitsen.** Nieuwe optie *Waarde splitsen (blad in meerdere boekjes)*: de parameterwaarde wordt gesplitst op scheidingstekens (standaard `;` en `,`, instelbaar) en per deel ontstaat een eigen gecombineerde PDF. Eén blad kan zo in meerdere boekjes belanden, bijv. `plattegronden;plattegronden-noord` → `<prefix>_plattegronden.pdf` én `<prefix>_plattegronden-noord.pdf`.
- Statusregel toont in deze modus het aantal boekjes, het totaal aantal bladpagina's (incl. dubbeltellingen) en het aantal unieke sheets.
- Profielvelden `pdf.split_group_values` (default `false`) en `pdf.group_value_separators` (default `";,"`). Oude profielen zonder deze velden laden ongewijzigd.
- **Naamgeving: vaste tekst + tokens overal.** De naamtemplate combineert vrij vaste tekst en `{tokens}` (bijv. `TO_{Sheet Number}_{Sheet Name}`); dat kon al, maar is nu zichtbaar met een token-kiezer (*Invoegen* op de cursorpositie), een live voorbeeld voor het eerste geselecteerde blad en een duidelijkere hint.
- **Nieuwe tokens.** Document-tokens `{Project Name}`, `{Project Number}`, `{Document Title}`, `{Sheet Set}` (sheetparameter met dezelfde naam wint). Instance-parameters van het titleblock zijn nu ook token (bijv. een fase- of stempelparameter die op het titleblock staat i.p.v. op de sheet); de sheet wint bij gelijke naam.
- **Viewparameters als token.** De token-kiezer volgt de Sheets/Views-schakelaar: bij Views toont hij de viewparameters (schaal, discipline, fase, …) en resolven die tokens ook in de bestandsnamen. Viewparameters worden pas bij de eerste wissel naar Views ingelezen, zodat het openen van de tool snel blijft; het naamvoorbeeld volgt de actieve lijst.
- **Batch-render + exporttimer.** De eenmalig te renderen bladen gaan nu in één Revit-`Export`-call (Combine uit + naming rule op sheetnummer) i.p.v. één API-rondreis per blad — scheelt honderden idle-waits bij grote runs; bladen die de batch niet oplevert (views, naamconflict) worden automatisch alsnog per stuk gerenderd. De statusregel toont de verstreken tijd tijdens de export en sluit af met "Klaar in 3:24 min — …" (geen popup).
- **Bladen 1× renderen, boekjes samenstellen.** Zit een blad in meerdere boekjes (voorblad in 77 woningboekjes), dan rendert Revit elk blad nog maar één keer naar een tijdelijke PDF; de boekjes worden daarna samengesteld door pagina's te mergen (PDFsharp, MIT), met een bookmark per blad. Zonder overlap verandert er niets (native export); mislukt het samenstellen van een boekje, dan wordt dat boekje alsnog native door Revit geëxporteerd. Instelling `pdf.assemble_booklets` (default `true`), checkbox *Bladen 1× renderen, boekjes samenstellen*. De jobs-grid toont `PDF (blad 1×)` en `PDF (boekje)`.
- **Wildcards in de groepswaarde (blad in meerdere/alle boekjes).** Met *Waarde splitsen* mag een gesplitst deel een glob-patroon zijn: `*` = 0 of meer tekens, `?` = 1 teken. Patronen worden geëxpandeerd tegen de concrete boekjesnamen van de selectie (hoofdletterongevoelig): een voorblad met `*` komt in elk boekje, een situatietekening met `Z_*` in alle woningboekjes, `*_E1;*_E2` in de E1- en E2-boekjes. Een patroon maakt nooit zelf een boekje aan; zonder match volgt de melding "patroon 'X' matcht geen enkel boekje" in de statusregel en valt het blad in *overig*. Instelling `pdf.expand_wildcards` (default `true`), UI-checkbox *Wildcards expanderen (\* en ?)*.
- **Boekjesnaam naast de naamtemplate.** Op de Exporteren-tab staat nu ook het veld *Boekjesnaam* (hetzelfde als "Bestandsnaam / prefix" op de PDF-tab; actief bij gecombineerde PDF's). Losse bestanden volgen de naamtemplate, gecombineerde PDF's de boekjesnaam — het voorbeeld toont beide. De token-kiezer voegt in het veld in waar je het laatst typte; `{Group}` staat in de lijst.
- **Tokens in boekjesnamen.** "Bestandsnaam / prefix" bij *Combineer alles* en *Combineer per parameterwaarde*, DWF-combine en XML accepteren tokens (opgelost via het eerste blad). Bevat de boekjesnaam tokens, dan wordt niets automatisch achtergevoegd; met `{Group}` bepaal je zelf waar de groepswaarde in de naam komt (anders `prefix_groep`).

### Ongewijzigd (bewust)
- Gedrag zonder splitsen (1 blad = 1 groep), DWF/XML/losse bestanden en het profielformaat.

## [0.1.0] — 2026-07-21
- Eerste release: batch-export naar PDF/DWG/DGN/DWF/NWC/IFC/IMG/XML, profielen, PDF combine-all / combine-per-parameter, standalone installer.
