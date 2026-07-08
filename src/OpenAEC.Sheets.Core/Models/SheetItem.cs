namespace OpenAEC.Sheets.Core.Models;

/// <summary>
/// Eén sheet of view uit het Revit-model, met de parameterwaarden
/// die nodig zijn voor weergave en bestandsnaam-tokens.
/// </summary>
public sealed class SheetItem
{
    /// <summary>Revit ElementId.Value</summary>
    public long Id { get; set; }

    public bool IsSheet { get; set; } = true;

    public string Number { get; set; } = "";
    public string Name { get; set; } = "";
    public string Revision { get; set; } = "";
    /// <summary>Bladformaat uit titleblock, bijv. "841x594" of "A0".</summary>
    public string Size { get; set; } = "";

    /// <summary>Handmatige override van de bestandsnaam (zonder extensie). Leeg = naming template.</summary>
    public string CustomFileName { get; set; } = "";

    /// <summary>Alle parameterwaarden (naam → waarde) voor naming-tokens en XML-export.</summary>
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
