using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.App.Services;

public sealed class MauiCsrFilePicker : ICsrFilePicker
{
    public async Task<CsrFilePickResult?> PickCsrFileAsync(CancellationToken ct = default)
    {
        var path = await FilePickerHelper.PickFileAsync(new[] { "csr", "pem", "req", "txt" });
        if (string.IsNullOrEmpty(path)) return null;

        var contents = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        return new CsrFilePickResult(path, contents);
    }
}
