using System.Windows.Interop;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using OpenAEC.Sheets.Core.Services;
using OpenAEC.Sheets.Revit.Helpers;
using OpenAEC.Sheets.Revit.Services;
using OpenAEC.Sheets.UI.ViewModels;
using OpenAEC.Sheets.UI.Views;

namespace OpenAEC.Sheets.Revit.Commands;

[Transaction(TransactionMode.Manual)]
public sealed class ShowExporterCommand : IExternalCommand
{
    private static MainWindow? _window;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            if (_window is { IsLoaded: true })
            {
                _window.Activate();
                return Result.Succeeded;
            }

            var uiApp = commandData.Application;
            var doc = uiApp.ActiveUIDocument?.Document;
            if (doc is null)
            {
                message = "Geen actief document.";
                return Result.Cancelled;
            }

            // ExternalEvent moet binnen een geldige API-context aangemaakt worden
            var handler = new ExternalEventHandler("OpenAEC Sheet Exporter");
            handler.Initialize();

            var gateway = new RevitGateway(handler, doc.Title);
            var viewModel = new MainViewModel(gateway, new ProfileStore());

            _window = new MainWindow(viewModel);
            _ = new WindowInteropHelper(_window) { Owner = uiApp.MainWindowHandle };
            _window.Closed += (_, _) => _window = null;
            _window.Show();

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            PluginLogger.LogException(ex);
            message = ex.Message;
            return Result.Failed;
        }
    }
}
