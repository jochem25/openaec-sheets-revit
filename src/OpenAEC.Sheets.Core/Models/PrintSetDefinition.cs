namespace OpenAEC.Sheets.Core.Models;

/// <summary>Vergelijkingsoperator voor een filterregel op een sheetparameter.</summary>
public enum FilterOperator
{
    Equals,
    NotEquals,
    Contains,
    StartsWith,
    EndsWith,
    Wildcard,
    IsEmpty,
    IsNotEmpty,
    InList,
}

/// <summary>Hoe de regels van een filter gecombineerd worden.</summary>
public enum FilterCombine
{
    /// <summary>Alle regels moeten matchen (EN).</summary>
    All,
    /// <summary>Minstens één regel moet matchen (OF).</summary>
    Any,
}

/// <summary>Wat er gebeurt met een al bestaande View/Sheet Set met dezelfde naam.</summary>
public enum PrintSetMode
{
    /// <summary>Bestaande set met dezelfde naam vervangen.</summary>
    Overwrite,
    /// <summary>Bestaande set aanvullen met de nieuwe sheets (unie).</summary>
    AddOnly,
}

/// <summary>Eén filterregel: een sheetparameter, een operator en een vergelijkingswaarde.</summary>
public sealed class FilterRule
{
    public string Parameter { get; set; } = "";
    public FilterOperator Operator { get; set; } = FilterOperator.Contains;
    public string Value { get; set; } = "";
}

/// <summary>
/// Definitie van een printset (View/Sheet Set) op basis van filters op sheetparameters.
/// Wordt in het exportprofiel bewaard zodat de set later opnieuw gegenereerd kan worden.
/// </summary>
public sealed class PrintSetDefinition
{
    /// <summary>Setnaam; bij bulk een template waarin {Group} vervangen wordt door de groepswaarde.</summary>
    public string Name { get; set; } = "";

    public FilterCombine Combine { get; set; } = FilterCombine.All;

    public List<FilterRule> Rules { get; set; } = [];

    /// <summary>Bestaande set met dezelfde naam overschrijven of alleen aanvullen.</summary>
    public PrintSetMode Mode { get; set; } = PrintSetMode.Overwrite;

    /// <summary>Per unieke waarde van <see cref="BulkParameter"/> een aparte set aanmaken.</summary>
    public bool BulkPerParameter { get; set; }

    /// <summary>Parameter waarop bij bulk gegroepeerd wordt.</summary>
    public string BulkParameter { get; set; } = "";

    /// <summary>Bulk-waarde splitsen op ";," (<see cref="Services.PrintSetEngine"/> / JobBuilder.SplitGroupValue).</summary>
    public bool SplitBulkValues { get; set; } = true;

    /// <summary>Uitzonderingen bovenop het filter: sheets die altijd meegaan, ook zonder match.</summary>
    public List<long> ManualIncludes { get; set; } = [];

    /// <summary>Uitzonderingen bovenop het filter: sheets die nooit meegaan, ook bij een match.</summary>
    public List<long> ManualExcludes { get; set; } = [];
}
