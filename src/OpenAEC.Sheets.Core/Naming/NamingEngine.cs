using System.Text.RegularExpressions;

namespace OpenAEC.Sheets.Core.Naming;

/// <summary>
/// Vervangt {Parameter Naam}-tokens in een bestandsnaam-template door
/// parameterwaarden van de sheet/view en saneert het resultaat.
/// Tekst buiten de accolades blijft letterlijk staan: "TO_{Sheet Number}" → "TO_TO-100".
/// </summary>
public static partial class NamingEngine
{
    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex TokenRegex();

    // Document-tokens: gelden voor elk blad, los van de sheetparameters.
    public const string TokenProjectName = "Project Name";
    public const string TokenProjectNumber = "Project Number";
    public const string TokenDocumentTitle = "Document Title";
    public const string TokenSheetSet = "Sheet Set";
    /// <summary>Groepswaarde bij "combineer per parameterwaarde"; alleen zinvol in de boekjesnaam.</summary>
    public const string TokenGroup = "Group";

    /// <summary>Document-tokens in de volgorde waarin de UI ze toont.</summary>
    public static readonly IReadOnlyList<string> DocumentTokens =
        [TokenProjectName, TokenProjectNumber, TokenDocumentTitle, TokenSheetSet];

    public static string Apply(string template, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrWhiteSpace(template)) return "";

        return TokenRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value.Trim();
            return values.TryGetValue(key, out var value) ? value : "";
        });
    }

    /// <summary>
    /// Als <see cref="Apply(string, IReadOnlyDictionary{string, string})"/>, maar met een
    /// terugvallaag: een token dat niet in <paramref name="values"/> staat, wordt in
    /// <paramref name="fallback"/> gezocht (document-tokens naast sheetparameters).
    /// </summary>
    public static string Apply(
        string template,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, string>? fallback)
    {
        if (fallback is null || fallback.Count == 0) return Apply(template, values);
        if (string.IsNullOrWhiteSpace(template)) return "";

        return TokenRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value.Trim();
            if (values.TryGetValue(key, out var value)) return value;
            return fallback.TryGetValue(key, out var fb) ? fb : "";
        });
    }

    /// <summary>True als de template minimaal één {token} bevat.</summary>
    public static bool HasTokens(string template) =>
        !string.IsNullOrEmpty(template) && TokenRegex().IsMatch(template);

    /// <summary>Verwijdert ongeldige bestandsnaam-tekens en dubbele separators.</summary>
    public static string Sanitize(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "unnamed";

        var invalid = Path.GetInvalidFileNameChars();
        var chars = fileName.Select(c => invalid.Contains(c) ? '-' : c).ToArray();
        var result = new string(chars).Trim().TrimEnd('.', '_', '-');

        // Dubbele underscores door lege tokens samenvouwen
        while (result.Contains("__"))
            result = result.Replace("__", "_");

        result = result.Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "unnamed" : result;
    }

    /// <summary>Tokens die in een template voorkomen (voor validatie/hints in de UI).</summary>
    public static IReadOnlyList<string> ExtractTokens(string template)
    {
        if (string.IsNullOrWhiteSpace(template)) return [];
        return TokenRegex().Matches(template).Select(m => m.Groups[1].Value.Trim()).Distinct().ToList();
    }
}
