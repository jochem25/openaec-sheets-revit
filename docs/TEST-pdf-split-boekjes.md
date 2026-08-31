# Handmatige test — PDF "Combineer per parameterwaarde" met waarde splitsen

**Versie:** 0.2.0 · **Revit:** 2025 · **Duur:** ±10 min
**Doel:** één blad belandt in meerdere gecombineerde PDF's (boekjes) op basis van een
gesplitste tekstparameter.

## Voorbereiding

1. Revit sluiten → `.\build\Deploy-Dev.ps1` (Revit houdt anders de DLL vast) → Revit 2025 starten.
2. Open een testmodel (of 2459 Parkview Gouda) met minimaal 6 sheets.
3. Manage → Project Parameters → nieuw **tekst**-parameter `Boekje`, categorie *Sheets*, instance.
4. Vul `Boekje` in (Sheet Properties of via een sheet-schedule):

| Sheet | Boekje | Verwacht in |
|-------|--------|-------------|
| TO-100n | `plattegronden;plattegronden-noord` | plattegronden, plattegronden-noord |
| TO-100z | `plattegronden;plattegronden-zuid` | plattegronden, plattegronden-zuid |
| TO-100t | `plattegronden;plattegronden-noord;plattegronden-zuid` | alle drie |
| TO-200 | ` gevels , doorsneden ` (met spaties, komma) | gevels, doorsneden |
| TO-300 | `details;;details` | alleen `details`, 1× |
| TO-900 | *(leeg)* | overig |

## Stappen

1. Ribbon **OpenAEC → Sheets** → tab *Selectie*: selecteer de 6 sheets.
2. Tab *Formaten* → **PDF** aanvinken → groep *Bestand*:
   - radio **Combineer per parameterwaarde** → combobox `Boekje`
   - ☑ **Waarde splitsen (blad in meerdere boekjes)** — pas aanklikbaar nadat de radio actief is
   - Scheidingstekens: laat `;,` staan
   - Bestandsnaam / prefix: `2459`
3. Controleer de **statusregel onderin**:
   `6 sheets en 0 views geselecteerd — 7 boekjes, 11 bladpagina's (6 unieke sheets)`
   (plattegronden 3 + noord 2 + zuid 2 + gevels 1 + doorsneden 1 + details 1 + overig 1 = 11)
4. Tab *Exporteren* → **Vernieuwen**: 7 regels verwacht (zie tabel hieronder). Let op: TO-100t staat in 3 jobs.
5. Kies exportmap → **Exporteren**.

## Verwacht resultaat (map / PDF-submap bij "map per formaat")

| Bestand | Pagina's | Sheets |
|---------|----------|--------|
| `2459_details.pdf` | 1 | TO-300 |
| `2459_doorsneden.pdf` | 1 | TO-200 |
| `2459_gevels.pdf` | 1 | TO-200 |
| `2459_overig.pdf` | 1 | TO-900 |
| `2459_plattegronden.pdf` | 3 | TO-100n, TO-100z, TO-100t (selectievolgorde) |
| `2459_plattegronden-noord.pdf` | 2 | TO-100n, TO-100t |
| `2459_plattegronden-zuid.pdf` | 2 | TO-100z, TO-100t |

Checks:
- [ ] Alle 7 PDF's aanwezig, geen foutregels in de statuskolom
- [ ] TO-100t staat in 3 PDF's; TO-300 maar 1× in `details`
- [ ] Geen `2459_.pdf` of lege PDF's (lege tokens genegeerd)
- [ ] Labels zonder voor-/naspaties (`gevels`, niet ` gevels`)

## Regressie (split uit)

6. ☐ **Waarde splitsen** uitzetten → *Vernieuwen*: nu **6 jobs**, elk blad 1×, labels zijn de hele
   waarde (`plattegronden;plattegronden-noord` wordt gesanitized tot een bestandsnaam met `;`).
   Statusregel toont géén boekjes-telling meer.
7. Radio **Losse bestanden** → 6 losse PDF's, ongewijzigd gedrag.

## Profiel

8. Profiel opslaan als `test-boekjes` → Revit herstarten → profiel laden: checkbox en scheidingstekens
   staan weer zoals opgeslagen. Bestand `%APPDATA%\OpenAEC\SheetExporter\Profiles\test-boekjes.json`
   bevat `"split_group_values": true` en `"group_value_separators": ";,"`.
9. Een profiel van vóór 0.2.0 laden → laadt zonder fout, splitsen staat uit.

## Wildcards: voorblad in elk boekje, situatietekening in alle woningboekjes

Voeg toe aan de tabel hierboven (of aan het Parkview-model):

| Sheet | Boekje | Verwacht in |
|-------|--------|-------------|
| TO-000 voorblad | `*` | **elk** boekje, als eerste blad (selectievolgorde) |
| TO-001 situatie | `plattegronden-*` | plattegronden-noord, plattegronden-zuid (niet in `plattegronden`) |
| TO-002 legenda | `*;details` | elk boekje, 1× in details |
| TO-003 test | `bestaat-*` | nergens matcht → in *overig*, statusregel meldt `patroon 'bestaat-*' matcht geen enkel boekje` |

18. PDF-tab: *Waarde splitsen* aan, ☑ **Wildcards expanderen (\* en ?)** (staat standaard aan; alleen actief bij splitsen).
19. Selecteer alle 10 sheets → statusregel: boekjes-telling stijgt (voorblad telt in elk boekje mee) + de waarschuwing voor `bestaat-*`.
20. *Vernieuwen* → géén regel met `*` in de bestandsnaam; `2459_plattegronden-noord.pdf` bevat TO-000, TO-100n, TO-001, TO-100t, TO-002 (in selectievolgorde).
21. Exporteren → open `2459_details.pdf`: voorblad, TO-300, legenda — legenda staat er 1×.
22. Checkbox uit → `*`, `plattegronden-*` enz. worden letterlijke groepen (bestandsnaam met `-` i.p.v. `*` door Sanitize); geen waarschuwing.

## Samenstellen: bladen 1× renderen

23. PDF-tab: ☑ **Bladen 1× renderen, boekjes samenstellen** (standaard aan). Selectie met voorblad `*` + ≥ 3 boekjes.
    *Vernieuwen* → grid toont eerst rijen `PDF (blad 1×)` (één per uniek blad, selectievolgorde), daarna
    `PDF (boekje)`; voortgangstekst: `N bestanden te exporteren (M bladen worden 1× gerenderd en tot boekjes samengesteld)`.
24. Exporteren → alleen de boekjes staan in de uitvoermap (geen `page_*.pdf`); `%TEMP%\OpenAEC.Sheets\` is leeg na afloop.
    Open een boekje: paginavolgorde = selectievolgorde, bladwijzerpaneel toont per blad `nummer - naam`.
    Vergelijk visueel met een native export (checkbox uit): vectorlijnen, kleuren en bladformaat identiek.
25. Tijdmeting: zelfde selectie met checkbox uit vs aan — bij 77 boekjes met voorblad moet "aan" ruwweg
    (unieke bladen + 77 merges) kosten i.p.v. (77 × pagina's) renders.
26. Fallback: checkbox aan, wis tijdens de export handmatig een `page_*.pdf` uit de tempmap → dat boekje krijgt
    toch een bestand (native), log `%TEMP%\OpenAEC.Sheets.log` meldt "Samenstellen van '…' mislukt, terugvallen op native export".
    Zonder overlap (geen `*`, geen dubbel blad) verschijnen géén `(blad 1×)`-rijen: gedrag als voorheen.

## Batch-render en timer

27. Zelfde selectie als stap 23 → Exporteren. Statusregel begint met `N bladen renderen (één batch)…`,
    daarna lopen de `(blad 1×)`-rijen vrijwel direct op ✓ (ze zijn al gerenderd door de batch);
    achter elke voortgangsmelding loopt de verstreken tijd mee (`… — 1:47 min`).
28. Na afloop: `Klaar in M:SS min — N bestanden geëxporteerd naar …` in de statusregel (geen popup).
    Log `%TEMP%\OpenAEC.Sheets.log`: regel `Batch-export: X/X bladen gematcht`.
29. Tijdsvergelijking t.o.v. de vorige run (zelfde selectie): batch hoort merkbaar sneller te zijn;
    matcht een blad niet (bijv. twee sheetnummers die alleen in leestekens verschillen), dan wordt dat
    blad per stuk gerenderd — trager, maar zelfde resultaat.

## Naamgeving: vaste tekst, tokens, boekjesnaam

10. Tab *Exporteren* → **Naamtemplate** leegmaken en typen: `TO_` → de voorbeeldregel eronder toont
    direct `Voorbeeld (TO-100n): TO` (vaste tekst blijft staan; `_` aan het eind wordt door Sanitize weggehaald).
11. Token-kiezer: kies `Sheet Number` → **Invoegen** → template `TO_{Sheet Number}`, voorbeeld `TO_TO-100n`.
    Cursor staat achter het token; nogmaals invoegen plakt op de cursorpositie.
12. Kies in de kiezer `tekening_fase` (of de fase-parameter zoals die in dit model heet — staat hij op het
    titleblock i.p.v. op de sheet, dan staat hij er nu óók tussen) → template `{tekening_fase}_{Sheet Number}`
    → voorbeeld `TO_TO-100n`.
13. Document-tokens: `{Project Number}_{Sheet Number}` → voorbeeld begint met het projectnummer uit
    Project Information (`2459_…`). `{Sheet Set}` is leeg zolang *Alle sheets/views* actief is en krijgt
    de setnaam zodra je een set kiest.
14. Radio **Losse bestanden** → *Vernieuwen* → kolom Bestandsnaam volgt de template voor elk blad.
15. Boekjesnaam met tokens: radio **Combineer alles**, Bestandsnaam `{Project Number}_{tekening_fase}_boekje`
    → *Vernieuwen* → precies `2459_TO_boekje` (géén projectnaam/set automatisch erachter).
    Zonder tokens (`2459`) blijft het `2459_<Project Name>_<set>` zoals voorheen.
16. Prefix met `{Group}`: radio **Combineer per parameterwaarde** + splitsen aan, prefix
    `{Group}_{Project Number}` → bestanden `plattegronden_2459.pdf`, `plattegronden-noord_2459.pdf`, …
    Prefix `{Project Number}` (zonder `{Group}`) → `2459_plattegronden.pdf` enz.

17. Staat de PDF-modus op *Combineer alles* of *per parameterwaarde*, dan is op de Exporteren-tab het
    veld **Boekjesnaam** actief (zelfde veld als "Bestandsnaam / prefix" op de PDF-tab). Typ
    `{tekening_fase}_wijzA_{Group}` (of klik in het veld en gebruik de token-kiezer) → voorbeeldregel
    toont `Boekje: TO+_wijzA_doorsneden`; *Voorbeeld vernieuwen* → kolom Bestandsnaam
    `TO+_wijzA_doorsneden`, `TO+_wijzA_doorsneden-noord`, … en de export schrijft die namen.
    Zonder `{Group}` (bijv. `TO_wijzA`) wordt de groepswaarde erachter gezet: `TO_wijzA_doorsneden`.
    Bij *Losse bestanden* is het veld uitgeschakeld en geldt de naamtemplate.

Checks:
- [ ] Voorbeeldregel loopt live mee tijdens typen en na wijzigen van de selectie
- [ ] In boekjes-modus is zichtbaar dat de template niet geldt en welke boekjesnaam er komt
- [ ] Token-kiezer bevat bovenaan Project Name / Project Number / Document Title / Sheet Set, daarna alle sheet- én titleblock-parameters
- [ ] Profiel opslaan/laden bewaart de template; oud profiel laadt met zijn eigen template

## Bekende aandachtspunten

- Groepssleutels zijn bij splitsen case-insensitive (`Noord` en `noord` → één boekje, label van het eerst
  geselecteerde blad); Windows-bestandsnamen zouden anders botsen.
- Bij "Combineer per parameterwaarde" is de combobox direct aan het profiel gebonden; de boekjes-telling
  in de statusregel ververst bij selectie-wijziging, bij het (un)checken van splitsen en bij *Vernieuwen*.
