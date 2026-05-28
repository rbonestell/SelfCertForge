using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.App.Services;

public sealed class MauiCsrFilePicker : ICsrFilePicker
{
    // CSRs are tiny PEM blobs; 1 MiB is far above any legitimate request and
    // guards against accidentally loading huge files into memory.
    private const long MaxCsrBytes = 1 * 1024 * 1024;

    public async Task<CsrFilePickResult?> PickCsrFileAsync(CancellationToken ct = default)
    {
        var path = await FilePickerHelper.PickFileAsync(new[] { "csr", "pem", "req", "txt" });
        if (string.IsNullOrEmpty(path)) return null;

        try
        {
            var info = new FileInfo(path);
            if (info.Exists && info.Length > MaxCsrBytes)
            {
                await ShowAlertAsync(
                    "File too large",
                    $"The selected file is {info.Length:N0} bytes. Certificate signing requests must be {MaxCsrBytes:N0} bytes or smaller.");
                return null;
            }

            var contents = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return new CsrFilePickResult(path, contents);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await ShowAlertAsync(
                "Could not read file",
                $"Could not read the selected file: {ex.Message}");
            return null;
        }
    }

    private static Task ShowAlertAsync(string title, string message) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Microsoft.Maui.Controls.Application.Current?.Windows
                .FirstOrDefault()?.Page;
            if (page is not null)
                await page.DisplayAlertAsync(title, message, "OK");
        });
}
