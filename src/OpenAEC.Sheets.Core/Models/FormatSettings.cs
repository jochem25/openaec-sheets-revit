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

public enum PdfFileMode
{
    /// <summary>Eén PDF per sheet/view.</summary>
    Separate,
    /// <summary>Alles gecombineerd in één PDF.</summary>
    CombineAll,
    /// <summary>Eén gecombineerde PDF per unieke waarde van een sheetparameter.</summary>
    CombineByParameter,
}

public sealed class PdfSettings
{
    public PdfFileMode FileMode { get; set; } = PdfFileMode.Separate;
    /// <summary>Sheetparameter waarop gegroepeerd wordt bij CombineByParameter.</summary>
    public string GroupByParameter { get; set; } = "";
    /// <summary>Bestandsnaam bij CombineAll; bij CombineByParameter gebruikt als prefix.</summary>
    public string CombinedFileName { get; set; } = "";
    /// <summary>
    /// Bij CombineByParameter: splits de parameterwaarde op <see cref="GroupValueSeparators"/>
    /// en maak per deel een eigen gecombineerde PDF. Eén blad kan zo in meerdere boekjes komen
    /// (bijv. "plattegronden;plattegronden-noord"). Standaard uit = één blad in één groep.
    /// </summary>
    public bool SplitGroupValues { get; set; }
    /// <summary>Scheidingstekens voor <see cref="SplitGroupValues"/>; elk teken splitst afzonderlijk.</summary>
    public string GroupValueSeparators { get; set; } = DefaultGroupValueSeparators;

    public const string DefaultGroupValueSeparators = ";,";
    /// <summary>
    /// Bij <see cref="SplitGroupValues"/>: een gesplitst token mag een glob-patroon zijn
    /// (<c>*</c> = 0 of meer tekens, <c>?</c> = 1 teken) en wordt geëxpandeerd tegen de concrete
    /// boekjesnamen van de selectie. Zo komt een voorblad met "*" in elk boekje en een
    /// situatietekening met "Z_*" in alle woningboekjes. Een patroon maakt nooit zelf een boekje aan.
    /// </summary>
    public bool ExpandWildcards { get; set; } = true;
    /// <summary>
    /// Bij CombineByParameter met overlappende boekjes: elk blad één keer door Revit laten
    /// renderen (tijdelijke PDF per blad) en de boekjes daarna samenstellen door pagina's te
    /// mergen, i.p.v. een blad dat in 77 boekjes zit 77× te exporteren. Zonder overlap gebeurt
    /// niets bijzonders (native export). Bij een fout in het samenstellen valt een boekje
    /// automatisch terug op de native gecombineerde export.
    /// </summary>
    public bool AssembleBooklets { get; set; } = true;
    public bool VectorHiddenLines { get; set; } = true;
    public RasterQuality RasterQuality { get; set; } = RasterQuality.Presentation;
    public ColorMode Colors { get; set; } = ColorMode.Color;
    /// <summary>Export-DPI: 72 | 144 | 300 | 600 | 1200</summary>
    public int QualityDpi { get; set; } = 600;
    /// <summary>"Default" (= sheetformaat) of ExportPaperFormat-naam: ISO_A0..A4, ISO_B1..B4, ANSI_A..E, ARCH_A..E3.</summary>
    public string PaperFormat { get; set; } = "Default";
    /// <summary>"Auto" | "Portrait" | "Landscape"</summary>
    public string Orientation { get; set; } = "Auto";
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
    /// <summary>"Default" | "R2007" | "R2010" | "R2013" | "R2018" — overschrijft de setup-versie.</summary>
    public string FileVersion { get; set; } = "Default";
    public bool UseSharedCoordinates { get; set; }
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
    /// <summary>"Default" | "Low" | "Medium" | "High" (alleen relevant bij lossy)</summary>
    public string ImageQuality { get; set; } = "Default";
    public bool ExportElementProperties { get; set; } = true;
    public bool ExportTextures { get; set; } = true;
    public bool CropBoxVisible { get; set; }
}

public sealed class NwcSettings
{
    public bool ConvertElementProperties { get; set; } = true;
    public bool UseSharedCoordinates { get; set; } = true;
    public bool ExportLinks { get; set; }
    public bool DivideFileIntoLevels { get; set; } = true;
    public bool ExportElementIds { get; set; } = true;
    public bool ExportParts { get; set; }
    public bool ExportRoomAsAttribute { get; set; } = true;
    public bool ExportRoomGeometry { get; set; }
    public bool ExportUrls { get; set; }
    public bool ConvertLights { get; set; }
    public bool ConvertLinkedCadFormats { get; set; }
    public bool FindMissingMaterials { get; set; }
    /// <summary>Geometrie-nauwkeurigheid, 0.1 (grof) – 10 (fijn). Revit-default 1.0.</summary>
    public double FacetingFactor { get; set; } = 1.0;
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
    public bool ExportLinkedFiles { get; set; }
    public bool ExportRoomsInView { get; set; }
    /// <summary>Detailniveau tessellatie: 0.25 (laag) | 0.5 | 0.75 | 1.0 (hoogst)</summary>
    public double TessellationLevelOfDetail { get; set; } = 0.5;
    /// <summary>Naam van de te exporteren fase. Leeg of "(standaard)" = Revit-default.</summary>
    public string PhaseName { get; set; } = "";
    /// <summary>Naam van een IFC category mapping template in het document. Leeg of "(standaard)" = actieve template.</summary>
    public string CategoryMappingTemplate { get; set; } = "";
    /// <summary>Pad naar een category mapping-bestand (.txt); wordt geïmporteerd en geactiveerd vóór export.</summary>
    public string CategoryMappingFile { get; set; } = "";
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
    /// <summary>Bestandstype voor shaded/gerenderde views.</summary>
    public ImageFormat ShadedFormat { get; set; } = ImageFormat.Png;
    /// <summary>Bestandstype voor lijnwerk-views (hidden line / wireframe).</summary>
    public ImageFormat NonShadedFormat { get; set; } = ImageFormat.Png;
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
