using Microsoft.Extensions.DependencyInjection;
using SelfCertForge.Core.Abstractions;

namespace SelfCertForge.App.Dialogs;

public sealed class CreateRootDialogHost : ICreateRootDialog
{
    private readonly IServiceProvider _services;

    public CreateRootDialogHost(IServiceProvider services) => _services = services;

    public async Task ShowAsync()
    {
        try
        {
            var page = Application.Current?.Windows?[0]?.Page
                ?? throw new InvalidOperationException("No active page for modal navigation.");
            var dialog = _services.GetRequiredService<CreateRootDialog>();
            dialog.PrepareForOpen();
            await page.Navigation.PushModalAsync(dialog, animated: false).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            App.Report("CreateRootDialogHost.ShowAsync", ex);
            throw;
        }
    }
}
