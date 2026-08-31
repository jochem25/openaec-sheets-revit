using System.Windows;
using System.Windows.Controls;

using Microsoft.Win32;

using OpenAEC.Sheets.UI.ViewModels;

namespace OpenAEC.Sheets.UI.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Model kon niet gelezen worden:\n" + ex.Message,
                "OpenAEC Sheet Exporter", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnFormatToggleClick(object sender, RoutedEventArgs e)
    {
        // CheckBox consumeert de klik — selecteer zelf de bijbehorende formaat-tab
        var element = sender as DependencyObject;
        while (element is not null and not TabItem)
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);

        if (element is TabItem tab)
            tab.IsSelected = true;
    }

    private bool _bookletBoxLastFocused;

    private void OnNamingBoxGotFocus(object sender, RoutedEventArgs e) =>
        _bookletBoxLastFocused = ReferenceEquals(sender, BookletNameBox);

    private void OnInsertNamingToken(object sender, RoutedEventArgs e)
    {
        // Doel: het veld waar het laatst in getypt is; boekjesnaam alleen als dat veld actief is
        var intoBooklet = _bookletBoxLastFocused && BookletNameBox.IsEnabled;
        var box = intoBooklet ? BookletNameBox : NamingTemplateBox;
        var caret = box.CaretIndex;
        var token = _viewModel.SelectedNamingToken ?? "";
        _viewModel.InsertNamingToken(token, caret, intoBooklet);
        // Cursor achter het ingevoegde token en focus terug naar het veld
        box.Focus();
        box.CaretIndex = Math.Min(caret + token.Length + 2, box.Text.Length);
    }

    private void OnBrowseFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Kies exportmap",
            InitialDirectory = _viewModel.Profile.OutputFolder,
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.Profile.OutputFolder = dialog.FolderName;
            _viewModel.RaiseProfileChanged();
        }
    }
}
