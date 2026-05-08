using Microsoft.Extensions.DependencyInjection;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.App.Pages;

public partial class CertificatesView : ContentView
{
    public CertificatesView()
        : this(ResolveViewModel()) { }

    public CertificatesView(CertificatesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _ = viewModel.LoadAsync();
    }

    private static CertificatesViewModel ResolveViewModel()
    {
        var services = IPlatformApplication.Current?.Services
            ?? throw new InvalidOperationException("Platform service provider is not available.");
        return services.GetRequiredService<CertificatesViewModel>();
    }
}
