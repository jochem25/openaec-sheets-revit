using Autodesk.Revit.UI;

using OpenAEC.Sheets.Revit.Commands;
using OpenAEC.Sheets.Revit.Helpers;
using OpenAEC.Sheets.Revit.Resources;

namespace OpenAEC.Sheets.Revit;

public sealed class App : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        PluginLogger.Log("=== OnStartup BEGIN ===");

        try
        {
            CreateRibbon(application);
            PluginLogger.Log("=== OnStartup SUCCEEDED ===");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            PluginLogger.LogException(ex);
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;

    private static void CreateRibbon(UIControlledApplication app)
    {
        const string TAB_NAME = "OpenAEC";
        const string PANEL_NAME = "Sheets";

        try
        {
            app.CreateRibbonTab(TAB_NAME);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException)
        {
            // Tab bestaat al (bijv. aangemaakt door de BCF-plugin)
        }

        var panel = app.CreateRibbonPanel(TAB_NAME, PANEL_NAME);
        var assemblyPath = typeof(App).Assembly.Location;

        var buttonData = new PushButtonData(
            "OpenAecSheetExporter",
            "Sheet\nExporter",
            assemblyPath,
            typeof(ShowExporterCommand).FullName
        )
        {
            ToolTip = "Batch-export van sheets en views naar PDF, DWG, DGN, DWF, NWC, IFC, afbeeldingen en XML",
            LargeImage = RibbonIcons.SheetExporter,
            Image = RibbonIcons.SheetExporterSmall,
        };
        panel.AddItem(buttonData);

        PluginLogger.Log("Ribbon created");
    }
}
