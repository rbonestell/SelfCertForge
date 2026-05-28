using SelfCertForge.Core.Presentation;

namespace SelfCertForge.Core.Abstractions;

public interface INavigationService
{
    void NavigateToCertificate(string certId);

    Task NavigateToAsync(AppRoute route, CancellationToken ct = default);
}
