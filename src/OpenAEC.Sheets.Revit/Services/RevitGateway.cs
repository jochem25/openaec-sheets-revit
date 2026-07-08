using System.IO;
using System.Xml.Linq;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using OpenAEC.Sheets.Core.Models;
using OpenAEC.Sheets.Core.Services;
using OpenAEC.Sheets.Revit.Helpers;

using ColorMode = OpenAEC.Sheets.Core.Models.ColorMode;

namespace OpenAEC.Sheets.Revit.Services;

/// <summary>
/// Implementatie van IRevitGateway. Alle Revit API-calls lopen via de
/// ExternalEventHandler zodat de WPF-UI modeless kan blijven.
/// </summary>
public sealed class RevitGateway : IRevitGateway
{
    private readonly ExternalEventHandler _handler;
    private readonly Dictionary<long, SheetItem> _itemCache = new();

    public string DocumentTitle { get; }

    public RevitGateway(ExternalEventHandler handler, string documentTitle)
    {
        _handler = handler;
        DocumentTitle = documentTitle;
    }

    // ── Lezen ───────────────────────────────────────────────────────────────

    public Task<ModelSnapshot> GetSnapshotAsync(IProgress<string>? progress = null) =>
        _handler.ExecuteAsync(app =>
        {
            var doc = app.ActiveUIDocument.Document;

            // Alle titleblocks in één query i.p.v. één collector per sheet
            var sizeBySheetId = CollectSheetSizes(doc);

            var allSheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder)
                .OrderBy(s => s.SheetNumber, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sheets = new List<SheetItem>();
            foreach (var sheet in allSheets)
            {
                if (sheets.Count % 20 == 0)
                    Report(progress, $"Sheets lezen ({sheets.Count}/{allSheets.Count})…");

                var item = new SheetItem
                {
                    Id = sheet.Id.Value,
                    IsSheet = true,
                    Number = sheet.SheetNumber,
                    Name = sheet.Name,
                    Revision = sheet.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION)?.AsString() ?? "",
                    Size = sizeBySheetId.GetValueOrDefault(sheet.Id, ""),
                    Parameters = CollectParameters(sheet),
                };
                _itemCache[item.Id] = item;
                sheets.Add(item);
            }

            Report(progress, "Views lezen…");

            // Views alleen lichtgewicht inlezen: volledige parametercollectie over
            // honderden views was de grootste kostenpost bij het openen
            var views = new List<SheetItem>();
            foreach (var view in new FilteredElementCollector(doc)
                         .OfClass(typeof(View))
                         .Cast<View>()
                         .Where(v => !v.IsTemplate
                                     && v.CanBePrinted
                                     && v.ViewType != ViewType.DrawingSheet)
                         .OrderBy(v => v.ViewType.ToString())
                         .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase))
            {
                var item = new SheetItem
                {
                    Id = view.Id.Value,
                    IsSheet = false,
                    Number = view.ViewType.ToString(),
                    Name = view.Name,
                    Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["View Name"] = view.Name,
                        ["View Type"] = view.ViewType.ToString(),
                    },
                };
                _itemCache[item.Id] = item;
                views.Add(item);
            }

            Report(progress, "View/Sheet Sets en exportsetups lezen…");

            var sets = new Dictionary<string, IReadOnlyList<long>>();
            foreach (var set in new FilteredElementCollector(doc)
                         .OfClass(typeof(ViewSheetSet))
                         .Cast<ViewSheetSet>()
                         .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            {
                var ids = new List<long>();
                foreach (View view in set.Views)
                    ids.Add(view.Id.Value);
                sets[set.Name] = ids;
            }

            return new ModelSnapshot
            {
                Sheets = sheets,
                Views = views,
                ViewSheetSets = sets,
                DwgSetupNames = SetupNames(doc, typeof(ExportDWGSettings)),
                DgnSetupNames = SetupNames(doc, typeof(ExportDGNSettings)),
            };
        });

    /// <summary>
    /// Meldt voortgang en pompt de WPF-dispatcher zodat de overlay rendert.
    /// Revit-thread == WPF-thread: zonder pompen blijft de UI bevroren tijdens deze call.
    /// Render-prioriteit verwerkt géén muis/toetsenbord-input, dus geen reentrancy.
    /// </summary>
    private static void Report(IProgress<string>? progress, string message)
    {
        if (progress is null) return;
        progress.Report(message);
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
            () => { }, System.Windows.Threading.DispatcherPriority.Render);
    }

    private static Dictionary<ElementId, string> CollectSheetSizes(Document doc)
    {
        var result = new Dictionary<ElementId, string>();
        foreach (var titleBlock in new FilteredElementCollector(doc)
                     .OfCategory(BuiltInCategory.OST_TitleBlocks)
                     .WhereElementIsNotElementType())
        {
            var sheetId = titleBlock.OwnerViewId;
            if (sheetId == ElementId.InvalidElementId || result.ContainsKey(sheetId)) continue;

            var width = titleBlock.get_Parameter(BuiltInParameter.SHEET_WIDTH)?.AsDouble() ?? 0;
            var height = titleBlock.get_Parameter(BuiltInParameter.SHEET_HEIGHT)?.AsDouble() ?? 0;
            if (width > 0 && height > 0)
                result[sheetId] = $"{FeetToMm(width)}x{FeetToMm(height)}";
        }
        return result;
    }

    private static List<string> SetupNames(Document doc, Type setupType) =>
        new FilteredElementCollector(doc)
            .OfClass(setupType)
            .Select(s => s.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static Dictionary<string, string> CollectParameters(Element element)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Parameter param in element.Parameters)
        {
            if (!param.HasValue) continue;
            var name = param.Definition?.Name;
            if (string.IsNullOrEmpty(name) || result.ContainsKey(name)) continue;

            var value = param.StorageType switch
            {
                StorageType.String => param.AsString(),
                StorageType.Integer => param.AsValueString() ?? param.AsInteger().ToString(),
                StorageType.Double => param.AsValueString(),
                StorageType.ElementId => param.AsValueString(),
                _ => null,
            };

            if (!string.IsNullOrEmpty(value))
                result[name] = value!;
        }
        return result;
    }

    private static int FeetToMm(double feet) => (int)Math.Round(feet * 304.8);

    // ── Exporteren ──────────────────────────────────────────────────────────

    public async Task ExportAsync(
        IReadOnlyList<ExportJob> jobs,
        ExportProfile profile,
        string outputFolder,
        IProgress<ExportProgress> progress,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < jobs.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var job = jobs[i];
            string? error = null;
            try
            {
                await _handler.ExecuteAsync(app => ExecuteJob(app.ActiveUIDocument.Document, job, profile, outputFolder));
            }
            catch (Exception ex)
            {
                PluginLogger.LogException(ex);
                error = ex.Message;
            }

            progress.Report(new ExportProgress(i, jobs.Count, job.FileName, error));
        }
    }

    private void ExecuteJob(Document doc, ExportJob job, ExportProfile profile, string baseFolder)
    {
        var folder = profile.SplitByFormat
            ? Path.Combine(baseFolder, job.Format.ToString().ToUpperInvariant())
            : baseFolder;
        Directory.CreateDirectory(folder);

        var ids = job.ElementIds.Select(id => new ElementId(id)).ToList();

        switch (job.Format)
        {
            case ExportFormat.Pdf: ExportPdf(doc, folder, job, ids, profile.Pdf); break;
            case ExportFormat.Dwg: ExportDwg(doc, folder, job, ids, profile.Dwg); break;
            case ExportFormat.Dgn: ExportDgn(doc, folder, job, ids, profile.Dgn); break;
            case ExportFormat.Dwf: ExportDwf(doc, folder, job, ids, profile.Dwf); break;
            case ExportFormat.Nwc: ExportNwc(doc, folder, job, ids, profile.Nwc); break;
            case ExportFormat.Ifc: ExportIfc(doc, folder, job, ids, profile.Ifc); break;
            case ExportFormat.Img: ExportImg(doc, folder, job, ids, profile.Img); break;
            case ExportFormat.Xml: ExportXml(folder, job, profile.Xml); break;
            default: throw new NotSupportedException($"Formaat {job.Format} wordt niet ondersteund.");
        }
    }

    private static void ExportPdf(Document doc, string folder, ExportJob job, IList<ElementId> ids, PdfSettings s)
    {
        var options = new PDFExportOptions
        {
            Combine = true,
            FileName = job.FileName,
            ColorDepth = s.Colors switch
            {
                ColorMode.GrayScale => ColorDepthType.GrayScale,
                ColorMode.BlackAndWhite => ColorDepthType.BlackLine,
                _ => ColorDepthType.Color,
            },
            RasterQuality = s.RasterQuality switch
            {
                Core.Models.RasterQuality.Low => RasterQualityType.Low,
                Core.Models.RasterQuality.Medium => RasterQualityType.Medium,
                Core.Models.RasterQuality.Presentation => RasterQualityType.Presentation,
                _ => RasterQualityType.High,
            },
            AlwaysUseRaster = !s.VectorHiddenLines,
            ExportQuality = s.QualityDpi switch
            {
                72 => PDFExportQualityType.DPI72,
                144 => PDFExportQualityType.DPI144,
                300 => PDFExportQualityType.DPI300,
                1200 => PDFExportQualityType.DPI1200,
                _ => PDFExportQualityType.DPI600,
            },
            PaperFormat = Enum.TryParse<ExportPaperFormat>(s.PaperFormat, out var paper)
                ? paper
                : ExportPaperFormat.Default,
            PaperOrientation = s.Orientation switch
            {
                "Portrait" => PageOrientationType.Portrait,
                "Landscape" => PageOrientationType.Landscape,
                _ => PageOrientationType.Auto,
            },
            ZoomType = s.FitToPage ? ZoomType.FitToPage : ZoomType.Zoom,
            HideCropBoundaries = s.HideCropBoundaries,
            HideReferencePlane = s.HideRefWorkPlanes,
            HideScopeBoxes = s.HideScopeBoxes,
            HideUnreferencedViewTags = s.HideUnreferencedViewTags,
            MaskCoincidentLines = s.MaskCoincidentLines,
            ReplaceHalftoneWithThinLines = s.ReplaceHalftoneWithThinLines,
            ViewLinksInBlue = s.ViewLinksInBlue,
            PaperPlacement = s.CenterPaperPlacement ? PaperPlacementType.Center : PaperPlacementType.LowerLeft,
            StopOnError = false,
        };

        if (!s.FitToPage)
            options.ZoomPercentage = Math.Clamp(s.ZoomPercentage, 1, 500);

        if (!s.CenterPaperPlacement)
        {
            options.OriginOffsetX = s.OriginOffsetXMm / 304.8;
            options.OriginOffsetY = s.OriginOffsetYMm / 304.8;
        }

        doc.Export(folder, ids, options);
    }

    private static void ExportDwg(Document doc, string folder, ExportJob job, ICollection<ElementId> ids, DwgSettings s)
    {
        var options = FindSetup<ExportDWGSettings>(doc, s.ExportSetupName)?.GetDWGExportOptions()
                      ?? new DWGExportOptions();
        options.MergedViews = !s.ExportViewsOnSheetsAsXrefs;
        options.SharedCoords = s.UseSharedCoordinates;
        if (Enum.TryParse<ACADVersion>(s.FileVersion, out var version) && version != ACADVersion.Default)
            options.FileVersion = version;

        doc.Export(folder, job.FileName, ids, options);
    }

    private static void ExportDgn(Document doc, string folder, ExportJob job, ICollection<ElementId> ids, DgnSettings s)
    {
        var options = FindSetup<ExportDGNSettings>(doc, s.ExportSetupName)?.GetDGNExportOptions()
                      ?? new DGNExportOptions();

        doc.Export(folder, job.FileName, ids, options);
    }

    private static void ExportDwf(Document doc, string folder, ExportJob job, IList<ElementId> ids, DwfSettings s)
    {
        var viewSet = new ViewSet();
        foreach (var id in ids)
        {
            if (doc.GetElement(id) is View view)
                viewSet.Insert(view);
        }

        if (s.UseDwfx)
        {
            var options = new DWFXExportOptions();
            ApplyDwfSettings(options, s, ids.Count > 1);
            doc.Export(folder, job.FileName, viewSet, options);
        }
        else
        {
            var options = new DWFExportOptions();
            ApplyDwfSettings(options, s, ids.Count > 1);
            doc.Export(folder, job.FileName, viewSet, options);
        }
    }

    private static void ApplyDwfSettings(DWFExportOptions options, DwfSettings s, bool merged)
    {
        options.MergedViews = merged;
        options.ImageFormat = s.LosslessImages ? DWFImageFormat.Lossless : DWFImageFormat.Lossy;
        options.ImageQuality = Enum.TryParse<DWFImageQuality>(s.ImageQuality, out var quality)
            ? quality
            : DWFImageQuality.Default;
        options.ExportObjectData = s.ExportElementProperties;
        options.ExportTexture = s.ExportTextures;
        options.CropBoxVisible = s.CropBoxVisible;
    }

    private static void ExportNwc(Document doc, string folder, ExportJob job, IList<ElementId> ids, NwcSettings s)
    {
        var options = new NavisworksExportOptions
        {
            ExportScope = NavisworksExportScope.View,
            ViewId = ids[0],
            ConvertElementProperties = s.ConvertElementProperties,
            Coordinates = s.UseSharedCoordinates
                ? NavisworksCoordinates.Shared
                : NavisworksCoordinates.Internal,
            ExportLinks = s.ExportLinks,
            DivideFileIntoLevels = s.DivideFileIntoLevels,
            ExportElementIds = s.ExportElementIds,
            ExportParts = s.ExportParts,
            ExportRoomAsAttribute = s.ExportRoomAsAttribute,
            ExportRoomGeometry = s.ExportRoomGeometry,
            ExportUrls = s.ExportUrls,
            ConvertLights = s.ConvertLights,
            ConvertLinkedCADFormats = s.ConvertLinkedCadFormats,
            FindMissingMaterials = s.FindMissingMaterials,
            FacetingFactor = Math.Clamp(s.FacetingFactor, 0.1, 10.0),
        };

        doc.Export(folder, job.FileName, options);
    }

    private static void ExportIfc(Document doc, string folder, ExportJob job, IList<ElementId> ids, IfcSettings s)
    {
        var options = new IFCExportOptions
        {
            FileVersion = s.Version switch
            {
                "IFC2x2" => IFCVersion.IFC2x2,
                "IFC4RV" => IFCVersion.IFC4RV,
                "IFC4DTV" => IFCVersion.IFC4DTV,
                _ => IFCVersion.IFC2x3CV2,
            },
            WallAndColumnSplitting = s.WallAndColumnSplitting,
            ExportBaseQuantities = s.ExportBaseQuantities,
            SpaceBoundaryLevel = s.SpaceBoundaries,
        };

        if (s.VisibleElementsOfCurrentView)
            options.FilterViewId = ids[0];

        AddBool(options, "ExportInternalRevitPropertySets", s.ExportInternalRevitPropertySets);
        AddBool(options, "ExportIFCCommonPropertySets", s.ExportIfcCommonPropertySets);
        AddBool(options, "ExportSchedulesAsPsets", s.ExportSchedulesAsPsets);
        AddBool(options, "Export2DElements", s.Export2DElements);
        AddBool(options, "VisibleElementsOfCurrentView", s.VisibleElementsOfCurrentView);
        AddBool(options, "IncludeSteelElements", s.IncludeSteelElements);
        AddBool(options, "ExportPartsAsBuildingElements", s.ExportPartsAsBuildingElements);
        AddBool(options, "UseActiveViewGeometry", s.UseActiveViewGeometry);
        AddBool(options, "ExportBoundingBox", s.ExportBoundingBox);
        AddBool(options, "StoreIFCGUID", s.StoreIfcGuid);
        AddBool(options, "ExportLinkedFiles", s.ExportLinkedFiles);
        AddBool(options, "ExportRoomsInView", s.ExportRoomsInView);
        options.AddOption("TessellationLevelOfDetail",
            Math.Clamp(s.TessellationLevelOfDetail, 0.1, 1.0).ToString(System.Globalization.CultureInfo.InvariantCulture));

        if (s.ExportUserDefinedPsets && !string.IsNullOrWhiteSpace(s.UserDefinedPsetsPath))
        {
            AddBool(options, "ExportUserDefinedPsets", true);
            options.AddOption("ExportUserDefinedPsetsFileName", s.UserDefinedPsetsPath);
        }

        // IFC-export vereist een open transaction
        using var transaction = new Transaction(doc, "OpenAEC IFC Export");
        transaction.Start();
        doc.Export(folder, job.FileName, options);
        if (s.StoreIfcGuid)
            transaction.Commit();
        else
            transaction.RollBack();
    }

    private static void AddBool(IFCExportOptions options, string name, bool value) =>
        options.AddOption(name, value ? "true" : "false");

    private static void ExportImg(Document doc, string folder, ExportJob job, IList<ElementId> ids, ImgSettings s)
    {
        var options = new ImageExportOptions
        {
            ExportRange = ExportRange.SetOfViews,
            FilePath = Path.Combine(folder, job.FileName),
            HLRandWFViewsFileType = ToImageFileType(s.NonShadedFormat),
            ShadowViewsFileType = ToImageFileType(s.ShadedFormat),
            ImageResolution = s.Dpi switch
            {
                72 => ImageResolution.DPI_72,
                300 => ImageResolution.DPI_300,
                600 => ImageResolution.DPI_600,
                _ => ImageResolution.DPI_150,
            },
        };

        if (s.FitToPixelSize)
        {
            options.ZoomType = ZoomFitType.FitToPage;
            options.PixelSize = Math.Clamp(s.PixelSize, 32, 15000);
            options.FitDirection = s.FitDirectionHorizontal
                ? FitDirectionType.Horizontal
                : FitDirectionType.Vertical;
        }
        else
        {
            options.ZoomType = ZoomFitType.Zoom;
            options.Zoom = Math.Clamp(s.ZoomPercentage, 1, 100);
        }

        options.SetViewsAndSheets(ids);
        doc.ExportImage(options);
    }

    private static ImageFileType ToImageFileType(Core.Models.ImageFormat format) => format switch
    {
        Core.Models.ImageFormat.Jpeg => ImageFileType.JPEGLossless,
        Core.Models.ImageFormat.Bmp => ImageFileType.BMP,
        Core.Models.ImageFormat.Tiff => ImageFileType.TIFF,
        _ => ImageFileType.PNG,
    };

    private void ExportXml(string folder, ExportJob job, XmlSettings s)
    {
        var selected = new HashSet<string>(s.SelectedParameters, StringComparer.OrdinalIgnoreCase);

        var root = new XElement("sheets",
            new XAttribute("document", DocumentTitle),
            new XAttribute("exported", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")));

        foreach (var id in job.ElementIds)
        {
            if (!_itemCache.TryGetValue(id, out var item)) continue;

            var element = new XElement(item.IsSheet ? "sheet" : "view",
                new XAttribute("id", item.Id),
                new XAttribute("number", item.Number),
                new XAttribute("name", item.Name));

            foreach (var (name, value) in item.Parameters.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (selected.Count > 0 && !selected.Contains(name)) continue;
                element.Add(new XElement("parameter", new XAttribute("name", name), value));
            }

            root.Add(element);
        }

        new XDocument(root).Save(Path.Combine(folder, job.FileName + ".xml"));
    }

    private static T? FindSetup<T>(Document doc, string name) where T : Element =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : new FilteredElementCollector(doc)
                .OfClass(typeof(T))
                .Cast<T>()
                .FirstOrDefault(s => s.Name == name);
}
