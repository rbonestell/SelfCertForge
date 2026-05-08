using Microsoft.Extensions.DependencyInjection;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.App.Pages;

public partial class DashboardView : ContentView
{
    public DashboardView()
        : this(ResolveViewModel()) { }

    public DashboardView(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _ = viewModel.LoadAsync();
    }

    private static DashboardViewModel ResolveViewModel()
    {
        var services = IPlatformApplication.Current?.Services
            ?? throw new InvalidOperationException("Platform service provider is not available.");
        return services.GetRequiredService<DashboardViewModel>();
    }
}
