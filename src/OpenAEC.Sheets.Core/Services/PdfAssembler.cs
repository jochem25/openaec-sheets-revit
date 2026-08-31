using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace OpenAEC.Sheets.Core.Services;

/// <summary>
/// Stelt een boekje samen uit losse PDF's (één per blad) door de pagina's in volgorde over te
/// nemen; per blad komt een bookmark. Geen Revit-afhankelijkheid, dus draait naast de Revit-thread.
/// </summary>
public static class PdfAssembler
{
    /// <summary>Voegt de pagina's van <paramref name="pages"/> in volgorde samen tot <paramref name="outputPath"/>.</summary>
    /// <returns>Aantal overgenomen pagina's.</returns>
    public static int Assemble(IReadOnlyList<(string Path, string Title)> pages, string outputPath)
    {
        if (pages.Count == 0) throw new ArgumentException("Geen bladen om samen te stellen.", nameof(pages));

        using var output = new PdfDocument();
        var count = 0;

        foreach (var (path, title) in pages)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Tijdelijk blad ontbreekt: " + Path.GetFileName(path), path);

            using var input = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            PdfPage? first = null;
            for (var i = 0; i < input.PageCount; i++)
            {
                var added = output.AddPage(input.Pages[i]);
                first ??= added;
                count++;
            }

            if (first is not null && !string.IsNullOrWhiteSpace(title))
                output.Outlines.Add(title, first, false);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        output.Save(outputPath);
        return count;
    }
}
