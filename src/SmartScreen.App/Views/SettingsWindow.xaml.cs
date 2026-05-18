using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SmartScreen.App.Services;
using SmartScreen.App.ViewModels;

namespace SmartScreen.App.Views;

public partial class SettingsWindow : Window
{
    private bool _themePreviewEnabled;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) =>
        {
            _themePreviewEnabled = true;
            viewModel.LoadCommand.Execute(null);
        };
    }

    private void ThemeModeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ApplyThemePreview();

    private void AccentColorTextBox_OnTextChanged(object sender, TextChangedEventArgs e) =>
        ApplyThemePreview();

    private void ApplyThemePreview()
    {
        if (!_themePreviewEnabled)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                if (DataContext is SettingsViewModel { Settings: not null } viewModel)
                {
                    ThemeResourceService.Apply(viewModel.Settings.Theme);
                }
            }));
    }
}
