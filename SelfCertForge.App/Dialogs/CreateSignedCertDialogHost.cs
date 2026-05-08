using Microsoft.Extensions.DependencyInjection;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.App.Dialogs;

public sealed class CreateSignedCertDialogHost : ICreateSignedCertDialog
{
    private readonly IServiceProvider _services;

    public CreateSignedCertDialogHost(IServiceProvider services) => _services = services;

    public async Task<StoredCertificate?> ShowAsync(string issuerId, string issuerName)
    {
        try
        {
            var page = Application.Current?.Windows?[0]?.Page
                ?? throw new InvalidOperationException("No active page for modal navigation.");
            var dialog = _services.GetRequiredService<CreateSignedCertDialog>();
            dialog.PrepareForOpen(issuerId, issuerName);

            var tcs = new TaskCompletionSource<StoredCertificate?>();
            dialog.ViewModel.Created += (_, cert) => tcs.TrySetResult(cert);
            dialog.ViewModel.CancelRequested += (_, _) => tcs.TrySetResult(null);

            await page.Navigation.PushModalAsync(dialog, animated: false).ConfigureAwait(true);
            return await tcs.Task.ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            App.Report("CreateSignedCertDialogHost.ShowAsync", ex);
            throw;
        }
    }
}
