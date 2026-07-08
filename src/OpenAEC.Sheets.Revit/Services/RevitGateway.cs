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

    public Task<IReadOnlyList<SheetItem>> GetSheetsAsync() =>
        _handler.ExecuteAsync<IReadOnlyList<SheetItem>>(app =>
        {
            var doc = app.ActiveUIDocument.Document;
            var items = new List<SheetItem>();

            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder)
                .OrderBy(s => s.SheetNumber, StringComparer.OrdinalIgnoreCase);

            foreach (var sheet in sheets)
            {
                var item = new SheetItem
                {
                    Id = sheet.Id.Value,
                    IsSheet = true,
                    Number = sheet.SheetNumber,
                    Name = sheet.Name,
                    Revision = sheet.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION)?.AsString() ?? "",
                    Size = GetSheetSize(doc, sheet),
                    Parameters = CollectParameters(sheet),
                };
                _itemCache[item.Id] = item;
                items.Add(item);
            }

            return items;
        });

    public Task<IReadOnlyList<SheetItem>> GetViewsAsync() =>
        _handler.ExecuteAsync<IReadOnlyList<SheetItem>>(app =>
        {
            var doc = app.ActiveUIDocument.Document;
            var items = new List<SheetItem>();

            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate
                            && v.CanBePrinted
                            && v.ViewType != ViewType.DrawingSheet)
                .OrderBy(v => v.ViewType.ToString())
                .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var view in views)
            {
                var item = new SheetItem
                {
                    Id = view.Id.Value,
                    IsSheet = false,
                    Number = view.ViewType.ToString(),
                    Name = view.Name,
                    Revision = "",
                    Size = "",
                    Parameters = CollectParameters(view),
                };
                _itemCache[item.Id] = item;
                items.Add(item);
            }

            return items;
        });

    public Task<IReadOnlyList<string>> GetViewSheetSetNamesAsync() =>
        _handler.ExecuteAsync<IReadOnlyList<string>>(app =>
            new FilteredElementCollector(app.ActiveUIDocument.Document)
                .OfClass(typeof(ViewSheetSet))
                .Cast<ViewSheetSet>()
                .Select(s => s.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList());

    public Task<IReadOnlyList<long>> GetViewSheetSetIdsAsync(string setName) =>
        _handler.ExecuteAsync<IReadOnlyList<long>>(app =>
        {
            var set = new FilteredElementCollector(app.ActiveUIDocument.Document)
                .OfClass(typeof(ViewSheetSet))
                .Cast<ViewSheetSet>()
                .FirstOrDefault(s => s.Name == setName);

            if (set is null) return [];

            var ids = new List<long>();
            foreach (View view in set.Views)
                ids.Add(view.Id.Value);
            return ids;
        });

    public Task<IReadOnlyList<string>> GetDwgSetupNamesAsync() =>
        _handler.ExecuteAsync<IReadOnlyList<string>>(app =>
            new FilteredElementCollector(app.ActiveUIDocument.Document)
                .OfClass(typeof(ExportDWGSettings))
                .Cast<ExportDWGSettings>()
                .Select(s => s.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList());

    public Task<IReadOnlyList<string>> GetDgnSetupNamesAsync() =>
        _handler.ExecuteAsync<IReadOnlyList<string>>(app =>
            new FilteredElementCollector(app.ActiveUIDocument.Document)
                .OfClass(typeof(ExportDGNSettings))
                .Cast<ExportDGNSettings>()
                .Select(s => s.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList());

    public Task<IReadOnlyList<string>> GetSheetParameterNamesAsync() =>
        Task.FromResult<IReadOnlyList<string>>(
            _itemCache.Values
                .Where(i => i.IsSheet)
                .SelectMany(i => i.Parameters.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList());

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

    private static string GetSheetSize(Document doc, ViewSheet sheet)
    {
        var titleBlock = new FilteredElementCollector(doc, sheet.Id)
            .OfCategory(BuiltInCategory.OST_TitleBlocks)
            .FirstElement();

        if (titleBlock is null) return "";

        var width = titleBlock.get_Parameter(BuiltInParameter.SHEET_WIDTH)?.AsDouble() ?? 0;
        var height = titleBlock.get_Parameter(BuiltInParameter.SHEET_HEIGHT)?.AsDouble() ?? 0;
        if (width <= 0 || height <= 0) return "";

        return $"{FeetToMm(width)}x{FeetToMm(height)}";
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
            PaperFormat = ExportPaperFormat.Default,
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
        options.ExportObjectData = s.ExportElementProperties;
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
        var fileType = s.Format switch
        {
            Core.Models.ImageFormat.Jpeg => ImageFileType.JPEGLossless,
            Core.Models.ImageFormat.Bmp => ImageFileType.BMP,
            Core.Models.ImageFormat.Tiff => ImageFileType.TIFF,
            _ => ImageFileType.PNG,
        };

        var options = new ImageExportOptions
        {
            ExportRange = ExportRange.SetOfViews,
            FilePath = Path.Combine(folder, job.FileName),
            HLRandWFViewsFileType = fileType,
            ShadowViewsFileType = fileType,
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
