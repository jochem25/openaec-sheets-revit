using OpenAEC.Sheets.Core.Models;
using OpenAEC.Sheets.Core.Services;

using Xunit;

namespace OpenAEC.Sheets.Core.Tests;

public class PrintSetEngineTests
{
    private static SheetItem Sheet(long id, string number, string name, string? bouwdeel = null, string revision = "", string size = "") => new()
    {
        Id = id,
        Number = number,
        Name = name,
        Revision = revision,
        Size = size,
        Parameters = bouwdeel is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["bouwdeel"] = bouwdeel },
    };

    /// <summary>Regel die op elk item matcht — voor tests die alleen de groepering/bulk-logica raken.</summary>
    private static List<FilterRule> MatchAll() =>
        [new FilterRule { Parameter = "Sheet Number", Operator = FilterOperator.Contains, Value = "" }];

    private static List<SheetItem> Sheets() =>
    [
        Sheet(1, "TO_100", "Plattegrond begane grond", "A", revision: "B"),
        Sheet(2, "TO_101", "Plattegrond eerste verdieping", "B", revision: "C"),
        Sheet(3, "TO_200", "Doorsnede", "A;B"),
        Sheet(4, "TO_900", "Detail", null),
    ];

    // ── ValueOf ────────────────────────────────────────────────────────────

    [Fact]
    public void ValueOf_IntrinsicFields_CaseInsensitiveNames()
    {
        var item = Sheet(1, "TO_100", "Plattegrond", revision: "B", size: "A1");

        Assert.Equal("TO_100", PrintSetEngine.ValueOf(item, "sheet number"));
        Assert.Equal("Plattegrond", PrintSetEngine.ValueOf(item, "SHEET NAME"));
        Assert.Equal("B", PrintSetEngine.ValueOf(item, "Current Revision"));
        Assert.Equal("B", PrintSetEngine.ValueOf(item, "Revision"));
        Assert.Equal("A1", PrintSetEngine.ValueOf(item, "Size"));
        Assert.Equal("A1", PrintSetEngine.ValueOf(item, "Formaat"));
    }

    [Fact]
    public void ValueOf_FallsBackToParameters_ThenEmpty()
    {
        var item = Sheet(1, "TO_100", "Plattegrond", "A");

        Assert.Equal("A", PrintSetEngine.ValueOf(item, "bouwdeel"));
        Assert.Equal("", PrintSetEngine.ValueOf(item, "bestaat_niet"));
    }

    // ── Matches: elke operator ────────────────────────────────────────────

    [Theory]
    [InlineData(FilterOperator.Equals, "TO_100", true)]
    [InlineData(FilterOperator.Equals, "to_100", true)]
    [InlineData(FilterOperator.Equals, "TO_101", false)]
    [InlineData(FilterOperator.NotEquals, "TO_101", true)]
    [InlineData(FilterOperator.NotEquals, "TO_100", false)]
    [InlineData(FilterOperator.Contains, "_10", true)]
    [InlineData(FilterOperator.Contains, "zzz", false)]
    [InlineData(FilterOperator.StartsWith, "TO_1", true)]
    [InlineData(FilterOperator.StartsWith, "XX", false)]
    [InlineData(FilterOperator.EndsWith, "100", true)]
    [InlineData(FilterOperator.EndsWith, "999", false)]
    [InlineData(FilterOperator.Wildcard, "TO_1??", true)]
    [InlineData(FilterOperator.Wildcard, "TO_9*", false)]
    public void Matches_SingleRule_Operator(FilterOperator op, string value, bool expected)
    {
        var item = Sheet(1, "TO_100", "Plattegrond");
        var rules = new List<FilterRule> { new() { Parameter = "Sheet Number", Operator = op, Value = value } };

        Assert.Equal(expected, PrintSetEngine.Matches(item, rules, FilterCombine.All));
    }

    [Fact]
    public void Matches_IsEmpty_And_IsNotEmpty()
    {
        var withValue = Sheet(1, "TO_100", "Plattegrond", "A");
        var withoutValue = Sheet(2, "TO_101", "Plattegrond", null);

        var isEmpty = new List<FilterRule> { new() { Parameter = "bouwdeel", Operator = FilterOperator.IsEmpty } };
        var isNotEmpty = new List<FilterRule> { new() { Parameter = "bouwdeel", Operator = FilterOperator.IsNotEmpty } };

        Assert.False(PrintSetEngine.Matches(withValue, isEmpty, FilterCombine.All));
        Assert.True(PrintSetEngine.Matches(withoutValue, isEmpty, FilterCombine.All));
        Assert.True(PrintSetEngine.Matches(withValue, isNotEmpty, FilterCombine.All));
        Assert.False(PrintSetEngine.Matches(withoutValue, isNotEmpty, FilterCombine.All));
    }

    [Fact]
    public void Matches_InList_SplitsOnSemicolonAndComma_TrimsSpaces()
    {
        var item = Sheet(1, "TO_100", "Plattegrond", "B");
        var rules = new List<FilterRule> { new() { Parameter = "bouwdeel", Operator = FilterOperator.InList, Value = " A ; B , C " } };

        Assert.True(PrintSetEngine.Matches(item, rules, FilterCombine.All));

        var other = Sheet(2, "TO_101", "Plattegrond", "D");
        Assert.False(PrintSetEngine.Matches(other, rules, FilterCombine.All));
    }

    // ── Matches: EN vs OF, lege regelset, lege parameternaam ─────────────

    [Fact]
    public void Matches_EmptyRuleSet_NeverMatches()
    {
        var item = Sheet(1, "TO_100", "Plattegrond");
        Assert.False(PrintSetEngine.Matches(item, [], FilterCombine.All));
        Assert.False(PrintSetEngine.Matches(item, [], FilterCombine.Any));
    }

    [Fact]
    public void Matches_RuleWithEmptyParameterName_IsIgnored()
    {
        var item = Sheet(1, "TO_100", "Plattegrond");
        var rules = new List<FilterRule> { new() { Parameter = "", Operator = FilterOperator.Contains, Value = "x" } };

        Assert.False(PrintSetEngine.Matches(item, rules, FilterCombine.All));
        Assert.False(PrintSetEngine.Matches(item, rules, FilterCombine.Any));
    }

    [Fact]
    public void Matches_All_RequiresEveryActiveRule()
    {
        var item = Sheet(1, "TO_100", "Plattegrond", "A");
        var rules = new List<FilterRule>
        {
            new() { Parameter = "Sheet Number", Operator = FilterOperator.StartsWith, Value = "TO_" },
            new() { Parameter = "bouwdeel", Operator = FilterOperator.Equals, Value = "B" }, // faalt
        };

        Assert.False(PrintSetEngine.Matches(item, rules, FilterCombine.All));
    }

    [Fact]
    public void Matches_Any_RequiresAtLeastOneActiveRule()
    {
        var item = Sheet(1, "TO_100", "Plattegrond", "A");
        var rules = new List<FilterRule>
        {
            new() { Parameter = "Sheet Number", Operator = FilterOperator.StartsWith, Value = "XX" }, // faalt
            new() { Parameter = "bouwdeel", Operator = FilterOperator.Equals, Value = "A" }, // matcht
        };

        Assert.True(PrintSetEngine.Matches(item, rules, FilterCombine.Any));
    }

    // ── Apply: ManualIncludes/Excludes ────────────────────────────────────

    [Fact]
    public void Apply_ManualIncludes_AddsNonMatchingItem()
    {
        var def = new PrintSetDefinition
        {
            // Contains "A" matcht item 1 ("A") en item 3 ("A;B"), niet item 2 ("B") of item 4 (leeg)
            Rules = [new FilterRule { Parameter = "bouwdeel", Operator = FilterOperator.Contains, Value = "A" }],
            ManualIncludes = [4L], // "Detail" matcht niet, maar wordt toegevoegd
        };

        var result = PrintSetEngine.Apply(Sheets(), def);

        Assert.Equal([1L, 3L, 4L], result.Select(i => i.Id).ToArray());
    }

    [Fact]
    public void Apply_ManualExcludes_RemovesMatchingItem()
    {
        var def = new PrintSetDefinition
        {
            Rules = [new FilterRule { Parameter = "bouwdeel", Operator = FilterOperator.Contains, Value = "A" }],
            ManualExcludes = [1L],
        };

        var result = PrintSetEngine.Apply(Sheets(), def);

        Assert.Equal([3L], result.Select(i => i.Id).ToArray());
    }

    [Fact]
    public void Apply_NoRulesButManualIncludes_OnlyIncludesThose()
    {
        var def = new PrintSetDefinition { ManualIncludes = [2L, 4L] };

        var result = PrintSetEngine.Apply(Sheets(), def);

        Assert.Equal([2L, 4L], result.Select(i => i.Id).ToArray());
    }

    // ── BuildSets: zonder bulk ─────────────────────────────────────────────

    [Fact]
    public void BuildSets_NoBulk_SingleSanitizedSet()
    {
        var def = new PrintSetDefinition
        {
            Name = "TO/plattegronden",
            Rules = [new FilterRule { Parameter = "bouwdeel", Operator = FilterOperator.Contains, Value = "A" }],
        };

        var sets = PrintSetEngine.BuildSets(Sheets(), def);

        var set = Assert.Single(sets);
        Assert.Equal("TO-plattegronden", set.Name);
        Assert.Equal([1L, 3L], set.Items.Select(i => i.Id).ToArray());
    }

    [Fact]
    public void BuildSets_NoBulk_EmptyResult_NoFilterMatches()
    {
        var def = new PrintSetDefinition
        {
            Name = "leeg",
            Rules = [new FilterRule { Parameter = "bouwdeel", Operator = FilterOperator.Equals, Value = "zzz" }],
        };

        Assert.Empty(PrintSetEngine.BuildSets(Sheets(), def));
    }

    // ── BuildSets: bulk, met en zonder split ───────────────────────────────

    [Fact]
    public void BuildSets_Bulk_WithSplit_ItemInMultipleSets()
    {
        var def = new PrintSetDefinition
        {
            Name = "boekje",
            Rules = MatchAll(),
            BulkPerParameter = true,
            BulkParameter = "bouwdeel",
            SplitBulkValues = true,
        };

        var sets = PrintSetEngine.BuildSets(Sheets(), def);

        // "" (item 4, geen waarde → overig) sorteert alfabetisch vóór "A" en "B"
        Assert.Equal(["boekje_overig", "boekje_A", "boekje_B"], sets.Select(s => s.Name).ToArray());
        Assert.Equal([4L], sets[0].Items.Select(i => i.Id).ToArray());
        Assert.Equal([1L, 3L], sets[1].Items.Select(i => i.Id).ToArray());
        Assert.Equal([2L, 3L], sets[2].Items.Select(i => i.Id).ToArray()); // sheet 3 ("A;B") zit in beide
    }

    [Fact]
    public void BuildSets_Bulk_WithoutSplit_WholeValueIsGroup()
    {
        var def = new PrintSetDefinition
        {
            Name = "boekje",
            Rules = MatchAll(),
            BulkPerParameter = true,
            BulkParameter = "bouwdeel",
            SplitBulkValues = false,
        };

        var sets = PrintSetEngine.BuildSets(Sheets(), def);

        // Zonder splitsen is "A;B" één letterlijke groep (';' is een geldig Windows-bestandsnaamteken);
        // sortering op de ruwe sleutel, "" (→ overig) komt alfabetisch vóór "A".
        Assert.Equal(["boekje_overig", "boekje_A", "boekje_A;B", "boekje_B"], sets.Select(s => s.Name).ToArray());
        Assert.All(sets, s => Assert.Single(s.Items));
    }

    // ── BuildSets: {Group} in de naam, lege naam, sanitize, lege groepen ──

    [Fact]
    public void BuildSets_GroupToken_UserPlacesLabel()
    {
        var def = new PrintSetDefinition
        {
            Name = "2459_{Group}_printset",
            Rules = MatchAll(),
            BulkPerParameter = true,
            BulkParameter = "bouwdeel",
        };

        var sets = PrintSetEngine.BuildSets(Sheets(), def);

        Assert.Contains("2459_A_printset", sets.Select(s => s.Name));
        Assert.Contains("2459_B_printset", sets.Select(s => s.Name));
    }

    [Fact]
    public void BuildSets_EmptyName_UsesGroupNameItself()
    {
        var def = new PrintSetDefinition
        {
            Name = "",
            Rules = MatchAll(),
            BulkPerParameter = true,
            BulkParameter = "bouwdeel",
        };

        var sets = PrintSetEngine.BuildSets(Sheets(), def);

        // "" (item 4, geen waarde → overig) sorteert alfabetisch vóór "A" en "B"
        Assert.Equal(["overig", "A", "B"], sets.Select(s => s.Name).ToArray());
    }

    [Fact]
    public void BuildSets_MissingBulkValue_GroupsAsOverig()
    {
        var def = new PrintSetDefinition
        {
            Name = "set",
            Rules = MatchAll(),
            BulkPerParameter = true,
            BulkParameter = "bestaat_niet",
        };

        var sets = PrintSetEngine.BuildSets(Sheets(), def);

        var set = Assert.Single(sets);
        Assert.Equal("set_overig", set.Name);
        Assert.Equal(4, set.Items.Count);
    }

    [Fact]
    public void BuildSets_NamesAreSanitized()
    {
        var def = new PrintSetDefinition
        {
            Name = "team:noord/zuid",
            Rules = MatchAll(),
            BulkPerParameter = true,
            BulkParameter = "bouwdeel",
        };

        var sets = PrintSetEngine.BuildSets(Sheets(), def);

        Assert.All(sets, s => Assert.DoesNotContain(':', s.Name));
        Assert.All(sets, s => Assert.DoesNotContain('/', s.Name));
    }

    [Fact]
    public void BuildSets_EmptyGroups_AreOmitted()
    {
        // Filter laat alleen bouwdeel "A" door -> groep "B" bestaat niet in het resultaat
        var def = new PrintSetDefinition
        {
            Name = "set",
            Rules = [new FilterRule { Parameter = "bouwdeel", Operator = FilterOperator.Equals, Value = "A" }],
            BulkPerParameter = true,
            BulkParameter = "bouwdeel",
        };

        var sets = PrintSetEngine.BuildSets(Sheets(), def);

        Assert.Single(sets);
        Assert.Equal("set_A", sets[0].Name);
    }

    [Fact]
    public void BuildSets_GroupsAlphabetical_OrdinalIgnoreCase()
    {
        var items = new List<SheetItem>
        {
            Sheet(1, "TO_1", "a", "zuid"),
            Sheet(2, "TO_2", "b", "Noord"),
            Sheet(3, "TO_3", "c", "algemeen"),
        };
        var def = new PrintSetDefinition { Rules = MatchAll(), BulkPerParameter = true, BulkParameter = "bouwdeel" };

        var sets = PrintSetEngine.BuildSets(items, def);

        Assert.Equal(["algemeen", "Noord", "zuid"], sets.Select(s => s.Name).ToArray());
    }
}
