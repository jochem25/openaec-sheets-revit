using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using OpenAEC.Sheets.Core.Models;
using OpenAEC.Sheets.Core.Naming;
using OpenAEC.Sheets.Core.Services;

namespace OpenAEC.Sheets.UI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IRevitGateway _gateway;
    private readonly ProfileStore _profileStore;

    private List<SheetRowViewModel> _sheetRows = [];
    private List<SheetRowViewModel> _viewRows = [];
    private Dictionary<string, HashSet<long>> _setContents = new();
    private string _projectName = "";
    private string _projectNumber = "";
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

    partial void OnSelectedSetFilterChanged(string value)
    {
        SelectSetContents(value);
        ApplyFilter();
    }

    /// <summary>Bij het kiezen van een set: precies de inhoud van die set selecteren.</summary>
    private void SelectSetContents(string setName)
    {
        if (setName == ALL_SETS || !_setContents.TryGetValue(setName, out var ids)) return;
        foreach (var row in _sheetRows.Concat(_viewRows))
            row.IsSelected = ids.Contains(row.Item.Id);
    }

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
        StatusText = $"{selectedSheets} sheets en {selectedViews} views geselecteerd" + BookletSummary();
        UpdatePdfCombineActive();
        UpdateNamingPreview();
    }

    /// <summary>
    /// Bij "combineer per parameterwaarde" + splitsen: aantal boekjes, totaal aantal bladpagina's
    /// over alle boekjes (incl. dubbeltellingen) en het aantal unieke sheets.
    /// </summary>
    private string BookletSummary()
    {
        if (!PdfEnabled || !PdfCombineByParameter || !PdfSplitGroupValues) return "";

        var items = SelectedItems();
        if (items.Count == 0) return "";

        var warnings = new List<string>();
        var jobs = JobBuilder.GroupedJobs(items, ExportFormat.Pdf, Profile.Pdf, CurrentDocumentTokens(), warnings);
        var pages = jobs.Sum(j => j.ElementIds.Count);
        var unique = jobs.SelectMany(j => j.ElementIds).Distinct().Count();
        var text = $" — {jobs.Count} boekjes, {pages} bladpagina's ({unique} unieke sheets)";
        if (warnings.Count > 0)
            text += " — let op: " + string.Join("; ", warnings);
        return text;
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
    public ObservableCollection<string> SheetParameterNames { get; } = [];
    public ObservableCollection<string> PhaseNames { get; } = [];
    public ObservableCollection<string> CategoryMappingNames { get; } = [];

    public const string DEFAULT_CHOICE = "(standaard)";

    public IReadOnlyList<string> IfcVersions { get; } = ["IFC2x2", "IFC2x3CV2", "IFC4RV", "IFC4DTV"];

    public IReadOnlyList<string> PdfPaperFormats { get; } =
    [
        "Default",
        "ISO_A0", "ISO_A1", "ISO_A2", "ISO_A3", "ISO_A4",
        "ISO_B1", "ISO_B2", "ISO_B3", "ISO_B4",
        "ANSI_A", "ANSI_B", "ANSI_C", "ANSI_D", "ANSI_E",
        "ARCH_A", "ARCH_B", "ARCH_C", "ARCH_D", "ARCH_E",
    ];

    public IReadOnlyList<string> Orientations { get; } = ["Auto", "Portrait", "Landscape"];
    public IReadOnlyList<int> PdfQualityDpis { get; } = [72, 144, 300, 600, 1200];
    public IReadOnlyList<string> DwgVersions { get; } = ["Default", "R2018", "R2013", "R2010", "R2007"];
    public IReadOnlyList<string> DwfQualities { get; } = ["Default", "Low", "Medium", "High"];
    public IReadOnlyList<double> TessellationLevels { get; } = [0.25, 0.5, 0.75, 1.0];

    partial void OnSelectedProfileNameChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var loaded = _profileStore.Load(value);
        if (loaded is null) return;

        Profile = loaded;
        SyncFormatFlagsFromProfile();
        SyncXmlParametersFromProfile();
        SyncPdfFileModeFromProfile();
        SyncNamingFromProfile();
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

    partial void OnPdfEnabledChanged(bool value)
    {
        ToggleFormat(ExportFormat.Pdf, value);
        UpdateStatus();
    }
    partial void OnDwgEnabledChanged(bool value) => ToggleFormat(ExportFormat.Dwg, value);
    partial void OnDgnEnabledChanged(bool value) => ToggleFormat(ExportFormat.Dgn, value);
    partial void OnDwfEnabledChanged(bool value) => ToggleFormat(ExportFormat.Dwf, value);
    partial void OnNwcEnabledChanged(bool value) => ToggleFormat(ExportFormat.Nwc, value);
    partial void OnIfcEnabledChanged(bool value) => ToggleFormat(ExportFormat.Ifc, value);
    partial void OnImgEnabledChanged(bool value) => ToggleFormat(ExportFormat.Img, value);
    partial void OnXmlEnabledChanged(bool value) => ToggleFormat(ExportFormat.Xml, value);

    // PDF bestandsmodus (radio buttons ↔ Profile.Pdf.FileMode)
    [ObservableProperty] private bool _pdfSeparateFiles = true;
    [ObservableProperty] private bool _pdfCombineAll;
    [ObservableProperty] private bool _pdfCombineByParameter;

    partial void OnPdfSeparateFilesChanged(bool value)
    {
        if (value) Profile.Pdf.FileMode = PdfFileMode.Separate;
        UpdateStatus();
    }

    partial void OnPdfCombineAllChanged(bool value)
    {
        if (value) Profile.Pdf.FileMode = PdfFileMode.CombineAll;
        UpdateStatus();
    }

    // Boekjesnaam / prefix (↔ Profile.Pdf.CombinedFileName) — via VM zodat het voorbeeld live meeloopt
    [ObservableProperty] private string _pdfCombinedFileName = "";

    /// <summary>True als PDF aan staat én gecombineerd wordt (alles of per parameterwaarde): dan geldt de boekjesnaam.</summary>
    [ObservableProperty] private bool _pdfCombineActive;

    private void UpdatePdfCombineActive() =>
        PdfCombineActive = PdfEnabled && Profile.Pdf.FileMode != PdfFileMode.Separate;

    partial void OnPdfCombinedFileNameChanged(string value)
    {
        Profile.Pdf.CombinedFileName = value ?? "";
        UpdateNamingPreview();
    }

    partial void OnPdfCombineByParameterChanged(bool value)
    {
        if (value) Profile.Pdf.FileMode = PdfFileMode.CombineByParameter;
        UpdateStatus();
    }

    // Waarde splitsen: één blad in meerdere boekjes (↔ Profile.Pdf.SplitGroupValues / GroupValueSeparators)
    [ObservableProperty] private bool _pdfSplitGroupValues;
    [ObservableProperty] private string _pdfGroupValueSeparators = PdfSettings.DefaultGroupValueSeparators;

    partial void OnPdfSplitGroupValuesChanged(bool value)
    {
        Profile.Pdf.SplitGroupValues = value;
        UpdateStatus();
    }

    partial void OnPdfGroupValueSeparatorsChanged(string value)
    {
        Profile.Pdf.GroupValueSeparators = value ?? "";
        UpdateStatus();
    }

    // Bladen één keer renderen en boekjes samenstellen (↔ Profile.Pdf.AssembleBooklets)
    [ObservableProperty] private bool _pdfAssembleBooklets = true;

    partial void OnPdfAssembleBookletsChanged(bool value)
    {
        Profile.Pdf.AssembleBooklets = value;
        UpdateStatus();
    }

    // Wildcards (* en ?) in gesplitste tokens expanderen tegen de concrete boekjesnamen (↔ Profile.Pdf.ExpandWildcards)
    [ObservableProperty] private bool _pdfExpandWildcards = true;

    partial void OnPdfExpandWildcardsChanged(bool value)
    {
        Profile.Pdf.ExpandWildcards = value;
        UpdateStatus();
    }

    private void SyncPdfFileModeFromProfile()
    {
        PdfSeparateFiles = Profile.Pdf.FileMode == PdfFileMode.Separate;
        PdfCombineAll = Profile.Pdf.FileMode == PdfFileMode.CombineAll;
        PdfCombineByParameter = Profile.Pdf.FileMode == PdfFileMode.CombineByParameter;
        PdfSplitGroupValues = Profile.Pdf.SplitGroupValues;
        PdfGroupValueSeparators = Profile.Pdf.GroupValueSeparators;
        PdfExpandWildcards = Profile.Pdf.ExpandWildcards;
        PdfAssembleBooklets = Profile.Pdf.AssembleBooklets;
        PdfCombinedFileName = Profile.Pdf.CombinedFileName;
    }

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

    // ── Naamgeving (template met vaste tekst + {tokens}) ────────────────────

    /// <summary>Spiegel van Profile.NamingTemplate zodat het voorbeeld live meeloopt tijdens typen.</summary>
    [ObservableProperty] private string _namingTemplate = "";

    /// <summary>Opgeloste bestandsnaam voor het eerste geselecteerde (of eerste) blad.</summary>
    [ObservableProperty] private string _namingPreview = "";

    /// <summary>Alle tokens voor de kiezer: document-tokens eerst, dan sheet-/titleblock-parameters.</summary>
    public ObservableCollection<string> NamingTokens { get; } = [];

    [ObservableProperty] private string? _selectedNamingToken;

    partial void OnNamingTemplateChanged(string value)
    {
        Profile.NamingTemplate = value ?? "";
        UpdateNamingPreview();
    }

    private void SyncNamingFromProfile()
    {
        NamingTemplate = Profile.NamingTemplate;
        UpdateNamingPreview();
    }

    private IReadOnlyDictionary<string, string> CurrentDocumentTokens()
    {
        var setName = SelectedSetFilter == ALL_SETS ? null : SelectedSetFilter;
        return JobBuilder.DocumentTokens(_gateway.DocumentTitle, _projectName, _projectNumber, setName);
    }

    private void UpdateNamingPreview()
    {
        var sample = _sheetRows.FirstOrDefault(r => r.IsSelected) ?? _sheetRows.FirstOrDefault();
        if (sample is null)
        {
            NamingPreview = "";
            return;
        }
        var docTokens = CurrentDocumentTokens();
        var name = NamingEngine.Apply(Profile.NamingTemplate, sample.Item.Parameters, docTokens);
        var text = $"Voorbeeld ({sample.Number}): {NamingEngine.Sanitize(name)}";

        // Gecombineerde PDF's volgen niet de template maar 'Bestandsnaam / prefix' op de PDF-tab —
        // laat dat hier zien, anders lijkt de template "niets te doen".
        if (PdfEnabled && Profile.Pdf.FileMode != PdfFileMode.Separate)
        {
            var items = SelectedItems();
            if (items.Count == 0) items = [sample.Item];
            var setName = SelectedSetFilter == ALL_SETS ? null : SelectedSetFilter;
            var booklet = Profile.Pdf.FileMode == PdfFileMode.CombineAll
                ? NamingEngine.Sanitize(JobBuilder.BookletName(
                    Profile.Pdf.CombinedFileName, _gateway.DocumentTitle, _projectName, setName, items[0], docTokens))
                : JobBuilder.GroupedJobs(items, ExportFormat.Pdf, Profile.Pdf, docTokens).FirstOrDefault()?.FileName ?? "";
            text += $"   ·   Boekje: {booklet}";
        }

        NamingPreview = text;
    }

    /// <summary>
    /// Token invoegen in de naamtemplate of (<paramref name="intoBooklet"/>) in de boekjesnaam;
    /// de view geeft de caret-positie door, anders achteraan.
    /// </summary>
    public void InsertNamingToken(string? token, int caretIndex = -1, bool intoBooklet = false)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        var insert = "{" + token + "}";
        var current = (intoBooklet ? PdfCombinedFileName : NamingTemplate) ?? "";
        var at = caretIndex < 0 || caretIndex > current.Length ? current.Length : caretIndex;
        var result = current.Insert(at, insert);
        if (intoBooklet) PdfCombinedFileName = result;
        else NamingTemplate = result;
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
        var setName = SelectedSetFilter == ALL_SETS ? null : SelectedSetFilter;
        foreach (var job in JobBuilder.Build(SelectedItems(), Profile, _gateway.DocumentTitle, _projectName, setName, _projectNumber))
            Jobs.Add(new JobRowViewModel(job));

        var tempPages = Jobs.Count(j => j.Job.Kind == ExportJobKind.TempPage);
        ProgressText = tempPages == 0
            ? $"{Jobs.Count} bestanden te exporteren"
            : $"{Jobs.Count - tempPages} bestanden te exporteren ({tempPages} bladen worden 1× gerenderd en tot boekjes samengesteld)";
        UpdateStatus(); // groepeer-parameter is direct aan Profile.Pdf gebonden → hier de boekjes-telling verversen
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
            var files = jobs.Count(j => j.Kind != ExportJobKind.TempPage);
            ProgressText = failures == 0
                ? $"Klaar — {files} bestanden geëxporteerd naar {Profile.OutputFolder}"
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

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _loadingText = "Model wordt gelezen…";

    public async Task InitializeAsync()
    {
        IsLoading = true;
        LoadingText = "Model wordt gelezen…";
        StatusText = "Model wordt gelezen…";
        try
        {
            await LoadSnapshotAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadSnapshotAsync()
    {
        // Eén Revit-rondreis voor alles — losse calls wachten elk op een idle-moment van Revit
        var progress = new Progress<string>(text => LoadingText = text);
        var snapshot = await _gateway.GetSnapshotAsync(progress);

        _sheetRows = snapshot.Sheets.Select(s => Track(new SheetRowViewModel(s))).ToList();
        _viewRows = snapshot.Views.Select(v => Track(new SheetRowViewModel(v))).ToList();
        _projectName = snapshot.ProjectName;
        _projectNumber = snapshot.ProjectNumber;

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
        SheetParameterNames.Clear();
        var paramNames = snapshot.Sheets
            .SelectMany(s => s.Parameters.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        foreach (var name in paramNames)
        {
            XmlParameters.Add(new ParamRowViewModel(name));
            SheetParameterNames.Add(name);
        }

        NamingTokens.Clear();
        foreach (var token in NamingEngine.DocumentTokens) NamingTokens.Add(token);
        NamingTokens.Add(NamingEngine.TokenGroup);
        foreach (var name in SheetParameterNames) NamingTokens.Add(name);
        SelectedNamingToken = NamingTokens.FirstOrDefault();

        PhaseNames.Clear();
        PhaseNames.Add(DEFAULT_CHOICE);
        foreach (var name in snapshot.PhaseNames) PhaseNames.Add(name);

        CategoryMappingNames.Clear();
        CategoryMappingNames.Add(DEFAULT_CHOICE);
        foreach (var name in snapshot.CategoryMappingNames) CategoryMappingNames.Add(name);

        SyncPdfFileModeFromProfile();
        SyncNamingFromProfile();

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
