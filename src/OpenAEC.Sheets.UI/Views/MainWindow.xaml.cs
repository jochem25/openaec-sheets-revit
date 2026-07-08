using System.Windows;

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
