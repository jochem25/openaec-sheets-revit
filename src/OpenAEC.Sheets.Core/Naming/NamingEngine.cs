using System.Text.RegularExpressions;

namespace OpenAEC.Sheets.Core.Naming;

/// <summary>
/// Vervangt {Parameter Naam}-tokens in een bestandsnaam-template door
/// parameterwaarden van de sheet/view en saneert het resultaat.
/// </summary>
public static partial class NamingEngine
{
    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex TokenRegex();

    public static string Apply(string template, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrWhiteSpace(template)) return "";

        return TokenRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value.Trim();
            return values.TryGetValue(key, out var value) ? value : "";
        });
    }

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
