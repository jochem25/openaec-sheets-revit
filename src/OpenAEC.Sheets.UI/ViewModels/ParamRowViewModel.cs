using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenAEC.Sheets.UI.ViewModels;

/// <summary>Aanvinkbare parameternaam voor de XML-export.</summary>
public sealed partial class ParamRowViewModel : ObservableObject
{
    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;

    public ParamRowViewModel(string name, bool isSelected = false)
    {
        Name = name;
        _isSelected = isSelected;
    }
}
