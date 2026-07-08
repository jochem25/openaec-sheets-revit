using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenAEC.Sheets.Revit.Resources;

/// <summary>
/// Genereert WPF-ribbon-iconen runtime met drawing primitives.
/// Amber #D97706 op transparante achtergrond — zelfde stijl als de BCF-plugin.
/// </summary>
internal static class RibbonIcons
{
    private static readonly Color AmberColor = (Color)ColorConverter.ConvertFromString("#D97706");

    internal static BitmapSource SheetExporter => RenderIcon(DrawSheetStackIcon, 32);
    internal static BitmapSource SheetExporterSmall => RenderIcon(DrawSheetStackIcon, 16);

    private static BitmapSource RenderIcon(Action<DrawingContext, double> drawAction, int size)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            drawAction(dc, size);
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Sheet Exporter icoon — stapel bladen met een export-pijl naar rechts.
    /// </summary>
    private static void DrawSheetStackIcon(DrawingContext dc, double size)
    {
        double stroke = size >= 32 ? 1.5 : 1.0;
        var brush = new SolidColorBrush(AmberColor);
        brush.Freeze();
        var pen = new Pen(brush, stroke);
        pen.Freeze();

        double p = size * 0.10;
        double sheetW = size * 0.48;
        double sheetH = size * 0.62;
        double offset = size * 0.09;

        // Achterste twee bladen (verschoven)
        dc.DrawRectangle(null, pen, new Rect(p + offset * 2, p, sheetW, sheetH));
        dc.DrawRectangle(null, pen, new Rect(p + offset, p + offset, sheetW, sheetH));

        // Voorste blad (gevuld wit met rand voor diepte)
        var white = new SolidColorBrush(Colors.White) { Opacity = 0.9 };
        white.Freeze();
        dc.DrawRectangle(white, pen, new Rect(p, p + offset * 2, sheetW, sheetH));

        // Export-pijl rechtsonder
        double arrowStroke = size >= 32 ? 2.5 : 1.5;
        var arrowPen = new Pen(brush, arrowStroke) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        arrowPen.Freeze();

        double ay = size * 0.72;
        double axStart = size * 0.42;
        double axEnd = size * 0.90;
        double head = size * 0.14;

        dc.DrawLine(arrowPen, new Point(axStart, ay), new Point(axEnd, ay));
        dc.DrawLine(arrowPen, new Point(axEnd - head, ay - head), new Point(axEnd, ay));
        dc.DrawLine(arrowPen, new Point(axEnd - head, ay + head), new Point(axEnd, ay));
    }
}
