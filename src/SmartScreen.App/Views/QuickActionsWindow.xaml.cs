using System.Windows;
using SmartScreen.App.ViewModels;

namespace SmartScreen.App.Views;

public partial class QuickActionsWindow : Window
{
    private readonly QuickActionsViewModel _viewModel;

    public QuickActionsWindow(QuickActionsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.CloseRequested += Close;
        Loaded += (_, _) => viewModel.LoadCommand.Execute(null);
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CloseRequested -= Close;
        base.OnClosed(e);
    }
}
