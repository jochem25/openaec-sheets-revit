using OpenAEC.Sheets.Core.Models;

using Xunit;

namespace OpenAEC.Sheets.Core.Tests;

public class JobBuilderTests
{
    private static List<SheetItem> TwoSheets() =>
    [
        new SheetItem
        {
            Id = 1, Number = "TO_099", Name = "Palenplan",
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Sheet Number"] = "TO_099", ["Sheet Name"] = "Palenplan",
            },
        },
        new SheetItem
        {
            Id = 2, Number = "TO_110", Name = "plattegrond begane grond",
            CustomFileName = "eigen naam",
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Sheet Number"] = "TO_110", ["Sheet Name"] = "plattegrond begane grond",
            },
        },
    ];

    [Fact]
    public void Build_SeparateFiles_OneJobPerItemPerFormat()
    {
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Pdf, ExportFormat.Dwg] };

        var jobs = JobBuilder.Build(TwoSheets(), profile, "doc");

        Assert.Equal(4, jobs.Count);
        Assert.Equal(2, jobs.Count(j => j.Format == ExportFormat.Pdf));
        Assert.Equal(2, jobs.Count(j => j.Format == ExportFormat.Dwg));
    }

    [Fact]
    public void Build_CustomFileName_OverridesTemplate()
    {
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Pdf] };

        var jobs = JobBuilder.Build(TwoSheets(), profile, "doc");

        Assert.Equal("TO_099_Palenplan", jobs[0].FileName);
        Assert.Equal("eigen naam", jobs[1].FileName);
    }

    [Fact]
    public void Build_CombinePdf_SingleJobWithAllIds()
    {
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Pdf] };
        profile.Pdf.FileMode = PdfFileMode.CombineAll;
        profile.Pdf.CombinedFileName = "bundel";

        var jobs = JobBuilder.Build(TwoSheets(), profile, "doc");

        var job = Assert.Single(jobs);
        Assert.Equal([1L, 2L], job.ElementIds);
        Assert.Equal("bundel", job.FileName);
        Assert.Null(job.Item);
    }

    [Fact]
    public void Build_CombineByParameter_OneJobPerGroupValue()
    {
        var items = TwoSheets();
        items[0].Parameters["bouwdeel"] = "A";
        items[1].Parameters["bouwdeel"] = "B";
        items.Add(new SheetItem
        {
            Id = 3, Number = "TO_111", Name = "eerste verdieping",
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["bouwdeel"] = "A" },
        });

        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Pdf] };
        profile.Pdf.FileMode = PdfFileMode.CombineByParameter;
        profile.Pdf.GroupByParameter = "bouwdeel";

        var jobs = JobBuilder.Build(items, profile, "doc");

        Assert.Equal(2, jobs.Count);
        Assert.Equal([1L, 3L], jobs[0].ElementIds);
        Assert.Equal("A", jobs[0].FileName);
        Assert.Equal("A", jobs[0].GroupLabel);
        Assert.Equal([2L], jobs[1].ElementIds);
        Assert.Equal("B", jobs[1].FileName);
    }

    [Fact]
    public void Build_CombineByParameter_MissingValueGroupsAsOverig_WithPrefix()
    {
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Pdf] };
        profile.Pdf.FileMode = PdfFileMode.CombineByParameter;
        profile.Pdf.GroupByParameter = "bestaat_niet";
        profile.Pdf.CombinedFileName = "2786";

        var jobs = JobBuilder.Build(TwoSheets(), profile, "doc");

        var job = Assert.Single(jobs);
        Assert.Equal("2786_overig", job.FileName);
        Assert.Equal([1L, 2L], job.ElementIds);
    }

    [Fact]
    public void Build_CombineWithoutName_FallsBackToDocumentTitle()
    {
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Xml] };

        var jobs = JobBuilder.Build(TwoSheets(), profile, "2786_Bouwkundige_model_v2");

        Assert.Equal("2786_Bouwkundige_model_v2", Assert.Single(jobs).FileName);
    }

    [Fact]
    public void Build_CombinePdf_AppendsProjectNameAndSetName()
    {
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Pdf] };
        profile.Pdf.FileMode = PdfFileMode.CombineAll;
        profile.Pdf.CombinedFileName = "2786";

        var jobs = JobBuilder.Build(TwoSheets(), profile, "doc", "Woonhuis Kerkstraat", "TO-set");

        Assert.Equal("2786_Woonhuis Kerkstraat_TO-set", Assert.Single(jobs).FileName);
    }

    [Fact]
    public void BookletName_SkipsEmptyFields()
    {
        Assert.Equal("2786_Project", JobBuilder.BookletName("2786", "doc", "Project", null));
        Assert.Equal("doc_Project_Set", JobBuilder.BookletName("", "doc", "Project", "Set"));
        Assert.Equal("2786", JobBuilder.BookletName("2786", "doc", "", null));
    }

    [Fact]
    public void Build_NoItems_NoJobs()
    {
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Pdf] };
        Assert.Empty(JobBuilder.Build([], profile, "doc"));
    }

    // ── Combineer per parameterwaarde: waarde splitsen (blad in meerdere boekjes) ──

    private static SheetItem Sheet(long id, string? boekje) => new()
    {
        Id = id, Number = "TO-" + id, Name = "blad " + id,
        Parameters = boekje is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Boekje"] = boekje },
    };

    /// <summary>Combine-per-parameter-profiel voor groepeer-tests; samenstellen staat uit zodat de jobs de boekjes zelf zijn.</summary>
    private static ExportProfile SplitProfile(bool split = true, string prefix = "", string? separators = null, bool assemble = false)
    {
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Pdf] };
        profile.Pdf.FileMode = PdfFileMode.CombineByParameter;
        profile.Pdf.GroupByParameter = "Boekje";
        profile.Pdf.SplitGroupValues = split;
        profile.Pdf.CombinedFileName = prefix;
        profile.Pdf.AssembleBooklets = assemble;
        if (separators is not null) profile.Pdf.GroupValueSeparators = separators;
        return profile;
    }

    [Fact]
    public void Split_Off_KeepsExclusiveGrouping_WholeValueIsKey()
    {
        // Regressie: zonder splitsen is "a;b" één groep, niet twee.
        var items = new List<SheetItem> { Sheet(1, "a;b"), Sheet(2, "a"), Sheet(3, "b") };

        var jobs = JobBuilder.Build(items, SplitProfile(split: false), "doc");

        Assert.Equal(3, jobs.Count);
        Assert.Equal(["a", "a;b", "b"], jobs.Select(j => j.GroupLabel!).ToArray());
        Assert.Equal([2L], jobs[0].ElementIds);
        Assert.Equal([1L], jobs[1].ElementIds);
        Assert.Equal([3L], jobs[2].ElementIds);
        Assert.All(jobs, j => Assert.Single(j.ElementIds));
    }

    [Fact]
    public void Split_On_ItemLandsInEveryTokenGroup()
    {
        var items = new List<SheetItem> { Sheet(1, "a;b") };

        var jobs = JobBuilder.Build(items, SplitProfile(), "doc");

        Assert.Equal(2, jobs.Count);
        Assert.Equal("a", jobs[0].GroupLabel);
        Assert.Equal([1L], jobs[0].ElementIds);
        Assert.Equal("b", jobs[1].GroupLabel);
        Assert.Equal([1L], jobs[1].ElementIds);
    }

    [Fact]
    public void Split_Parkview_SheetInThreeBooklets()
    {
        // Praktijkcase 2459 Parkview Gouda
        var items = new List<SheetItem>
        {
            Sheet(100, "plattegronden;plattegronden-noord"),
            Sheet(101, "plattegronden;plattegronden-noord;plattegronden-zuid"),
            Sheet(102, "plattegronden;plattegronden-zuid"),
        };

        var jobs = JobBuilder.Build(items, SplitProfile(prefix: "2459"), "doc");

        Assert.Equal(["2459_plattegronden", "2459_plattegronden-noord", "2459_plattegronden-zuid"],
            jobs.Select(j => j.FileName).ToArray());
        Assert.Equal([100L, 101L, 102L], jobs[0].ElementIds);
        Assert.Equal([100L, 101L], jobs[1].ElementIds);
        Assert.Equal([101L, 102L], jobs[2].ElementIds);
    }

    [Fact]
    public void Split_TrimsWhitespaceAroundTokens()
    {
        var jobs = JobBuilder.Build([Sheet(1, " a ; b ")], SplitProfile(), "doc");

        Assert.Equal(["a", "b"], jobs.Select(j => j.GroupLabel!).ToArray());
        Assert.Equal(["a", "b"], jobs.Select(j => j.FileName).ToArray());
    }

    [Theory]
    [InlineData("a;;b", new[] { "a", "b" })]
    [InlineData(";", new[] { "overig" })]
    [InlineData("", new[] { "overig" })]
    [InlineData(" ; , ", new[] { "overig" })]
    public void Split_EmptyTokensIgnored_NoValueBecomesOverig(string value, string[] expectedLabels)
    {
        var jobs = JobBuilder.Build([Sheet(1, value)], SplitProfile(), "doc");

        Assert.Equal(expectedLabels, jobs.Select(j => j.GroupLabel!).ToArray());
        Assert.All(jobs, j => Assert.Equal([1L], j.ElementIds));
    }

    [Fact]
    public void Split_MissingParameter_GroupsAsOverig()
    {
        var jobs = JobBuilder.Build([Sheet(1, null), Sheet(2, "a")], SplitProfile(), "doc");

        // Lege sleutel sorteert vóór "a" — zelfde volgorde als bij de klassieke groepering
        Assert.Equal(["overig", "a"], jobs.Select(j => j.GroupLabel!).ToArray());
        Assert.Equal([1L], jobs[0].ElementIds);
    }

    [Fact]
    public void Split_DuplicateTokenWithinItem_CountedOnce_CaseInsensitive()
    {
        var jobs = JobBuilder.Build([Sheet(1, "a;a"), Sheet(2, "b;B")], SplitProfile(), "doc");

        Assert.Equal(2, jobs.Count);
        Assert.Equal([1L], jobs[0].ElementIds);
        Assert.Equal([2L], jobs[1].ElementIds);
    }

    [Fact]
    public void Split_CommaAndMixedSeparators()
    {
        var comma = JobBuilder.Build([Sheet(1, "a,b")], SplitProfile(), "doc");
        Assert.Equal(["a", "b"], comma.Select(j => j.GroupLabel!).ToArray());

        var mixed = JobBuilder.Build([Sheet(1, "a;b,c")], SplitProfile(), "doc");
        Assert.Equal(["a", "b", "c"], mixed.Select(j => j.GroupLabel!).ToArray());
        Assert.All(mixed, j => Assert.Equal([1L], j.ElementIds));
    }

    [Fact]
    public void Split_CustomSeparator_OnlySplitsOnConfiguredChars()
    {
        var jobs = JobBuilder.Build([Sheet(1, "a;b|c")], SplitProfile(separators: "|"), "doc");

        Assert.Equal(["a;b", "c"], jobs.Select(j => j.GroupLabel!).ToArray());
    }

    [Fact]
    public void Split_EmptySeparators_DoesNotSplit()
    {
        var jobs = JobBuilder.Build([Sheet(1, "a;b")], SplitProfile(separators: ""), "doc");

        Assert.Equal("a;b", Assert.Single(jobs).GroupLabel);
    }

    [Fact]
    public void Split_GroupsAlphabetical_ItemsInSelectionOrder()
    {
        var items = new List<SheetItem>
        {
            Sheet(30, "zuid;Noord"),
            Sheet(10, "noord"),
            Sheet(20, "Zuid;algemeen"),
        };

        var jobs = JobBuilder.Build(items, SplitProfile(), "doc");

        Assert.Equal(["algemeen", "Noord", "zuid"], jobs.Select(j => j.GroupLabel!).ToArray());
        Assert.Equal([20L], jobs[0].ElementIds);
        Assert.Equal([30L, 10L], jobs[1].ElementIds); // selectievolgorde, niet gesorteerd op id
        Assert.Equal([30L, 20L], jobs[2].ElementIds);
    }

    [Fact]
    public void Split_FileName_PrefixEmptyVsFilled()
    {
        var noPrefix = JobBuilder.Build([Sheet(1, "a;b")], SplitProfile(prefix: ""), "doc");
        Assert.Equal(["a", "b"], noPrefix.Select(j => j.FileName).ToArray());

        var withPrefix = JobBuilder.Build([Sheet(1, "a;b")], SplitProfile(prefix: "2459"), "doc");
        Assert.Equal(["2459_a", "2459_b"], withPrefix.Select(j => j.FileName).ToArray());
    }

    [Fact]
    public void Split_FileName_IsSanitized()
    {
        var jobs = JobBuilder.Build([Sheet(1, "noord/zuid;a:b")], SplitProfile(), "doc");

        Assert.Equal(["a-b", "noord-zuid"], jobs.Select(j => j.FileName).ToArray());
    }

    [Fact]
    public void Split_DoesNotAffectOtherFormats()
    {
        var profile = SplitProfile();
        profile.EnabledFormats = [ExportFormat.Pdf, ExportFormat.Dwg, ExportFormat.Dwf];
        profile.Dwf.Combine = true;

        var jobs = JobBuilder.Build([Sheet(1, "a;b"), Sheet(2, "b")], profile, "doc");

        var pdf = jobs.Where(j => j.Format == ExportFormat.Pdf).ToList();
        Assert.Equal(2, pdf.Count);                                     // boekje a en boekje b
        Assert.Equal([1L, 2L], pdf[1].ElementIds);                      // b bevat 1 én 2
        Assert.Equal(2, jobs.Count(j => j.Format == ExportFormat.Dwg)); // één per item, geen dubbelen
        Assert.Single(jobs, j => j.Format == ExportFormat.Dwf);          // één gecombineerd
    }

    // ── Naamgeving: vaste tekst, document-tokens, parameters als token, tokens in boekjesnaam ──

    private static SheetItem FaseSheet(long id, string number, string fase) => new()
    {
        Id = id, Number = number, Name = "blad " + id,
        Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sheet Number"] = number, ["Sheet Name"] = "blad " + id, ["tekening_fase"] = fase,
        },
    };

    [Fact]
    public void Naming_FixedPrefixPlusTokens()
    {
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Pdf], NamingTemplate = "TO_{Sheet Number}_{Sheet Name}" };

        var jobs = JobBuilder.Build([FaseSheet(1, "100n", "TO")], profile, "doc");

        Assert.Equal("TO_100n_blad 1", Assert.Single(jobs).FileName);
    }

    [Fact]
    public void Naming_ParameterAsToken_Fase()
    {
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Pdf], NamingTemplate = "{tekening_fase}_{Sheet Number}" };

        var jobs = JobBuilder.Build([FaseSheet(1, "100n", "TO"), FaseSheet(2, "200", "DO")], profile, "doc");

        Assert.Equal(["TO_100n", "DO_200"], jobs.Select(j => j.FileName).ToArray());
    }

    [Fact]
    public void Naming_DocumentTokens_ProjectNumberNameTitleSet()
    {
        var profile = new ExportProfile
        {
            EnabledFormats = [ExportFormat.Pdf],
            NamingTemplate = "{Project Number}_{Project Name}_{Document Title}_{Sheet Set}_{Sheet Number}",
        };

        var jobs = JobBuilder.Build([FaseSheet(1, "100n", "TO")], profile, "PVG_TO_BWK", "Parkview", "TO-set", "2459");

        Assert.Equal("2459_Parkview_PVG_TO_BWK_TO-set_100n", Assert.Single(jobs).FileName);
    }

    [Fact]
    public void Naming_DocumentTokens_EmptyWhenNotProvided_NoDoubleUnderscores()
    {
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Pdf], NamingTemplate = "{Project Number}_{Sheet Number}" };

        var jobs = JobBuilder.Build([FaseSheet(1, "100n", "TO")], profile, "doc");

        Assert.Equal("100n", Assert.Single(jobs).FileName);
    }

    [Fact]
    public void Naming_SheetParameterWinsOverDocumentToken()
    {
        var item = FaseSheet(1, "100n", "TO");
        item.Parameters["Project Number"] = "van-sheet";
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Pdf], NamingTemplate = "{Project Number}" };

        var jobs = JobBuilder.Build([item], profile, "doc", projectNumber: "van-document");

        Assert.Equal("van-sheet", Assert.Single(jobs).FileName);
    }

    [Fact]
    public void Naming_CustomFileName_StillOverridesTemplate()
    {
        var item = FaseSheet(1, "100n", "TO");
        item.CustomFileName = "eigen";
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Pdf], NamingTemplate = "TO_{Sheet Number}" };

        Assert.Equal("eigen", Assert.Single(JobBuilder.Build([item], profile, "doc")).FileName);
    }

    [Fact]
    public void BookletName_WithTokens_UserControlsName_NoAutoAppend()
    {
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Pdf] };
        profile.Pdf.FileMode = PdfFileMode.CombineAll;
        profile.Pdf.CombinedFileName = "{Project Number}_{tekening_fase}_boekje";

        var jobs = JobBuilder.Build([FaseSheet(1, "100n", "TO"), FaseSheet(2, "200", "DO")], profile,
            "doc", "Parkview", "TO-set", "2459");

        // Eerste blad levert de fase; projectnaam/set NIET automatisch achtergevoegd
        Assert.Equal("2459_TO_boekje", Assert.Single(jobs).FileName);
    }

    [Fact]
    public void BookletName_WithoutTokens_KeepsAutoAppend()
    {
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Pdf] };
        profile.Pdf.FileMode = PdfFileMode.CombineAll;
        profile.Pdf.CombinedFileName = "2459";

        var jobs = JobBuilder.Build([FaseSheet(1, "100n", "TO")], profile, "doc", "Parkview", "TO-set", "2459");

        Assert.Equal("2459_Parkview_TO-set", Assert.Single(jobs).FileName);
    }

    [Fact]
    public void GroupedPrefix_WithTokens_ResolvedFromFirstSheetOfGroup_LabelAppended()
    {
        var items = new List<SheetItem>
        {
            FaseSheet(1, "100n", "TO"),
            FaseSheet(2, "200", "DO"),
        };
        items[0].Parameters["Boekje"] = "plattegronden";
        items[1].Parameters["Boekje"] = "gevels";
        var profile = SplitProfile(prefix: "{Project Number}_{tekening_fase}");

        var jobs = JobBuilder.Build(items, profile, "doc", projectNumber: "2459");

        Assert.Equal(["2459_DO_gevels", "2459_TO_plattegronden"], jobs.Select(j => j.FileName).ToArray());
    }

    [Fact]
    public void GroupedPrefix_WithGroupToken_UserPlacesLabel()
    {
        var item = FaseSheet(1, "100n", "TO");
        item.Parameters["Boekje"] = "noord;zuid";
        var profile = SplitProfile(prefix: "{Group}_{Project Number}_{tekening_fase}");

        var jobs = JobBuilder.Build([item], profile, "doc", projectNumber: "2459");

        Assert.Equal(["noord_2459_TO", "zuid_2459_TO"], jobs.Select(j => j.FileName).ToArray());
    }

    [Fact]
    public void GroupedPrefix_WithoutTokens_Unchanged()
    {
        var item = FaseSheet(1, "100n", "TO");
        item.Parameters["Boekje"] = "a";
        var jobs = JobBuilder.Build([item], SplitProfile(prefix: "2459"), "doc", projectNumber: "9999");

        Assert.Equal("2459_a", Assert.Single(jobs).FileName);
    }

    [Fact]
    public void DwfAndXml_CombinedNames_AcceptDocumentTokens()
    {
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Dwf, ExportFormat.Xml] };
        profile.Dwf.Combine = true;
        profile.Dwf.CombinedFileName = "{Project Number}_set";
        profile.Xml.FileName = "params_{Document Title}";

        var jobs = JobBuilder.Build([FaseSheet(1, "100n", "TO")], profile, "PVG", projectNumber: "2459");

        Assert.Equal("2459_set", jobs.Single(j => j.Format == ExportFormat.Dwf).FileName);
        Assert.Equal("params_PVG", jobs.Single(j => j.Format == ExportFormat.Xml).FileName);
    }

    [Fact]
    public void DocumentTokens_ContainsAllFourKeys_CaseInsensitive()
    {
        var tokens = JobBuilder.DocumentTokens("doc", "naam", "2459", null);

        Assert.Equal("2459", tokens["project number"]);
        Assert.Equal("naam", tokens["PROJECT NAME"]);
        Assert.Equal("doc", tokens["Document Title"]);
        Assert.Equal("", tokens["Sheet Set"]);
    }

    // ── Wildcards: glob-patronen in de groepswaarde expanderen tegen de concrete boekjesnamen ──

    private static List<string> WarningsOf(IReadOnlyList<SheetItem> items, ExportProfile profile)
    {
        var warnings = new List<string>();
        JobBuilder.GroupedJobs(items, ExportFormat.Pdf, profile.Pdf, null, warnings);
        return warnings;
    }

    [Fact]
    public void Wildcard_Star_CoverSheetInEveryBooklet_KeepsSelectionOrder()
    {
        var items = new List<SheetItem>
        {
            Sheet(1, "*"),                 // voorblad, als eerste geselecteerd
            Sheet(2, "plattegronden"),
            Sheet(3, "gevels;plattegronden"),
            Sheet(4, "*"),                 // legenda, achteraan
        };

        var jobs = JobBuilder.Build(items, SplitProfile(), "doc");

        Assert.Equal(["gevels", "plattegronden"], jobs.Select(j => j.GroupLabel!).ToArray());
        Assert.Equal([1L, 3L, 4L], jobs[0].ElementIds);
        Assert.Equal([1L, 2L, 3L, 4L], jobs[1].ElementIds);
    }

    [Fact]
    public void Wildcard_Prefix_MatchesAllHouseBooklets()
    {
        // Situatietekening "Z_*" → in beide woningboekjes
        var items = new List<SheetItem>
        {
            Sheet(1, "Z_*"),
            Sheet(2, "Z_00_01_G1"),
            Sheet(3, "Z_11_04_G4"),
            Sheet(4, "algemeen"),
        };

        var jobs = JobBuilder.Build(items, SplitProfile(), "doc");

        Assert.Equal(["algemeen", "Z_00_01_G1", "Z_11_04_G4"], jobs.Select(j => j.GroupLabel!).ToArray());
        Assert.Equal([4L], jobs[0].ElementIds);
        Assert.Equal([1L, 2L], jobs[1].ElementIds);
        Assert.Equal([1L, 3L], jobs[2].ElementIds);
    }

    [Fact]
    public void Wildcard_NarrowPrefix_MatchesOnlyThatFloor()
    {
        var items = new List<SheetItem>
        {
            Sheet(1, "Z_02_*"),
            Sheet(2, "Z_02_01_G1"), Sheet(3, "Z_02_07_G3"),
            Sheet(4, "Z_03_01_G1"),
        };

        var jobs = JobBuilder.Build(items, SplitProfile(), "doc");

        Assert.Equal([1L, 2L], jobs.Single(j => j.GroupLabel == "Z_02_01_G1").ElementIds);
        Assert.Equal([1L, 3L], jobs.Single(j => j.GroupLabel == "Z_02_07_G3").ElementIds);
        Assert.Equal([4L], jobs.Single(j => j.GroupLabel == "Z_03_01_G1").ElementIds);
    }

    [Fact]
    public void Wildcard_Suffix_MultiplePatterns()
    {
        var items = new List<SheetItem>
        {
            Sheet(1, "*_E1;*_E2"),
            Sheet(2, "Z_01_E1"), Sheet(3, "Z_02_E2"), Sheet(4, "Z_03_E3"),
        };

        var jobs = JobBuilder.Build(items, SplitProfile(), "doc");

        Assert.Equal([1L, 2L], jobs.Single(j => j.GroupLabel == "Z_01_E1").ElementIds);
        Assert.Equal([1L, 3L], jobs.Single(j => j.GroupLabel == "Z_02_E2").ElementIds);
        Assert.Equal([4L], jobs.Single(j => j.GroupLabel == "Z_03_E3").ElementIds);
    }

    [Fact]
    public void Wildcard_QuestionMark_MatchesExactlyOneChar()
    {
        var items = new List<SheetItem> { Sheet(1, "G?"), Sheet(2, "G1"), Sheet(3, "G12") };

        var jobs = JobBuilder.Build(items, SplitProfile(), "doc");

        Assert.Equal([1L, 2L], jobs.Single(j => j.GroupLabel == "G1").ElementIds);
        Assert.Equal([3L], jobs.Single(j => j.GroupLabel == "G12").ElementIds);
    }

    [Fact]
    public void Wildcard_CaseInsensitive()
    {
        var items = new List<SheetItem> { Sheet(1, "z_*"), Sheet(2, "Z_00_01_G1") };

        var jobs = JobBuilder.Build(items, SplitProfile(), "doc");

        var job = Assert.Single(jobs);
        Assert.Equal("Z_00_01_G1", job.GroupLabel);
        Assert.Equal([1L, 2L], job.ElementIds);
    }

    [Fact]
    public void Wildcard_NoMatch_WarningAndNoFile()
    {
        var items = new List<SheetItem> { Sheet(1, "X_*"), Sheet(2, "a") };
        var profile = SplitProfile();

        var jobs = JobBuilder.Build(items, profile, "doc");
        var warnings = WarningsOf(items, profile);

        // Geen bestand met * in de naam; blad 1 valt in "overig" (niet stilzwijgend weg)
        Assert.Equal(["overig", "a"], jobs.Select(j => j.GroupLabel!).ToArray());
        Assert.DoesNotContain(jobs, j => j.FileName.Contains('*'));
        Assert.Equal([1L], jobs[0].ElementIds);
        Assert.Equal([2L], jobs[1].ElementIds);
        Assert.Equal(["patroon 'X_*' matcht geen enkel boekje"], warnings);
    }

    [Fact]
    public void Wildcard_OnlyPatterns_NoConcreteNames_AllInOverig_NoCrash()
    {
        var items = new List<SheetItem> { Sheet(1, "*"), Sheet(2, "Z_*") };
        var profile = SplitProfile();

        var jobs = JobBuilder.Build(items, profile, "doc");

        var job = Assert.Single(jobs);
        Assert.Equal("overig", job.GroupLabel);
        Assert.Equal([1L, 2L], job.ElementIds);
        Assert.Equal(2, WarningsOf(items, profile).Count);
    }

    [Fact]
    public void Wildcard_MixedWithConcrete_NoDuplicateMembership()
    {
        // "*;a" → in alle boekjes, niet dubbel in a
        var jobs = JobBuilder.Build([Sheet(1, "*;a"), Sheet(2, "a"), Sheet(3, "b")], SplitProfile(), "doc");

        Assert.Equal(["a", "b"], jobs.Select(j => j.GroupLabel!).ToArray());
        Assert.Equal([1L, 2L], jobs[0].ElementIds);
        Assert.Equal([1L, 3L], jobs[1].ElementIds);
    }

    [Fact]
    public void Wildcard_NeverCreatesBooklet_AlsoWhenMatched()
    {
        var jobs = JobBuilder.Build([Sheet(1, "Z_*"), Sheet(2, "Z_01")], SplitProfile(), "doc");

        Assert.Equal("Z_01", Assert.Single(jobs).GroupLabel);
    }

    [Fact]
    public void Wildcard_Disabled_PatternIsLiteralGroup()
    {
        var profile = SplitProfile();
        profile.Pdf.ExpandWildcards = false;

        var jobs = JobBuilder.Build([Sheet(1, "Z_*"), Sheet(2, "Z_01")], profile, "doc");

        Assert.Equal(["Z_*", "Z_01"], jobs.Select(j => j.GroupLabel!).ToArray());
        Assert.Equal([1L], jobs[0].ElementIds);
        Assert.Empty(WarningsOf([Sheet(1, "Z_*")], profile));
    }

    [Fact]
    public void Wildcard_InactiveInExclusiveMode_CaseSensitiveRegression()
    {
        var items = new List<SheetItem> { Sheet(1, "*"), Sheet(2, "a"), Sheet(3, "A") };

        var jobs = JobBuilder.Build(items, SplitProfile(split: false), "doc");

        // Klassiek: "*" is gewoon een (letterlijke) groep, a en A blijven apart
        Assert.Equal(3, jobs.Count);
        Assert.Equal([1L], jobs.Single(j => j.GroupLabel == "*").ElementIds);
    }

    [Fact]
    public void Wildcard_PageCountIncludesCoverInEveryBooklet()
    {
        var items = new List<SheetItem> { Sheet(1, "*"), Sheet(2, "a"), Sheet(3, "b"), Sheet(4, "c") };

        var jobs = JobBuilder.GroupedJobs(items, ExportFormat.Pdf, SplitProfile().Pdf);

        Assert.Equal(3, jobs.Count);
        Assert.Equal(6, jobs.Sum(j => j.ElementIds.Count));                     // 3 boekjes × (voorblad + 1)
        Assert.Equal(4, jobs.SelectMany(j => j.ElementIds).Distinct().Count());
    }

    [Theory]
    [InlineData("Z_*", "Z_00_01_G1", true)]
    [InlineData("Z_*", "z_x", true)]
    [InlineData("Z_*", "Y_01", false)]
    [InlineData("*_E1", "Z_01_E1", true)]
    [InlineData("*_E1", "Z_01_E12", false)]
    [InlineData("G?", "G1", true)]
    [InlineData("G?", "G12", false)]
    [InlineData("a.b*", "a.bc", true)]
    [InlineData("a.b*", "aXbc", false)]   // punt is letterlijk, geen regex-metateken
    public void GlobToRegex_Cases(string pattern, string name, bool expected)
    {
        Assert.Equal(expected, JobBuilder.GlobToRegex(pattern).IsMatch(name));
    }

    // ── Samenstellen: bladen 1× renderen, boekjes mergen ──

    [Fact]
    public void AssemblyPlan_WithOverlap_PagesOnceInSelectionOrder_ThenBooklets()
    {
        var items = new List<SheetItem> { Sheet(1, "*"), Sheet(2, "a"), Sheet(3, "b"), Sheet(4, "a;b") };

        var jobs = JobBuilder.Build(items, SplitProfile(prefix: "2459", assemble: true), "doc");

        var pages = jobs.Where(j => j.Kind == ExportJobKind.TempPage).ToList();
        var booklets = jobs.Where(j => j.Kind == ExportJobKind.Assemble).ToList();

        Assert.Equal(4, pages.Count);
        Assert.Equal([1L, 2L, 3L, 4L], pages.Select(p => p.ElementIds.Single()).ToArray());
        Assert.Equal(["page_1", "page_2", "page_3", "page_4"], pages.Select(p => p.FileName).ToArray());
        Assert.All(pages, p => Assert.NotNull(p.Item));
        Assert.True(jobs.IndexOf(pages[^1]) < jobs.IndexOf(booklets[0])); // eerst alle bladen, dan boekjes

        Assert.Equal(["2459_a", "2459_b"], booklets.Select(b => b.FileName).ToArray());
        Assert.Equal([1L, 2L, 4L], booklets[0].ElementIds);
        Assert.Equal([1L, 3L, 4L], booklets[1].ElementIds);
        Assert.Equal(["TO-1 - blad 1", "TO-2 - blad 2", "TO-4 - blad 4"], booklets[0].PageTitles);
        Assert.Equal("a", booklets[0].GroupLabel);
    }

    [Fact]
    public void AssemblyPlan_NoOverlap_KeepsNativeBooklets()
    {
        var items = new List<SheetItem> { Sheet(1, "a"), Sheet(2, "b") };

        var jobs = JobBuilder.Build(items, SplitProfile(assemble: true), "doc");

        Assert.Equal(2, jobs.Count);
        Assert.All(jobs, j => Assert.Equal(ExportJobKind.Normal, j.Kind));
    }

    [Fact]
    public void AssemblyPlan_SettingOff_KeepsNativeBooklets_EvenWithOverlap()
    {
        var profile = SplitProfile(assemble: false);

        var jobs = JobBuilder.Build([Sheet(1, "*"), Sheet(2, "a"), Sheet(3, "b")], profile, "doc");

        Assert.Equal(2, jobs.Count);
        Assert.All(jobs, j => Assert.Equal(ExportJobKind.Normal, j.Kind));
        Assert.Equal([1L, 2L], jobs[0].ElementIds);
    }

    [Fact]
    public void AssemblyPlan_OnlySheetsInBooklets_GetATempPage()
    {
        // Voorblad in a én b (overlap) → plan; blad 4 is niet geselecteerd voor deze boekjes en mag geen TempPage krijgen
        var items = new List<SheetItem> { Sheet(1, "*"), Sheet(2, "a"), Sheet(3, "b") };
        var booklets = JobBuilder.GroupedJobs(items, ExportFormat.Pdf, SplitProfile().Pdf);
        var itemsWithExtra = items.Append(Sheet(4, "c")).ToList();

        var plan = JobBuilder.AssemblyPlan(itemsWithExtra, booklets);

        Assert.Equal(2, plan.Count(j => j.Kind == ExportJobKind.Assemble));
        Assert.Equal([1L, 2L, 3L], plan.Where(j => j.Kind == ExportJobKind.TempPage).Select(j => j.ElementIds.Single()).ToArray());
    }

    [Fact]
    public void DisplayFormat_PerKind()
    {
        Assert.Equal("PDF", new ExportJob { Format = ExportFormat.Pdf }.DisplayFormat);
        Assert.Equal("PDF (blad 1×)", new ExportJob { Format = ExportFormat.Pdf, Kind = ExportJobKind.TempPage }.DisplayFormat);
        Assert.Equal("PDF (boekje)", new ExportJob { Format = ExportFormat.Pdf, Kind = ExportJobKind.Assemble }.DisplayFormat);
        Assert.Equal("DWG", new ExportJob { Format = ExportFormat.Dwg }.DisplayFormat);
    }
}
