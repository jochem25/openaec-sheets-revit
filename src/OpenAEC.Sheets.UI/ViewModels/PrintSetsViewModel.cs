using System.Collections.ObjectModel;
using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using OpenAEC.Sheets.Core.Models;
using OpenAEC.Sheets.Core.Services;

namespace OpenAEC.Sheets.UI.ViewModels;

/// <summary>
/// Beheert het tabblad "Printsets": filterdefinities op sheetparameters, een levend voorbeeld
/// van de resulterende selectie en het aanmaken/verwijderen van Revit View/Sheet Sets.
/// Definities worden gespiegeld aan <see cref="ExportProfile.PrintSets"/> zodat ze met het
/// profiel worden opgeslagen.
/// </summary>
public sealed partial class PrintSetsViewModel : ObservableObject
{
    private readonly IRevitGateway _gateway;
    private readonly Func<IReadOnlyList<SheetItem>> _sheetsProvider;
    private readonly Func<IReadOnlyList<string>> _existingSetNamesProvider;
    private readonly Action<IReadOnlyDictionary<string, IReadOnlyList<long>>> _onApplied;

    private ExportProfile _profile = new();
    private bool _loadingDefinition;

    public PrintSetsViewModel(
        IRevitGateway gateway,
        Func<IReadOnlyList<SheetItem>> sheetsProvider,
        Func<IReadOnlyList<string>> existingSetNamesProvider,
        Action<IReadOnlyDictionary<string, IReadOnlyList<long>>> onApplied)
    {
        _gateway = gateway;
        _sheetsProvider = sheetsProvider;
        _existingSetNamesProvider = existingSetNamesProvider;
        _onApplied = onApplied;
    }

    // ── Opgeslagen definities ──────────────────────────────────────────────

    public ObservableCollection<PrintSetDefinition> SavedDefinitions { get; } = [];

    [ObservableProperty]
    private PrintSetDefinition? _selectedDefinition;

    /// <summary>Herlaadt de opgeslagen definities uit het (nieuw geladen of net geopende) profiel.</summary>
    public void LoadFromProfile(ExportProfile profile)
    {
        _profile = profile;
        SavedDefinitions.Clear();
        foreach (var def in profile.PrintSets) SavedDefinitions.Add(def);
        SelectedDefinition = SavedDefinitions.FirstOrDefault();
        RefreshParameterNames();
    }

    partial void OnSelectedDefinitionChanged(PrintSetDefinition? value) => LoadEditorFromDefinition(value);

    [RelayCommand]
    private void NewDefinition()
    {
        var def = new PrintSetDefinition { Name = "Nieuwe printset" };
        _profile.PrintSets.Add(def);
        SavedDefinitions.Add(def);
        SelectedDefinition = def;
    }

    [RelayCommand]
    private void DuplicateDefinition()
    {
        if (SelectedDefinition is not { } source) return;

        var copy = new PrintSetDefinition
        {
            Name = source.Name + " (kopie)",
            Combine = source.Combine,
            Rules = source.Rules.Select(r => new FilterRule { Parameter = r.Parameter, Operator = r.Operator, Value = r.Value }).ToList(),
            Mode = source.Mode,
            BulkPerParameter = source.BulkPerParameter,
            BulkParameter = source.BulkParameter,
            SplitBulkValues = source.SplitBulkValues,
            ManualIncludes = [.. source.ManualIncludes],
            ManualExcludes = [.. source.ManualExcludes],
        };

        _profile.PrintSets.Add(copy);
        SavedDefinitions.Add(copy);
        SelectedDefinition = copy;
    }

    /// <summary>Verwijdert de definitie alleen uit het profiel — geen Revit-actie.</summary>
    [RelayCommand]
    private void DeleteDefinition()
    {
        if (SelectedDefinition is not { } def) return;
        _profile.PrintSets.Remove(def);
        SavedDefinitions.Remove(def);
        SelectedDefinition = SavedDefinitions.FirstOrDefault();
    }

    // ── Editor ──────────────────────────────────────────────────────────────

    public ObservableCollection<FilterRuleViewModel> Rules { get; } = [];

    public IReadOnlyList<FilterOperatorOption> Operators { get; } =
    [
        new(FilterOperator.Equals, "is gelijk aan"),
        new(FilterOperator.NotEquals, "is niet gelijk aan"),
        new(FilterOperator.Contains, "bevat"),
        new(FilterOperator.StartsWith, "begint met"),
        new(FilterOperator.EndsWith, "eindigt met"),
        new(FilterOperator.Wildcard, "wildcard (* ?)"),
        new(FilterOperator.IsEmpty, "is leeg"),
        new(FilterOperator.IsNotEmpty, "is niet leeg"),
        new(FilterOperator.InList, "in lijst (; of ,)"),
    ];

    public ObservableCollection<string> ParameterNames { get; } = [];

    [ObservableProperty] private bool _combineAll = true;
    [ObservableProperty] private bool _combineAny;
    [ObservableProperty] private string _setName = "";
    [ObservableProperty] private bool _modeOverwrite = true;
    [ObservableProperty] private bool _modeAddOnly;
    [ObservableProperty] private bool _bulkPerParameter;
    [ObservableProperty] private string _bulkParameter = "";
    [ObservableProperty] private bool _splitBulkValues = true;

    private void LoadEditorFromDefinition(PrintSetDefinition? def)
    {
        _loadingDefinition = true;
        try
        {
            Rules.Clear();
            if (def is not null)
                foreach (var rule in def.Rules) AddRuleRow(rule);

            CombineAll = def?.Combine != FilterCombine.Any;
            CombineAny = def?.Combine == FilterCombine.Any;
            SetName = def?.Name ?? "";
            ModeOverwrite = def?.Mode != PrintSetMode.AddOnly;
            ModeAddOnly = def?.Mode == PrintSetMode.AddOnly;
            BulkPerParameter = def?.BulkPerParameter ?? false;
            BulkParameter = def?.BulkParameter ?? "";
            SplitBulkValues = def?.SplitBulkValues ?? true;
        }
        finally
        {
            _loadingDefinition = false;
        }
        RefreshPreview();
    }

    partial void OnCombineAllChanged(bool value)
    {
        if (_loadingDefinition || !value || SelectedDefinition is not { } def) return;
        def.Combine = FilterCombine.All;
        RefreshPreview();
    }

    partial void OnCombineAnyChanged(bool value)
    {
        if (_loadingDefinition || !value || SelectedDefinition is not { } def) return;
        def.Combine = FilterCombine.Any;
        RefreshPreview();
    }

    partial void OnSetNameChanged(string value)
    {
        if (_loadingDefinition || SelectedDefinition is not { } def) return;
        def.Name = value ?? "";
    }

    partial void OnModeOverwriteChanged(bool value)
    {
        if (_loadingDefinition || !value || SelectedDefinition is not { } def) return;
        def.Mode = PrintSetMode.Overwrite;
    }

    partial void OnModeAddOnlyChanged(bool value)
    {
        if (_loadingDefinition || !value || SelectedDefinition is not { } def) return;
        def.Mode = PrintSetMode.AddOnly;
    }

    partial void OnBulkPerParameterChanged(bool value)
    {
        if (_loadingDefinition || SelectedDefinition is not { } def) return;
        def.BulkPerParameter = value;
        RefreshPreview();
    }

    partial void OnBulkParameterChanged(string value)
    {
        if (_loadingDefinition || SelectedDefinition is not { } def) return;
        def.BulkParameter = value ?? "";
        RefreshPreview();
    }

    partial void OnSplitBulkValuesChanged(bool value)
    {
        if (_loadingDefinition || SelectedDefinition is not { } def) return;
        def.SplitBulkValues = value;
        RefreshPreview();
    }

    [RelayCommand]
    private void AddRule()
    {
        if (SelectedDefinition is not { } def) return;
        var rule = new FilterRule();
        def.Rules.Add(rule);
        AddRuleRow(rule);
        RefreshPreview();
    }

    private void AddRuleRow(FilterRule rule)
    {
        var row = new FilterRuleViewModel(rule, RemoveRule);
        row.PropertyChanged += (_, _) => RefreshPreview();
        Rules.Add(row);
    }

    private void RemoveRule(FilterRuleViewModel row)
    {
        if (SelectedDefinition is { } def) def.Rules.Remove(row.Rule);
        Rules.Remove(row);
        RefreshPreview();
    }

    /// <summary>Alle parameternamen voor de kiezer: unie van intrinsieke velden en Parameters-keys van alle sheets.</summary>
    public void RefreshParameterNames()
    {
        ParameterNames.Clear();
        IReadOnlyList<string> intrinsic = ["Sheet Number", "Sheet Name", "Current Revision", "Size"];
        var names = intrinsic
            .Concat(_sheetsProvider().SelectMany(s => s.Parameters.Keys))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        foreach (var name in names) ParameterNames.Add(name);
    }

    // ── Voorbeeld ───────────────────────────────────────────────────────────

    public ObservableCollection<PrintSetRowViewModel> Preview { get; } = [];

    [ObservableProperty] private string _previewStatus = "";

    [RelayCommand]
    private void RefreshPreview()
    {
        Preview.Clear();
        var def = SelectedDefinition;
        var sheets = _sheetsProvider();

        if (def is null)
        {
            PreviewStatus = "Geen printset geselecteerd.";
            return;
        }

        var selectedIds = new HashSet<long>(PrintSetEngine.Apply(sheets, def).Select(i => i.Id));
        foreach (var sheet in sheets)
        {
            var row = new PrintSetRowViewModel(sheet) { IsSelected = selectedIds.Contains(sheet.Id) };
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PrintSetRowViewModel.IsSelected)) OnPreviewRowToggled(row, def);
            };
            Preview.Add(row);
        }

        UpdatePreviewStatus(def);
    }

    /// <summary>
    /// Checkbox in het voorbeeld omgezet: het verschil t.o.v. het pure filterresultaat wordt
    /// vastgelegd als ManualIncludes/ManualExcludes; komt de keuze weer overeen met het filter,
    /// dan verdwijnt de uitzondering.
    /// </summary>
    private void OnPreviewRowToggled(PrintSetRowViewModel row, PrintSetDefinition def)
    {
        var pureMatch = PrintSetEngine.Matches(row.Item, def.Rules, def.Combine);
        var id = row.Item.Id;

        def.ManualIncludes.Remove(id);
        def.ManualExcludes.Remove(id);

        if (row.IsSelected != pureMatch)
        {
            if (row.IsSelected) def.ManualIncludes.Add(id);
            else def.ManualExcludes.Add(id);
        }

        UpdatePreviewStatus(def);
    }

    private void UpdatePreviewStatus(PrintSetDefinition def)
    {
        var selected = Preview.Count(r => r.IsSelected);
        var manualCount = def.ManualIncludes.Count + def.ManualExcludes.Count;
        var text = $"{selected} van {Preview.Count} sheets geselecteerd — {manualCount} handmatige uitzondering{(manualCount == 1 ? "" : "en")}";

        if (def.BulkPerParameter && !string.IsNullOrWhiteSpace(def.BulkParameter))
        {
            var sets = PrintSetEngine.BuildSets(_sheetsProvider(), def);
            text += $"   ·   → {sets.Count} sets";
        }

        PreviewStatus = text;
    }

    // ── Toepassen in Revit ──────────────────────────────────────────────────

    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    private async Task ApplySetAsync()
    {
        if (IsBusy || SelectedDefinition is not { } def) return;

        var sets = PrintSetEngine.BuildSets(_sheetsProvider(), def)
            .Select(s => (s.Name, s.Items, def.Mode))
            .ToList();
        await ApplySetsAsync(sets);
    }

    [RelayCommand]
    private async Task ApplyAllSetsAsync()
    {
        if (IsBusy || SavedDefinitions.Count == 0) return;

        var sheets = _sheetsProvider();
        var sets = SavedDefinitions
            .SelectMany(def => PrintSetEngine.BuildSets(sheets, def).Select(s => (s.Name, s.Items, def.Mode)))
            .ToList();
        await ApplySetsAsync(sets);
    }

    private async Task ApplySetsAsync(IReadOnlyList<(string Name, List<SheetItem> Items, PrintSetMode Mode)> sets)
    {
        if (sets.Count == 0)
        {
            PreviewStatus = "Geen sets om aan te maken (filter levert niets op).";
            return;
        }

        if (sets.Any(s => string.IsNullOrWhiteSpace(s.Name)))
        {
            PreviewStatus = "Geef de printset eerst een naam.";
            return;
        }

        IsBusy = true;
        try
        {
            var request = sets
                .Select(s => (s.Name, (IReadOnlyList<long>)s.Items.Select(i => i.Id).ToList(), s.Mode))
                .ToList();
            var progress = new Progress<string>(text => PreviewStatus = text);

            var results = await _gateway.ApplyPrintSetsAsync(request, progress);
            PreviewStatus = string.Join("   ·   ", results);

            var applied = new Dictionary<string, IReadOnlyList<long>>(StringComparer.OrdinalIgnoreCase);
            foreach (var set in sets) applied[set.Name] = set.Items.Select(i => i.Id).ToList();
            _onApplied(applied);

            RefreshExistingSetNames();
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Verwijderen uit Revit ───────────────────────────────────────────────

    public ObservableCollection<string> ExistingRevitSetNames { get; } = [];

    [ObservableProperty] private string? _selectedDeleteSetName;

    [RelayCommand]
    private void RefreshExistingSetNames()
    {
        var current = SelectedDeleteSetName;
        ExistingRevitSetNames.Clear();
        foreach (var name in _existingSetNamesProvider()) ExistingRevitSetNames.Add(name);
        SelectedDeleteSetName = current is not null && ExistingRevitSetNames.Contains(current) ? current : null;
    }

    [RelayCommand]
    private async Task DeleteRevitSetAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(SelectedDeleteSetName)) return;

        var confirm = MessageBox.Show(
            $"Printset '{SelectedDeleteSetName}' verwijderen uit Revit?",
            "Printset verwijderen", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            var name = SelectedDeleteSetName;
            var error = await _gateway.DeletePrintSetAsync(name);
            PreviewStatus = error ?? $"'{name}' verwijderd.";
            if (error is null)
            {
                ExistingRevitSetNames.Remove(name);
                SelectedDeleteSetName = null;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
