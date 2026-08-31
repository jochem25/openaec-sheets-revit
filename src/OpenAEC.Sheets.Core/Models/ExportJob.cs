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
    /// <summary>
    /// Bouwt de lijst exportjobs uit de selectie en het profiel.
    /// Combine-formaten (PDF/DWF/XML) leveren één job voor alle items, anders één job per item.
    /// </summary>
    public static List<ExportJob> Build(
        IReadOnlyList<SheetItem> items, ExportProfile profile, string documentTitle,
        string projectName = "", string? sheetSetName = null)
    {
        var jobs = new List<ExportJob>();
        if (items.Count == 0) return jobs;

        foreach (var format in profile.EnabledFormats)
        {
            switch (format)
            {
                case ExportFormat.Pdf when profile.Pdf.FileMode == PdfFileMode.CombineAll:
                    var bookletName = BookletName(profile.Pdf.CombinedFileName, documentTitle, projectName, sheetSetName);
                    jobs.Add(CombinedJob(items, format, bookletName, documentTitle));
                    break;
                case ExportFormat.Pdf when profile.Pdf.FileMode == PdfFileMode.CombineByParameter:
                    jobs.AddRange(GroupedJobs(items, format, profile.Pdf));
                    break;
                case ExportFormat.Dwf when profile.Dwf.Combine:
                    jobs.Add(CombinedJob(items, format, profile.Dwf.CombinedFileName, documentTitle));
                    break;
                case ExportFormat.Xml:
                    jobs.Add(CombinedJob(items, format, profile.Xml.FileName, documentTitle));
                    break;
                default:
                    jobs.AddRange(items.Select(item => new ExportJob
                    {
                        Format = format,
                        ElementIds = [item.Id],
                        Item = item,
                        FileName = ResolveFileName(item, profile),
                    }));
                    break;
            }
        }

        return jobs;
    }

    public static string ResolveFileName(SheetItem item, ExportProfile profile)
    {
        var name = !string.IsNullOrWhiteSpace(item.CustomFileName)
            ? item.CustomFileName
            : NamingEngine.Apply(profile.NamingTemplate, item.Parameters);
        return NamingEngine.Sanitize(name);
    }

    /// <summary>
    /// Eén gecombineerde job per unieke waarde van de groepeer-parameter.
    /// Met <see cref="PdfSettings.SplitGroupValues"/> wordt de waarde eerst gesplitst op de
    /// scheidingstekens en belandt een item in elke tokengroep (blad in meerdere boekjes).
    /// Groepen alfabetisch (OrdinalIgnoreCase); items binnen een groep in selectievolgorde.
    /// </summary>
    public static List<ExportJob> GroupedJobs(IReadOnlyList<SheetItem> items, ExportFormat format, PdfSettings pdf) =>
        pdf.SplitGroupValues
            ? SplitGroupedJobs(items, format, pdf.GroupByParameter, pdf.CombinedFileName, pdf.GroupValueSeparators)
            : ExclusiveGroupedJobs(items, format, pdf.GroupByParameter, pdf.CombinedFileName);

    /// <summary>Klassieke groepering: 1 item = 1 groep (hele parameterwaarde is de sleutel).</summary>
    private static List<ExportJob> ExclusiveGroupedJobs(
        IReadOnlyList<SheetItem> items, ExportFormat format, string groupParameter, string prefix)
    {
        var groups = items
            .GroupBy(i => i.Parameters.GetValueOrDefault(groupParameter, "").Trim())
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        return groups
            .Select(g => GroupJob(format, g.Key, g.Select(i => i.Id), prefix))
            .ToList();
    }

    /// <summary>
    /// Gesplitste groepering: per item de waarde splitsen op de scheidingstekens, tokens trimmen,
    /// lege tokens negeren en duplicaten binnen één item ontdubbelen (case-insensitive).
    /// Groepssleutels zijn case-insensitive (Windows-bestandsnamen zijn dat ook); het label
    /// krijgt de schrijfwijze van het eerst geziene item.
    /// </summary>
    private static List<ExportJob> SplitGroupedJobs(
        IReadOnlyList<SheetItem> items, ExportFormat format, string groupParameter, string prefix, string separators)
    {
        var order = new List<string>();
        var groups = new Dictionary<string, List<long>>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var raw = item.Parameters.GetValueOrDefault(groupParameter, "");
            foreach (var token in SplitGroupValue(raw, separators))
            {
                if (!groups.TryGetValue(token, out var ids))
                {
                    ids = [];
                    groups[token] = ids;
                    order.Add(token);
                }
                ids.Add(item.Id);
            }
        }

        return order
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .Select(k => GroupJob(format, k, groups[k], prefix))
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

    private static ExportJob GroupJob(ExportFormat format, string key, IEnumerable<long> ids, string prefix)
    {
        var label = string.IsNullOrWhiteSpace(key) ? "overig" : key;
        var name = string.IsNullOrWhiteSpace(prefix) ? label : prefix + "_" + label;
        return new ExportJob
        {
            Format = format,
            ElementIds = ids.ToList(),
            FileName = NamingEngine.Sanitize(name),
            GroupLabel = label,
        };
    }

    /// <summary>
    /// Bestandsnaam van het gecombineerde boekje: basis (ingevulde naam of documenttitel),
    /// dan projectnaam als tweede veld en printset-naam als derde veld; lege velden vervallen.
    /// </summary>
    public static string BookletName(string configuredName, string documentTitle, string projectName, string? sheetSetName)
    {
        var baseName = string.IsNullOrWhiteSpace(configuredName) ? documentTitle : configuredName.Trim();
        var parts = new[] { baseName, projectName.Trim(), sheetSetName?.Trim() ?? "" }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join("_", parts);
    }

    private static ExportJob CombinedJob(IReadOnlyList<SheetItem> items, ExportFormat format, string configuredName, string documentTitle)
    {
        var name = string.IsNullOrWhiteSpace(configuredName) ? documentTitle : configuredName;
        return new ExportJob
        {
            Format = format,
            ElementIds = items.Select(i => i.Id).ToList(),
            FileName = NamingEngine.Sanitize(name),
        };
    }
}
