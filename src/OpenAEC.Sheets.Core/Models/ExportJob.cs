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

    public string DisplayNumber => Item?.Number ?? "—";
    public string DisplayName => Item?.Name ?? "(gecombineerd)";
}

public static class JobBuilder
{
    /// <summary>
    /// Bouwt de lijst exportjobs uit de selectie en het profiel.
    /// Combine-formaten (PDF/DWF/XML) leveren één job voor alle items, anders één job per item.
    /// </summary>
    public static List<ExportJob> Build(IReadOnlyList<SheetItem> items, ExportProfile profile, string documentTitle)
    {
        var jobs = new List<ExportJob>();
        if (items.Count == 0) return jobs;

        foreach (var format in profile.EnabledFormats)
        {
            switch (format)
            {
                case ExportFormat.Pdf when profile.Pdf.Combine:
                    jobs.Add(CombinedJob(items, format, profile.Pdf.CombinedFileName, documentTitle));
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
