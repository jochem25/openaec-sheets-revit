using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using OpenAEC.Sheets.Core.Models;
using OpenAEC.Sheets.Core.Services;

namespace OpenAEC.Sheets.UI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IRevitGateway _gateway;
    private readonly ProfileStore _profileStore;

    private List<SheetRowViewModel> _sheetRows = [];
    private List<SheetRowViewModel> _viewRows = [];
    private Dictionary<string, HashSet<long>> _setContents = new();
    private CancellationTokenSource? _exportCts;

    public MainViewModel(IRevitGateway gateway, ProfileStore profileStore)
    {
        _gateway = gateway;
        _profileStore = profileStore;
        _profile = new ExportProfile();
    }

    // ── Selectie ────────────────────────────────────────────────────────────

    public ObservableCollection<SheetRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    private bool _showSheets = true;

    [ObservableProperty]
    private string _searchText = "";

    public ObservableCollection<string> SetFilters { get; } = [];

    [ObservableProperty]
    private string _selectedSetFilter = ALL_SETS;

    private const string ALL_SETS = "Alle sheets/views";

    [ObservableProperty]
    private string _statusText = "";

    partial void OnShowSheetsChanged(bool value) => ApplyFilter();
    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedSetFilterChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void SelectAllVisible()
    {
        var target = !Rows.All(r => r.IsSelected);
        foreach (var row in Rows) row.IsSelected = target;
        UpdateStatus();
    }

    private void ApplyFilter()
    {
        var source = ShowSheets ? _sheetRows : _viewRows;
        IEnumerable<SheetRowViewModel> filtered = source;

        if (SelectedSetFilter != ALL_SETS && _setContents.TryGetValue(SelectedSetFilter, out var ids))
            filtered = filtered.Where(r => ids.Contains(r.Item.Id));

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            filtered = filtered.Where(r =>
                r.Number.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        Rows.Clear();
        foreach (var row in filtered) Rows.Add(row);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var selectedSheets = _sheetRows.Count(r => r.IsSelected);
        var selectedViews = _viewRows.Count(r => r.IsSelected);
        StatusText = $"{selectedSheets} sheets en {selectedViews} views geselecteerd";
    }

    private IReadOnlyList<SheetItem> SelectedItems() =>
        _sheetRows.Concat(_viewRows).Where(r => r.IsSelected).Select(r => r.Item).ToList();

    // ── Profiel & formaatinstellingen ───────────────────────────────────────

    [ObservableProperty]
    private ExportProfile _profile;

    public ObservableCollection<string> ProfileNames { get; } = [];

    [ObservableProperty]
    private string? _selectedProfileName;

    [ObservableProperty]
    private string _newProfileName = "";

    public ObservableCollection<string> DwgSetupNames { get; } = [];
    public ObservableCollection<string> DgnSetupNames { get; } = [];
    public ObservableCollection<ParamRowViewModel> XmlParameters { get; } = [];

    public IReadOnlyList<string> IfcVersions { get; } = ["IFC2x2", "IFC2x3CV2", "IFC4RV", "IFC4DTV"];

    partial void OnSelectedProfileNameChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var loaded = _profileStore.Load(value);
        if (loaded is null) return;

        Profile = loaded;
        SyncFormatFlagsFromProfile();
        SyncXmlParametersFromProfile();
    }

    [RelayCommand]
    private void SaveProfile()
    {
        var name = string.IsNullOrWhiteSpace(NewProfileName)
            ? SelectedProfileName ?? "Default"
            : NewProfileName.Trim();

        Profile.Name = name;
        Profile.Xml.SelectedParameters = XmlParameters.Where(p => p.IsSelected).Select(p => p.Name).ToList();
        _profileStore.Save(Profile);

        if (!ProfileNames.Contains(name)) ProfileNames.Add(name);
        SelectedProfileName = name;
        NewProfileName = "";
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (SelectedProfileName is null) return;
        _profileStore.Delete(SelectedProfileName);
        ProfileNames.Remove(SelectedProfileName);
        SelectedProfileName = null;
    }

    // Per-formaat enable-vlaggen (gekoppeld aan Profile.EnabledFormats)
    [ObservableProperty] private bool _pdfEnabled;
    [ObservableProperty] private bool _dwgEnabled;
    [ObservableProperty] private bool _dgnEnabled;
    [ObservableProperty] private bool _dwfEnabled;
    [ObservableProperty] private bool _nwcEnabled;
    [ObservableProperty] private bool _ifcEnabled;
    [ObservableProperty] private bool _imgEnabled;
    [ObservableProperty] private bool _xmlEnabled;

    partial void OnPdfEnabledChanged(bool value) => ToggleFormat(ExportFormat.Pdf, value);
    partial void OnDwgEnabledChanged(bool value) => ToggleFormat(ExportFormat.Dwg, value);
    partial void OnDgnEnabledChanged(bool value) => ToggleFormat(ExportFormat.Dgn, value);
    partial void OnDwfEnabledChanged(bool value) => ToggleFormat(ExportFormat.Dwf, value);
    partial void OnNwcEnabledChanged(bool value) => ToggleFormat(ExportFormat.Nwc, value);
    partial void OnIfcEnabledChanged(bool value) => ToggleFormat(ExportFormat.Ifc, value);
    partial void OnImgEnabledChanged(bool value) => ToggleFormat(ExportFormat.Img, value);
    partial void OnXmlEnabledChanged(bool value) => ToggleFormat(ExportFormat.Xml, value);

    private void ToggleFormat(ExportFormat format, bool enabled)
    {
        if (enabled && !Profile.EnabledFormats.Contains(format))
            Profile.EnabledFormats.Add(format);
        else if (!enabled)
            Profile.EnabledFormats.Remove(format);
    }

    private void SyncFormatFlagsFromProfile()
    {
        PdfEnabled = Profile.IsEnabled(ExportFormat.Pdf);
        DwgEnabled = Profile.IsEnabled(ExportFormat.Dwg);
        DgnEnabled = Profile.IsEnabled(ExportFormat.Dgn);
        DwfEnabled = Profile.IsEnabled(ExportFormat.Dwf);
        NwcEnabled = Profile.IsEnabled(ExportFormat.Nwc);
        IfcEnabled = Profile.IsEnabled(ExportFormat.Ifc);
        ImgEnabled = Profile.IsEnabled(ExportFormat.Img);
        XmlEnabled = Profile.IsEnabled(ExportFormat.Xml);
    }

    private void SyncXmlParametersFromProfile()
    {
        var selected = new HashSet<string>(Profile.Xml.SelectedParameters, StringComparer.OrdinalIgnoreCase);
        foreach (var param in XmlParameters)
            param.IsSelected = selected.Contains(param.Name);
    }

    // ── Export ──────────────────────────────────────────────────────────────

    public ObservableCollection<JobRowViewModel> Jobs { get; } = [];

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _progressText = "";

    [RelayCommand]
    private void RefreshJobs()
    {
        Profile.Xml.SelectedParameters = XmlParameters.Where(p => p.IsSelected).Select(p => p.Name).ToList();

        Jobs.Clear();
        foreach (var job in JobBuilder.Build(SelectedItems(), Profile, _gateway.DocumentTitle))
            Jobs.Add(new JobRowViewModel(job));

        ProgressText = $"{Jobs.Count} bestanden te exporteren";
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        RefreshJobs();
        if (Jobs.Count == 0)
        {
            ProgressText = "Geen sheets/views geselecteerd of geen formaat aangevinkt.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Profile.OutputFolder))
        {
            ProgressText = "Kies eerst een exportmap.";
            return;
        }

        IsExporting = true;
        ProgressValue = 0;
        _exportCts = new CancellationTokenSource();

        var progress = new Progress<ExportProgress>(p =>
        {
            ProgressValue = (p.JobIndex + 1) * 100.0 / p.Total;
            var row = Jobs.ElementAtOrDefault(p.JobIndex);
            if (row is not null)
                row.Status = p.Error is null ? "✓ Gereed" : "✗ " + p.Error;
            ProgressText = p.Error is null
                ? $"{p.JobIndex + 1}/{p.Total} — {p.FileName}"
                : $"{p.JobIndex + 1}/{p.Total} — FOUT bij {p.FileName}";
        });

        try
        {
            var jobs = Jobs.Select(j => j.Job).ToList();
            await _gateway.ExportAsync(jobs, Profile, Profile.OutputFolder, progress, _exportCts.Token);
            var failures = Jobs.Count(j => j.Status.StartsWith('✗'));
            ProgressText = failures == 0
                ? $"Klaar — {jobs.Count} bestanden geëxporteerd naar {Profile.OutputFolder}"
                : $"Klaar met {failures} fout(en) — zie statuskolom";
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Export geannuleerd.";
        }
        catch (Exception ex)
        {
            ProgressText = "Export mislukt: " + ex.Message;
        }
        finally
        {
            IsExporting = false;
            _exportCts?.Dispose();
            _exportCts = null;
        }
    }

    [RelayCommand]
    private void CancelExport() => _exportCts?.Cancel();

    /// <summary>Bindings op Profile.* verversen na een wijziging buiten de UI om (bijv. folder-dialog).</summary>
    public void RaiseProfileChanged() => OnPropertyChanged(nameof(Profile));

    // ── Initialisatie ───────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        StatusText = "Model wordt gelezen…";

        // Eén Revit-rondreis voor alles — losse calls wachten elk op een idle-moment van Revit
        var snapshot = await _gateway.GetSnapshotAsync();

        _sheetRows = snapshot.Sheets.Select(s => Track(new SheetRowViewModel(s))).ToList();
        _viewRows = snapshot.Views.Select(v => Track(new SheetRowViewModel(v))).ToList();

        SetFilters.Clear();
        SetFilters.Add(ALL_SETS);
        _setContents = new Dictionary<string, HashSet<long>>();
        foreach (var (setName, ids) in snapshot.ViewSheetSets)
        {
            SetFilters.Add(setName);
            _setContents[setName] = [.. ids];
        }

        DwgSetupNames.Clear();
        foreach (var name in snapshot.DwgSetupNames) DwgSetupNames.Add(name);
        DgnSetupNames.Clear();
        foreach (var name in snapshot.DgnSetupNames) DgnSetupNames.Add(name);

        XmlParameters.Clear();
        var paramNames = snapshot.Sheets
            .SelectMany(s => s.Parameters.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        foreach (var name in paramNames)
            XmlParameters.Add(new ParamRowViewModel(name));

        ProfileNames.Clear();
        foreach (var name in _profileStore.ListNames()) ProfileNames.Add(name);

        SyncFormatFlagsFromProfile();
        ApplyFilter();
    }

    private SheetRowViewModel Track(SheetRowViewModel row)
    {
        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SheetRowViewModel.IsSelected)) UpdateStatus();
        };
        return row;
    }
}
