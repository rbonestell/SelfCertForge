using SelfCertForge.Core.Abstractions;

namespace SelfCertForge.App.Services;

public sealed class MauiConfirmationDialog : IConfirmationDialog
{
    public async Task<bool> ShowAsync(string title, string message, string confirmLabel = "Confirm", string cancelLabel = "Cancel")
    {
        var page = Application.Current?.Windows?[0]?.Page;
        if (page is null) return false;
        if (string.IsNullOrEmpty(cancelLabel))
        {
            await page.DisplayAlertAsync(title, message, confirmLabel);
            return true;
        }
        return await page.DisplayAlertAsync(title, message, confirmLabel, cancelLabel);
    }
}
