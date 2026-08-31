using OpenAEC.Sheets.Core.Naming;

using Xunit;

namespace OpenAEC.Sheets.Core.Tests;

public class NamingEngineTests
{
    private static readonly Dictionary<string, string> Values = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sheet Number"] = "TO_110",
        ["Sheet Name"] = "plattegrond begane grond",
        ["Current Revision"] = "B",
    };

    [Fact]
    public void Apply_ReplacesKnownTokens()
    {
        var result = NamingEngine.Apply("{Sheet Number}_{Sheet Name}", Values);
        Assert.Equal("TO_110_plattegrond begane grond", result);
    }

    [Fact]
    public void Apply_UnknownTokenBecomesEmpty()
    {
        var result = NamingEngine.Apply("{Sheet Number}_{Onbekend}", Values);
        Assert.Equal("TO_110_", result);
    }

    [Fact]
    public void Apply_IsCaseInsensitive()
    {
        var result = NamingEngine.Apply("{sheet number}", Values);
        Assert.Equal("TO_110", result);
    }

    [Fact]
    public void Apply_EmptyTemplate_ReturnsEmpty()
    {
        Assert.Equal("", NamingEngine.Apply("", Values));
    }

    [Theory]
    [InlineData("naam:met/slash", "naam-met-slash")]
    [InlineData("  spaties  ", "spaties")]
    [InlineData("a__b___c", "a_b_c")]
    [InlineData("einde_", "einde")]
    [InlineData("", "unnamed")]
    [InlineData("///", "unnamed")]
    public void Sanitize_Cases(string input, string expected)
    {
        Assert.Equal(expected, NamingEngine.Sanitize(input));
    }

    [Fact]
    public void Apply_FixedTextAroundTokens_IsKeptLiterally()
    {
        Assert.Equal("TO_plattegrond begane grond", NamingEngine.Apply("TO_{Sheet Name}", Values));
        Assert.Equal("TO_TO_110-B_def", NamingEngine.Apply("TO_{Sheet Number}-{Current Revision}_def", Values));
        Assert.Equal("alleen tekst", NamingEngine.Apply("alleen tekst", Values));
    }

    [Fact]
    public void Apply_WithFallback_ItemValueWinsOverFallback()
    {
        var fallback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Project Number"] = "2459",
            ["Sheet Number"] = "NIET-GEBRUIKT",
        };

        var result = NamingEngine.Apply("{Project Number}_{Sheet Number}_{Onbekend}", Values, fallback);

        Assert.Equal("2459_TO_110_", result);
    }

    [Theory]
    [InlineData("{Sheet Number}", true)]
    [InlineData("TO_{x}", true)]
    [InlineData("TO_", false)]
    [InlineData("", false)]
    [InlineData("{}", false)]
    public void HasTokens_Cases(string template, bool expected)
    {
        Assert.Equal(expected, NamingEngine.HasTokens(template));
    }

    [Fact]
    public void ExtractTokens_FindsDistinctTokens()
    {
        var tokens = NamingEngine.ExtractTokens("{A}_{B}_{A}");
        Assert.Equal(["A", "B"], tokens);
    }
}
