using CommunityToolkit.Mvvm.ComponentModel;

using OpenAEC.Sheets.Core.Models;

namespace OpenAEC.Sheets.UI.ViewModels;

/// <summary>Rij in de exportvoorbeeld-grid met live status tijdens het exporteren.</summary>
public sealed partial class JobRowViewModel : ObservableObject
{
    public ExportJob Job { get; }

    [ObservableProperty]
    private string _status = "";

    public JobRowViewModel(ExportJob job)
    {
        Job = job;
    }

    public string Number => Job.DisplayNumber;
    public string Name => Job.DisplayName;
    public string Format => Job.DisplayFormat;
    public string FileName => Job.FileName;
}
