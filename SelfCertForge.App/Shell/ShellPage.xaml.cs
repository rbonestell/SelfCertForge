namespace SelfCertForge.App.Shell;

public partial class ShellPage : ContentPage
{
    public ShellPage(ShellViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
