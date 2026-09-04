using OpenAEC.Sheets.Core.Models;
using OpenAEC.Sheets.Core.Naming;

namespace OpenAEC.Sheets.Core.Services;

/// <summary>
/// Past <see cref="PrintSetDefinition"/>-filters toe op sheets en bouwt er View/Sheet Set-groepen
/// mee op. Hergebruikt <see cref="JobBuilder"/> voor glob-matching en waarde-splitsing zodat
/// printset-filters en boekjes-groepering hetzelfde gedrag hebben.
/// </summary>
public static class PrintSetEngine
{
    /// <summary>
    /// Waarde van een sheetparameter voor een item: eerst de intrinsieke velden (Sheet Number/Name,
    /// Current Revision/Revision, Size/Formaat), anders een lookup in <see cref="SheetItem.Parameters"/>,
    /// anders leeg. Case-insensitieve parameternaam.
    /// </summary>
    public static string ValueOf(SheetItem item, string parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter)) return "";
        var name = parameter.Trim();

        if (string.Equals(name, "Sheet Number", StringComparison.OrdinalIgnoreCase)) return item.Number;
        if (string.Equals(name, "Sheet Name", StringComparison.OrdinalIgnoreCase)) return item.Name;
        if (string.Equals(name, "Current Revision", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Revision", StringComparison.OrdinalIgnoreCase)) return item.Revision;
        if (string.Equals(name, "Size", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Formaat", StringComparison.OrdinalIgnoreCase)) return item.Size;

        return item.Parameters.TryGetValue(name, out var value) ? value : "";
    }

    /// <summary>
    /// True als <paramref name="item"/> voldoet aan <paramref name="rules"/> volgens <paramref name="combine"/>.
    /// Een lege regelset levert nooit een match op (nooit stilzwijgend "alles" selecteren). Regels met een
    /// lege parameternaam tellen niet mee. Vergelijkingen zijn case-insensitief en waarden worden getrimd.
    /// </summary>
    public static bool Matches(SheetItem item, IReadOnlyList<FilterRule> rules, FilterCombine combine)
    {
        var active = rules.Where(r => !string.IsNullOrWhiteSpace(r.Parameter)).ToList();
        if (active.Count == 0) return false;

        return combine == FilterCombine.Any
            ? active.Any(r => MatchesRule(item, r))
            : active.All(r => MatchesRule(item, r));
    }

    private static bool MatchesRule(SheetItem item, FilterRule rule)
    {
        var value = ValueOf(item, rule.Parameter).Trim();
        var compare = (rule.Value ?? "").Trim();

        return rule.Operator switch
        {
            FilterOperator.Equals => string.Equals(value, compare, StringComparison.OrdinalIgnoreCase),
            FilterOperator.NotEquals => !string.Equals(value, compare, StringComparison.OrdinalIgnoreCase),
            FilterOperator.Contains => value.Contains(compare, StringComparison.OrdinalIgnoreCase),
            FilterOperator.StartsWith => value.StartsWith(compare, StringComparison.OrdinalIgnoreCase),
            FilterOperator.EndsWith => value.EndsWith(compare, StringComparison.OrdinalIgnoreCase),
            FilterOperator.Wildcard => JobBuilder.GlobToRegex(compare).IsMatch(value),
            FilterOperator.IsEmpty => value.Length == 0,
            FilterOperator.IsNotEmpty => value.Length > 0,
            FilterOperator.InList => InList(value, compare),
            _ => false,
        };
    }

    private static bool InList(string value, string list) =>
        list.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(token => string.Equals(token, value, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Filterresultaat van <paramref name="def"/> toegepast op <paramref name="items"/>, met
    /// <see cref="PrintSetDefinition.ManualIncludes"/> erbij en <see cref="PrintSetDefinition.ManualExcludes"/>
    /// eruit. Volgorde: itemvolgorde van <paramref name="items"/>.
    /// </summary>
    public static List<SheetItem> Apply(IReadOnlyList<SheetItem> items, PrintSetDefinition def)
    {
        var includes = new HashSet<long>(def.ManualIncludes);
        var excludes = new HashSet<long>(def.ManualExcludes);

        var result = new List<SheetItem>();
        foreach (var item in items)
        {
            var included = Matches(item, def.Rules, def.Combine) || includes.Contains(item.Id);
            if (included && !excludes.Contains(item.Id)) result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Bouwt de set(en) voor <paramref name="def"/>. Zonder bulk: één set met de gesaneerde naam.
    /// Met bulk: één set per unieke waarde van <see cref="PrintSetDefinition.BulkParameter"/>
    /// (item kan in meerdere groepen belanden bij <see cref="PrintSetDefinition.SplitBulkValues"/>);
    /// groepen alfabetisch (OrdinalIgnoreCase). Lege groepen worden weggelaten.
    /// </summary>
    public static List<(string Name, List<SheetItem> Items)> BuildSets(
        IReadOnlyList<SheetItem> items, PrintSetDefinition def)
    {
        var filtered = Apply(items, def);

        if (!def.BulkPerParameter || string.IsNullOrWhiteSpace(def.BulkParameter))
        {
            var name = NamingEngine.Sanitize(def.Name.Trim());
            return filtered.Count == 0 ? [] : [(name, filtered)];
        }

        Func<SheetItem, IReadOnlyList<string>> keysOf = def.SplitBulkValues
            ? item => JobBuilder.SplitGroupValue(ValueOf(item, def.BulkParameter), PdfSettings.DefaultGroupValueSeparators)
            : item => [ValueOf(item, def.BulkParameter).Trim()];

        var perItem = new List<(SheetItem Item, List<string> Keys)>();
        var order = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in filtered)
        {
            var keys = keysOf(item).ToList();
            if (keys.Count == 0) keys = [""];
            foreach (var key in keys)
                if (seen.Add(key)) order.Add(key);
            perItem.Add((item, keys));
        }

        return order
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .Select(key => (
                Label: string.IsNullOrWhiteSpace(key) ? "overig" : key,
                Items: perItem.Where(p => p.Keys.Contains(key, StringComparer.OrdinalIgnoreCase)).Select(p => p.Item).ToList()))
            .Where(g => g.Items.Count > 0)
            .Select(g => (Name: NamingEngine.Sanitize(BuildSetName(def.Name, g.Label)), g.Items))
            .ToList();
    }

    /// <summary>
    /// Setnaam per groep: bevat de template {Group}, dan bepaalt de gebruiker zelf waar het
    /// groepslabel komt; anders wordt het label als "template_label" achter de template gezet;
    /// een lege template levert de groepsnaam zelf op.
    /// </summary>
    private static string BuildSetName(string template, string group)
    {
        if (string.IsNullOrWhiteSpace(template)) return group;

        if (NamingEngine.ExtractTokens(template).Contains(NamingEngine.TokenGroup, StringComparer.OrdinalIgnoreCase))
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [NamingEngine.TokenGroup] = group,
            };
            return NamingEngine.Apply(template, values);
        }

        return template.Trim() + "_" + group;
    }
}
