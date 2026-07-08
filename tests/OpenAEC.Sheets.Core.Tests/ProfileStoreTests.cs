using OpenAEC.Sheets.Core.Models;
using OpenAEC.Sheets.Core.Services;

using Xunit;

namespace OpenAEC.Sheets.Core.Tests;

public class ProfileStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProfileStore _store;

    public ProfileStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "openaec-sheets-tests-" + Guid.NewGuid().ToString("N"));
        _store = new ProfileStore(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsProfile()
    {
        var profile = new ExportProfile
        {
            Name = "3BM standaard",
            NamingTemplate = "{Sheet Number}_{Current Revision}",
            EnabledFormats = [ExportFormat.Pdf, ExportFormat.Ifc],
            SplitByFormat = false,
        };
        profile.Pdf.FileMode = PdfFileMode.CombineByParameter;
        profile.Pdf.GroupByParameter = "bouwdeel";
        profile.Pdf.Colors = ColorMode.BlackAndWhite;
        profile.Ifc.Version = "IFC4RV";

        _store.Save(profile);
        var loaded = _store.Load("3BM standaard");

        Assert.NotNull(loaded);
        Assert.Equal("3BM standaard", loaded.Name);
        Assert.Equal("{Sheet Number}_{Current Revision}", loaded.NamingTemplate);
        Assert.Equal([ExportFormat.Pdf, ExportFormat.Ifc], loaded.EnabledFormats);
        Assert.False(loaded.SplitByFormat);
        Assert.Equal(PdfFileMode.CombineByParameter, loaded.Pdf.FileMode);
        Assert.Equal("bouwdeel", loaded.Pdf.GroupByParameter);
        Assert.Equal(ColorMode.BlackAndWhite, loaded.Pdf.Colors);
        Assert.Equal("IFC4RV", loaded.Ifc.Version);
    }

    [Fact]
    public void ListNames_ReturnsSavedProfiles()
    {
        _store.Save(new ExportProfile { Name = "B-profiel" });
        _store.Save(new ExportProfile { Name = "A-profiel" });

        Assert.Equal(["A-profiel", "B-profiel"], _store.ListNames());
    }

    [Fact]
    public void Delete_RemovesProfile()
    {
        _store.Save(new ExportProfile { Name = "weg" });
        _store.Delete("weg");

        Assert.Empty(_store.ListNames());
        Assert.Null(_store.Load("weg"));
    }

    [Fact]
    public void Load_MissingProfile_ReturnsNull()
    {
        Assert.Null(_store.Load("bestaat-niet"));
    }
}
