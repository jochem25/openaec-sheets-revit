using OpenAEC.Sheets.Core.Services;

using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

using Xunit;

namespace OpenAEC.Sheets.Core.Tests;

public class PdfAssemblerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "openaec-assemble-" + Guid.NewGuid().ToString("N"));

    public PdfAssemblerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    /// <summary>Maakt een PDF met <paramref name="pages"/> pagina's van <paramref name="widthPt"/> punt breed (breedte = herkenning).</summary>
    private string MakePdf(string name, int pages, double widthPt)
    {
        var path = Path.Combine(_dir, name + ".pdf");
        using var doc = new PdfDocument();
        for (var i = 0; i < pages; i++)
        {
            var page = doc.AddPage();
            page.Width = PdfSharp.Drawing.XUnit.FromPoint(widthPt);
            page.Height = PdfSharp.Drawing.XUnit.FromPoint(500);
        }
        doc.Save(path);
        return path;
    }

    [Fact]
    public void Assemble_MergesInOrder_WithBookmarkPerSheet()
    {
        var cover = MakePdf("page_1", 1, 100);
        var plan = MakePdf("page_2", 2, 200);   // blad met 2 pagina's (kan bij Revit niet, maar assembler moet het aankunnen)
        var section = MakePdf("page_3", 1, 300);
        var output = Path.Combine(_dir, "out", "boekje.pdf");

        var count = PdfAssembler.Assemble(
            [(cover, "TO-000 - Voorblad"), (plan, "TO-100 - Plattegrond"), (section, "TO-300 - Doorsnede")], output);

        Assert.Equal(4, count);
        using var result = PdfReader.Open(output, PdfDocumentOpenMode.Import);
        Assert.Equal(4, result.PageCount);
        Assert.Equal([100.0, 200.0, 200.0, 300.0], result.Pages.Cast<PdfPage>().Select(p => p.Width.Point).ToArray());
        Assert.Equal(3, result.Outlines.Count);
        Assert.Equal("TO-000 - Voorblad", result.Outlines[0].Title);
        Assert.Equal("TO-300 - Doorsnede", result.Outlines[2].Title);
    }

    [Fact]
    public void Assemble_SameSourceTwice_IsAllowed()
    {
        // Voorblad in meerdere boekjes: dezelfde bron meermaals importeren mag
        var cover = MakePdf("page_1", 1, 100);
        var a = MakePdf("page_2", 1, 200);
        var out1 = Path.Combine(_dir, "a.pdf");
        var out2 = Path.Combine(_dir, "b.pdf");

        PdfAssembler.Assemble([(cover, "v"), (a, "a")], out1);
        PdfAssembler.Assemble([(cover, "v")], out2);

        Assert.Equal(2, PdfReader.Open(out1, PdfDocumentOpenMode.Import).PageCount);
        Assert.Equal(1, PdfReader.Open(out2, PdfDocumentOpenMode.Import).PageCount);
    }

    [Fact]
    public void Assemble_MissingPage_ThrowsFileNotFound()
    {
        var cover = MakePdf("page_1", 1, 100);
        var missing = Path.Combine(_dir, "page_99.pdf");

        Assert.Throws<FileNotFoundException>(() =>
            PdfAssembler.Assemble([(cover, "v"), (missing, "x")], Path.Combine(_dir, "out.pdf")));
    }

    [Fact]
    public void Assemble_NoPages_Throws()
    {
        Assert.Throws<ArgumentException>(() => PdfAssembler.Assemble([], Path.Combine(_dir, "out.pdf")));
    }
}
