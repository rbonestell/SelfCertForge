using Microsoft.Extensions.DependencyInjection;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.App.Pages;

public partial class AuthoritiesView : ContentView
{
    public AuthoritiesView()
        : this(ResolveViewModel()) { }

    public AuthoritiesView(AuthoritiesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _ = viewModel.LoadAsync();
    }

    private static AuthoritiesViewModel ResolveViewModel()
    {
        var services = IPlatformApplication.Current?.Services
            ?? throw new InvalidOperationException("Platform service provider is not available.");
        return services.GetRequiredService<AuthoritiesViewModel>();
    }
}
