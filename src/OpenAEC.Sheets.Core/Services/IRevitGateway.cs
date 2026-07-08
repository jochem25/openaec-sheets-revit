using OpenAEC.Sheets.Core.Models;

namespace OpenAEC.Sheets.Core.Services;

/// <summary>
/// Abstractie over de Revit API zodat de UI-laag geen Revit-dependency heeft.
/// Geïmplementeerd in OpenAEC.Sheets.Revit via een ExternalEvent-bridge.
/// </summary>
public interface IRevitGateway
{
    string DocumentTitle { get; }

    Task<IReadOnlyList<SheetItem>> GetSheetsAsync();
    Task<IReadOnlyList<SheetItem>> GetViewsAsync();

    /// <summary>Namen van View/Sheet Sets in het model.</summary>
    Task<IReadOnlyList<string>> GetViewSheetSetNamesAsync();

    /// <summary>ElementId.Value's van de views/sheets in een View/Sheet Set.</summary>
    Task<IReadOnlyList<long>> GetViewSheetSetIdsAsync(string setName);

    Task<IReadOnlyList<string>> GetDwgSetupNamesAsync();
    Task<IReadOnlyList<string>> GetDgnSetupNamesAsync();

    /// <summary>Alle parameternamen die op sheets voorkomen (voor naming-tokens en XML).</summary>
    Task<IReadOnlyList<string>> GetSheetParameterNamesAsync();

    /// <summary>Voert de jobs één voor één uit op de Revit-thread en rapporteert voortgang.</summary>
    Task ExportAsync(
        IReadOnlyList<ExportJob> jobs,
        ExportProfile profile,
        string outputFolder,
        IProgress<ExportProgress> progress,
        CancellationToken cancellationToken);
}

/// <param name="JobIndex">Index van de afgeronde/falende job.</param>
/// <param name="Total">Totaal aantal jobs.</param>
/// <param name="FileName">Bestandsnaam van de job.</param>
/// <param name="Error">Foutmelding, null bij succes.</param>
public sealed record ExportProgress(int JobIndex, int Total, string FileName, string? Error);
