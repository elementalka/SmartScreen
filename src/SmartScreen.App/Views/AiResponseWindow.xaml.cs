using System.Windows;
using SmartScreen.App.ViewModels;

namespace SmartScreen.App.Views;

public partial class AiResponseWindow : Window
{
    public AiResponseWindow(AiResponseViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.LoadCommand.Execute(null);
    }
}

