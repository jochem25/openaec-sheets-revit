using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using OpenAEC.Sheets.Core.Models;

namespace OpenAEC.Sheets.UI.ViewModels;

/// <summary>Weergaveoptie voor de operator-ComboBox: enum-waarde + Nederlands label.</summary>
public sealed record FilterOperatorOption(FilterOperator Value, string Label);

/// <summary>Rij in de regel-editor van een printset — wrapper om een FilterRule.</summary>
public sealed partial class FilterRuleViewModel : ObservableObject
{
    public FilterRule Rule { get; }

    private readonly Action<FilterRuleViewModel> _onRemove;

    public FilterRuleViewModel(FilterRule rule, Action<FilterRuleViewModel> onRemove)
    {
        Rule = rule;
        _onRemove = onRemove;
    }

    public string Parameter
    {
        get => Rule.Parameter;
        set
        {
            if (Rule.Parameter == value) return;
            Rule.Parameter = value ?? "";
            OnPropertyChanged();
        }
    }

    public FilterOperator Operator
    {
        get => Rule.Operator;
        set
        {
            if (Rule.Operator == value) return;
            Rule.Operator = value;
            OnPropertyChanged();
        }
    }

    public string Value
    {
        get => Rule.Value;
        set
        {
            if (Rule.Value == value) return;
            Rule.Value = value ?? "";
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private void Remove() => _onRemove(this);
}
