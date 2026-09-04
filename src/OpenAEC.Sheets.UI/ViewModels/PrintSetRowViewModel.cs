using CommunityToolkit.Mvvm.ComponentModel;

using OpenAEC.Sheets.Core.Models;

namespace OpenAEC.Sheets.UI.ViewModels;

/// <summary>Rij in de printset-voorbeeldgrid — wrapper om een SheetItem met selectiestatus.</summary>
public sealed partial class PrintSetRowViewModel : ObservableObject
{
    public SheetItem Item { get; }

    [ObservableProperty]
    private bool _isSelected;

    public PrintSetRowViewModel(SheetItem item)
    {
        Item = item;
    }

    public string Number => Item.Number;
    public string Name => Item.Name;
}
