using Microsoft.Extensions.DependencyInjection;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.App.Dialogs;

public sealed class CreateFromCsrDialogHost : ICreateFromCsrDialog
{
    private readonly IServiceProvider _services;

    public CreateFromCsrDialogHost(IServiceProvider services) => _services = services;

    public async Task<StoredCertificate?> ShowAsync(
        string signingAuthorityId,
        string signingAuthorityName,
        CsrSummary csrSummary,
        string sourceCsrFilename,
        CancellationToken ct = default)
    {
        try
        {
            var page = Application.Current?.Windows?[0]?.Page
                ?? throw new InvalidOperationException("No active page for modal navigation.");
            var dialog = _services.GetRequiredService<CreateFromCsrDialog>();
            dialog.PrepareForOpen(signingAuthorityId, signingAuthorityName, csrSummary, sourceCsrFilename);

            var tcs = new TaskCompletionSource<StoredCertificate?>();
            dialog.ViewModel.Created += (_, cert) => tcs.TrySetResult(cert);
            dialog.ViewModel.CancelRequested += (_, _) => tcs.TrySetResult(null);

            await page.Navigation.PushModalAsync(dialog, animated: false).ConfigureAwait(true);
            return await tcs.Task.ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            App.Report("CreateFromCsrDialogHost.ShowAsync", ex);
            throw;
        }
    }
}
