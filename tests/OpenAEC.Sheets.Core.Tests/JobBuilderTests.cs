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
        profile.Pdf.Combine = true;
        profile.Pdf.CombinedFileName = "bundel";

        var jobs = JobBuilder.Build(TwoSheets(), profile, "doc");

        var job = Assert.Single(jobs);
        Assert.Equal([1L, 2L], job.ElementIds);
        Assert.Equal("bundel", job.FileName);
        Assert.Null(job.Item);
    }

    [Fact]
    public void Build_CombineWithoutName_FallsBackToDocumentTitle()
    {
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Xml] };

        var jobs = JobBuilder.Build(TwoSheets(), profile, "2786_Bouwkundige_model_v2");

        Assert.Equal("2786_Bouwkundige_model_v2", Assert.Single(jobs).FileName);
    }

    [Fact]
    public void Build_NoItems_NoJobs()
    {
        var profile = new ExportProfile { EnabledFormats = [ExportFormat.Pdf] };
        Assert.Empty(JobBuilder.Build([], profile, "doc"));
    }
}
