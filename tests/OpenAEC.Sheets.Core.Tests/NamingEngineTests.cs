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
    public void ExtractTokens_FindsDistinctTokens()
    {
        var tokens = NamingEngine.ExtractTokens("{A}_{B}_{A}");
        Assert.Equal(["A", "B"], tokens);
    }
}
