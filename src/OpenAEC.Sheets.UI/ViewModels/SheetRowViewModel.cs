using CommunityToolkit.Mvvm.ComponentModel;

using OpenAEC.Sheets.Core.Models;

namespace OpenAEC.Sheets.UI.ViewModels;

/// <summary>Rij in de selectiegrid — wrapper om een SheetItem met selectiestatus.</summary>
public sealed partial class SheetRowViewModel : ObservableObject
{
    public SheetItem Item { get; }

    [ObservableProperty]
    private bool _isSelected;

    public SheetRowViewModel(SheetItem item)
    {
        Item = item;
    }

    public string Number => Item.Number;
    public string Name => Item.Name;
    public string Revision => Item.Revision;
    public string Size => Item.Size;

    public string CustomFileName
    {
        get => Item.CustomFileName;
        set
        {
            if (Item.CustomFileName == value) return;
            Item.CustomFileName = value;
            OnPropertyChanged();
        }
    }
}
