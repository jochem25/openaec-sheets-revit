using OpenAEC.Sheets.Core.Naming;

namespace OpenAEC.Sheets.Core.Models;

/// <summary>
/// Eén uit te voeren exportactie: één of meer views/sheets naar één bestand.
/// </summary>
public sealed class ExportJob
{
    public ExportFormat Format { get; init; }

    /// <summary>Revit ElementId.Value's van de views/sheets in deze job.</summary>
    public List<long> ElementIds { get; init; } = [];

    /// <summary>Bestandsnaam zonder extensie.</summary>
    public string FileName { get; init; } = "";

    /// <summary>Bronitem — null bij gecombineerde jobs (combine PDF/DWF, XML-document).</summary>
    public SheetItem? Item { get; init; }

    /// <summary>Groepslabel bij combine-per-parameter, bijv. de bouwdeel-waarde.</summary>
    public string? GroupLabel { get; init; }

    public string DisplayNumber => Item?.Number ?? "—";
    public string DisplayName => Item?.Name ?? GroupLabel ?? "(gecombineerd)";
}

public static class JobBuilder
{
    private static readonly IReadOnlyDictionary<string, string> NoValues =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Bouwt de lijst exportjobs uit de selectie en het profiel.
    /// Combine-formaten (PDF/DWF/XML) leveren één job voor alle items, anders één job per item.
    /// Naast sheetparameters zijn in elke naam de document-tokens bruikbaar:
    /// {Project Name}, {Project Number}, {Document Title}, {Sheet Set}.
    /// </summary>
    public static List<ExportJob> Build(
        IReadOnlyList<SheetItem> items, ExportProfile profile, string documentTitle,
        string projectName = "", string? sheetSetName = null, string projectNumber = "")
    {
        var jobs = new List<ExportJob>();
        if (items.Count == 0) return jobs;

        var docTokens = DocumentTokens(documentTitle, projectName, projectNumber, sheetSetName);

        foreach (var format in profile.EnabledFormats)
        {
            switch (format)
            {
                case ExportFormat.Pdf when profile.Pdf.FileMode == PdfFileMode.CombineAll:
                    var bookletName = BookletName(
                        profile.Pdf.CombinedFileName, documentTitle, projectName, sheetSetName, items[0], docTokens);
                    jobs.Add(CombinedJob(items, format, bookletName));
                    break;
                case ExportFormat.Pdf when profile.Pdf.FileMode == PdfFileMode.CombineByParameter:
                    jobs.AddRange(GroupedJobs(items, format, profile.Pdf, docTokens));
                    break;
                case ExportFormat.Dwf when profile.Dwf.Combine:
                    jobs.Add(CombinedJob(items, format,
                        CombinedName(profile.Dwf.CombinedFileName, documentTitle, items[0], docTokens)));
                    break;
                case ExportFormat.Xml:
                    jobs.Add(CombinedJob(items, format,
                        CombinedName(profile.Xml.FileName, documentTitle, items[0], docTokens)));
                    break;
                default:
                    jobs.AddRange(items.Select(item => new ExportJob
                    {
                        Format = format,
                        ElementIds = [item.Id],
                        Item = item,
                        FileName = ResolveFileName(item, profile, docTokens),
                    }));
                    break;
            }
        }

        return jobs;
    }

    /// <summary>Document-tokens die voor elk blad gelden (naast de sheetparameters).</summary>
    public static IReadOnlyDictionary<string, string> DocumentTokens(
        string documentTitle, string projectName = "", string projectNumber = "", string? sheetSetName = null) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [NamingEngine.TokenProjectName] = projectName ?? "",
            [NamingEngine.TokenProjectNumber] = projectNumber ?? "",
            [NamingEngine.TokenDocumentTitle] = documentTitle ?? "",
            [NamingEngine.TokenSheetSet] = sheetSetName ?? "",
        };

    /// <summary>Bestandsnaam per blad: eigen naam van de rij, anders de naamtemplate (vaste tekst + tokens).</summary>
    public static string ResolveFileName(
        SheetItem item, ExportProfile profile, IReadOnlyDictionary<string, string>? documentTokens = null)
    {
        var name = !string.IsNullOrWhiteSpace(item.CustomFileName)
            ? item.CustomFileName
            : NamingEngine.Apply(profile.NamingTemplate, item.Parameters, documentTokens);
        return NamingEngine.Sanitize(name);
    }

    /// <summary>
    /// Eén gecombineerde job per unieke waarde van de groepeer-parameter.
    /// Met <see cref="PdfSettings.SplitGroupValues"/> wordt de waarde eerst gesplitst op de
    /// scheidingstekens en belandt een item in elke tokengroep (blad in meerdere boekjes).
    /// Groepen alfabetisch (OrdinalIgnoreCase); items binnen een groep in selectievolgorde.
    /// </summary>
    public static List<ExportJob> GroupedJobs(
        IReadOnlyList<SheetItem> items, ExportFormat format, PdfSettings pdf,
        IReadOnlyDictionary<string, string>? documentTokens = null) =>
        pdf.SplitGroupValues
            ? SplitGroupedJobs(items, format, pdf.GroupByParameter, pdf.CombinedFileName, pdf.GroupValueSeparators, documentTokens)
            : ExclusiveGroupedJobs(items, format, pdf.GroupByParameter, pdf.CombinedFileName, documentTokens);

    /// <summary>Klassieke groepering: 1 item = 1 groep (hele parameterwaarde is de sleutel).</summary>
    private static List<ExportJob> ExclusiveGroupedJobs(
        IReadOnlyList<SheetItem> items, ExportFormat format, string groupParameter, string prefix,
        IReadOnlyDictionary<string, string>? documentTokens)
    {
        var groups = items
            .GroupBy(i => i.Parameters.GetValueOrDefault(groupParameter, "").Trim())
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        return groups
            .Select(g => GroupJob(format, g.Key, g.ToList(), prefix, documentTokens))
            .ToList();
    }

    /// <summary>
    /// Gesplitste groepering: per item de waarde splitsen op de scheidingstekens, tokens trimmen,
    /// lege tokens negeren en duplicaten binnen één item ontdubbelen (case-insensitive).
    /// Groepssleutels zijn case-insensitive (Windows-bestandsnamen zijn dat ook); het label
    /// krijgt de schrijfwijze van het eerst geziene item.
    /// </summary>
    private static List<ExportJob> SplitGroupedJobs(
        IReadOnlyList<SheetItem> items, ExportFormat format, string groupParameter, string prefix, string separators,
        IReadOnlyDictionary<string, string>? documentTokens)
    {
        var order = new List<string>();
        var groups = new Dictionary<string, List<SheetItem>>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var raw = item.Parameters.GetValueOrDefault(groupParameter, "");
            foreach (var token in SplitGroupValue(raw, separators))
            {
                if (!groups.TryGetValue(token, out var members))
                {
                    members = [];
                    groups[token] = members;
                    order.Add(token);
                }
                members.Add(item);
            }
        }

        return order
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .Select(k => GroupJob(format, k, groups[k], prefix, documentTokens))
            .ToList();
    }

    /// <summary>
    /// Splitst een groepswaarde in unieke, getrimde tokens. Zonder bruikbare tokens
    /// (lege waarde, alleen scheidingstekens) levert dit één lege token op → groep "overig".
    /// Lege scheidingstekens-string = niet splitsen.
    /// </summary>
    public static IReadOnlyList<string> SplitGroupValue(string value, string separators)
    {
        var tokens = string.IsNullOrEmpty(separators)
            ? new[] { value.Trim() }
            : value.Split(separators.ToCharArray(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var unique = tokens
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return unique.Count == 0 ? new[] { "" } : unique;
    }

    /// <summary>
    /// Boekjesnaam per groep. De prefix mag tokens bevatten (opgelost via het eerste blad van de
    /// groep + document-tokens). Bevat de prefix {Group}, dan bepaalt de gebruiker zelf waar het
    /// groepslabel komt; anders wordt het label als "prefix_label" achter de prefix gezet.
    /// </summary>
    private static ExportJob GroupJob(
        ExportFormat format, string key, IReadOnlyList<SheetItem> members, string prefix,
        IReadOnlyDictionary<string, string>? documentTokens)
    {
        var label = string.IsNullOrWhiteSpace(key) ? "overig" : key;

        string name;
        if (string.IsNullOrWhiteSpace(prefix))
        {
            name = label;
        }
        else if (NamingEngine.HasTokens(prefix))
        {
            var values = new Dictionary<string, string>(members[0].Parameters, StringComparer.OrdinalIgnoreCase)
            {
                [NamingEngine.TokenGroup] = label,
            };
            var resolved = NamingEngine.Apply(prefix, values, documentTokens);
            var placesGroup = NamingEngine.ExtractTokens(prefix)
                .Contains(NamingEngine.TokenGroup, StringComparer.OrdinalIgnoreCase);
            name = placesGroup ? resolved : resolved + "_" + label;
        }
        else
        {
            name = prefix + "_" + label;
        }

        return new ExportJob
        {
            Format = format,
            ElementIds = members.Select(i => i.Id).ToList(),
            FileName = NamingEngine.Sanitize(name),
            GroupLabel = label,
        };
    }

    /// <summary>
    /// Bestandsnaam van het gecombineerde boekje.
    /// Zonder tokens: basis (ingevulde naam of documenttitel), dan projectnaam als tweede veld en
    /// printset-naam als derde veld; lege velden vervallen.
    /// Mét tokens: de gebruiker bepaalt de naam volledig zelf — niets wordt automatisch achtergevoegd;
    /// tokens worden opgelost via het eerste blad + document-tokens.
    /// </summary>
    public static string BookletName(
        string configuredName, string documentTitle, string projectName, string? sheetSetName,
        SheetItem? firstItem = null, IReadOnlyDictionary<string, string>? documentTokens = null)
    {
        if (NamingEngine.HasTokens(configuredName))
            return NamingEngine.Apply(configuredName, firstItem?.Parameters ?? NoValues, documentTokens);

        var baseName = string.IsNullOrWhiteSpace(configuredName) ? documentTitle : configuredName.Trim();
        var parts = new[] { baseName, projectName.Trim(), sheetSetName?.Trim() ?? "" }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join("_", parts);
    }

    /// <summary>Naam voor DWF-combine / XML: ingevulde naam (tokens toegestaan) of documenttitel.</summary>
    private static string CombinedName(
        string configuredName, string documentTitle, SheetItem firstItem,
        IReadOnlyDictionary<string, string>? documentTokens) =>
        string.IsNullOrWhiteSpace(configuredName)
            ? documentTitle
            : NamingEngine.Apply(configuredName, firstItem.Parameters, documentTokens);

    private static ExportJob CombinedJob(IReadOnlyList<SheetItem> items, ExportFormat format, string name) =>
        new()
        {
            Format = format,
            ElementIds = items.Select(i => i.Id).ToList(),
            FileName = NamingEngine.Sanitize(name),
        };
}
