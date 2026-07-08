using OpenAEC.Sheets.Core.Models;

namespace OpenAEC.Sheets.Core.Services;

/// <summary>
/// Abstractie over de Revit API zodat de UI-laag geen Revit-dependency heeft.
/// Geïmplementeerd in OpenAEC.Sheets.Revit via een ExternalEvent-bridge.
/// </summary>
public interface IRevitGateway
{
    string DocumentTitle { get; }

    /// <summary>
    /// Leest alles wat de UI nodig heeft in één Revit API-call:
    /// sheets, views, view/sheet sets en exportsetups.
    /// </summary>
    /// <param name="progress">Fase-updates voor de laad-overlay ("Sheets lezen 40/120…").</param>
    Task<ModelSnapshot> GetSnapshotAsync(IProgress<string>? progress = null);

    /// <summary>Voert de jobs één voor één uit op de Revit-thread en rapporteert voortgang.</summary>
    Task ExportAsync(
        IReadOnlyList<ExportJob> jobs,
        ExportProfile profile,
        string outputFolder,
        IProgress<ExportProgress> progress,
        CancellationToken cancellationToken);
}

/// <summary>Alle modelgegevens voor de UI, opgehaald in één Revit-rondreis.</summary>
public sealed class ModelSnapshot
{
    public IReadOnlyList<SheetItem> Sheets { get; init; } = [];
    public IReadOnlyList<SheetItem> Views { get; init; } = [];

    /// <summary>View/Sheet Set-naam → ElementId.Value's van de views/sheets erin.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<long>> ViewSheetSets { get; init; }
        = new Dictionary<string, IReadOnlyList<long>>();

    public IReadOnlyList<string> DwgSetupNames { get; init; } = [];
    public IReadOnlyList<string> DgnSetupNames { get; init; } = [];

    /// <summary>Fasenamen in documentvolgorde (voor IFC "Phase to export").</summary>
    public IReadOnlyList<string> PhaseNames { get; init; } = [];

    /// <summary>Namen van IFC category mapping templates in het document.</summary>
    public IReadOnlyList<string> CategoryMappingNames { get; init; } = [];
}

/// <param name="JobIndex">Index van de afgeronde/falende job.</param>
/// <param name="Total">Totaal aantal jobs.</param>
/// <param name="FileName">Bestandsnaam van de job.</param>
/// <param name="Error">Foutmelding, null bij succes.</param>
public sealed record ExportProgress(int JobIndex, int Total, string FileName, string? Error);
