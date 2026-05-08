using Microsoft.Extensions.DependencyInjection;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.App.Pages;

public partial class AboutSectionView : ContentView
{
    public AboutSectionView()
        : this(ResolveViewModel()) { }

    public AboutSectionView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private static SettingsViewModel ResolveViewModel()
    {
        var services = IPlatformApplication.Current?.Services
            ?? throw new InvalidOperationException("Platform service provider is not available.");
        return services.GetRequiredService<SettingsViewModel>();
    }
}
