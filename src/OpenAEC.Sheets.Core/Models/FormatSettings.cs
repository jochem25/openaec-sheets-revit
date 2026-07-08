namespace OpenAEC.Sheets.Core.Models;

public enum ExportFormat
{
    Pdf,
    Dwg,
    Dgn,
    Dwf,
    Nwc,
    Ifc,
    Img,
    Xml,
}

public enum ColorMode
{
    Color,
    GrayScale,
    BlackAndWhite,
}

public enum RasterQuality
{
    Low,
    Medium,
    High,
    Presentation,
}

public sealed class PdfSettings
{
    public bool Combine { get; set; }
    public string CombinedFileName { get; set; } = "";
    public bool VectorHiddenLines { get; set; } = true;
    public RasterQuality RasterQuality { get; set; } = RasterQuality.High;
    public ColorMode Colors { get; set; } = ColorMode.Color;
    public bool FitToPage { get; set; } = true;
    public int ZoomPercentage { get; set; } = 100;
    public bool CenterPaperPlacement { get; set; } = true;
    public double OriginOffsetXMm { get; set; }
    public double OriginOffsetYMm { get; set; }
    public bool ViewLinksInBlue { get; set; }
    public bool HideRefWorkPlanes { get; set; } = true;
    public bool HideUnreferencedViewTags { get; set; } = true;
    public bool HideScopeBoxes { get; set; } = true;
    public bool HideCropBoundaries { get; set; } = true;
    public bool ReplaceHalftoneWithThinLines { get; set; }
    public bool MaskCoincidentLines { get; set; }
}

public sealed class DwgSettings
{
    /// <summary>Naam van een in het model opgeslagen DWG export setup. Leeg = Revit default.</summary>
    public string ExportSetupName { get; set; } = "";
    public bool ExportViewsOnSheetsAsXrefs { get; set; }
}

public sealed class DgnSettings
{
    public string ExportSetupName { get; set; } = "";
}

public sealed class DwfSettings
{
    public bool UseDwfx { get; set; } = true;
    public bool Combine { get; set; }
    public string CombinedFileName { get; set; } = "";
    public bool LosslessImages { get; set; } = true;
    public bool ExportElementProperties { get; set; } = true;
    public bool CropBoxVisible { get; set; }
}

public sealed class NwcSettings
{
    public bool ConvertElementProperties { get; set; } = true;
    public bool UseSharedCoordinates { get; set; } = true;
    public bool ExportLinks { get; set; }
}

public sealed class IfcSettings
{
    /// <summary>IFC2x2 | IFC2x3CV2 | IFC4RV | IFC4DTV</summary>
    public string Version { get; set; } = "IFC2x3CV2";
    public bool WallAndColumnSplitting { get; set; }
    public bool ExportBaseQuantities { get; set; }
    /// <summary>0 = geen, 1 = 1st level, 2 = 2nd level</summary>
    public int SpaceBoundaries { get; set; }
    public bool IncludeSteelElements { get; set; } = true;
    public bool VisibleElementsOfCurrentView { get; set; } = true;
    public bool Export2DElements { get; set; }
    public bool ExportInternalRevitPropertySets { get; set; }
    public bool ExportIfcCommonPropertySets { get; set; } = true;
    public bool ExportSchedulesAsPsets { get; set; }
    public bool ExportUserDefinedPsets { get; set; }
    public string UserDefinedPsetsPath { get; set; } = "";
    public bool ExportPartsAsBuildingElements { get; set; }
    public bool UseActiveViewGeometry { get; set; }
    public bool ExportBoundingBox { get; set; }
    public bool StoreIfcGuid { get; set; }
}

public enum ImageFormat
{
    Png,
    Jpeg,
    Bmp,
    Tiff,
}

public sealed class ImgSettings
{
    public ImageFormat Format { get; set; } = ImageFormat.Png;
    public bool FitToPixelSize { get; set; } = true;
    public int PixelSize { get; set; } = 2048;
    public bool FitDirectionHorizontal { get; set; } = true;
    public int ZoomPercentage { get; set; } = 50;
    /// <summary>72 | 150 | 300 | 600</summary>
    public int Dpi { get; set; } = 150;
}

public sealed class XmlSettings
{
    /// <summary>Parameternamen die per sheet/view als XML-element worden weggeschreven. Leeg = alle parameters.</summary>
    public List<string> SelectedParameters { get; set; } = [];
    public string FileName { get; set; } = "";
}
