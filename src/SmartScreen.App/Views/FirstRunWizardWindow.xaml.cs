using System.Windows;
using SmartScreen.App.ViewModels;

namespace SmartScreen.App.Views;

public partial class FirstRunWizardWindow : Window
{
    private readonly FirstRunWizardViewModel _viewModel;

    public FirstRunWizardWindow(FirstRunWizardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.CloseRequested += CloseWizard;
        Loaded += (_, _) => viewModel.LoadCommand.Execute(null);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CloseRequested -= CloseWizard;
        base.OnClosed(e);
    }

    private void CloseWizard()
    {
        DialogResult = true;
    }
}
