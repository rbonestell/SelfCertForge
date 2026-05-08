using SelfCertForge.App.Shell;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.App.Navigation;

public sealed class NavigationService : INavigationService
{
    private readonly ShellViewModel _shell;
    private readonly CertificatesViewModel _certificates;

    public NavigationService(ShellViewModel shell, CertificatesViewModel certificates)
    {
        _shell = shell;
        _certificates = certificates;
    }

    public void NavigateToCertificate(string certId)
    {
        _shell.CurrentRoute = AppRoute.Certificates;
        _certificates.SelectById(certId);
    }
}
