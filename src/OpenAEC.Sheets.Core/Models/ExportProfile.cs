namespace OpenAEC.Sheets.Core.Models;

/// <summary>
/// Volledige exportconfiguratie — serialiseerbaar naar JSON zodat teamleden
/// profielen kunnen delen (vgl. ProSheets "Profiles").
/// </summary>
public sealed class ExportProfile
{
    public string Name { get; set; } = "Default";

    public List<ExportFormat> EnabledFormats { get; set; } = [ExportFormat.Pdf];

    /// <summary>
    /// Bestandsnaam-template met parameter-tokens, bijv. "{Sheet Number}_{Sheet Name}_{Current Revision}".
    /// Elke sheet/view-parameter is bruikbaar als token.
    /// </summary>
    public string NamingTemplate { get; set; } = "{Sheet Number}_{Sheet Name}";

    public string OutputFolder { get; set; } = "";

    /// <summary>Per formaat een submap aanmaken (PDF\, DWG\, ...).</summary>
    public bool SplitByFormat { get; set; } = true;

    public PdfSettings Pdf { get; set; } = new();
    public DwgSettings Dwg { get; set; } = new();
    public DgnSettings Dgn { get; set; } = new();
    public DwfSettings Dwf { get; set; } = new();
    public NwcSettings Nwc { get; set; } = new();
    public IfcSettings Ifc { get; set; } = new();
    public ImgSettings Img { get; set; } = new();
    public XmlSettings Xml { get; set; } = new();

    /// <summary>Opgeslagen printset-definities (filters voor View/Sheet Sets); leeg bij oudere profielen.</summary>
    public List<PrintSetDefinition> PrintSets { get; set; } = [];

    public bool IsEnabled(ExportFormat format) => EnabledFormats.Contains(format);
}
