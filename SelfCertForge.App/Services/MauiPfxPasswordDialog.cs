using SelfCertForge.Core.Abstractions;

namespace SelfCertForge.App.Services;

public sealed class MauiPfxPasswordDialog : IPfxPasswordDialog
{
    public async Task<(bool Confirmed, string? Password)> ShowAsync()
    {
        var page = Application.Current?.Windows?[0]?.Page;
        if (page is null) return (false, null);

        var password = await page.DisplayPromptAsync(
            title: "Export as PFX",
            message: "Enter a password to protect the bundle, or leave blank to export without protection.",
            accept: "Export",
            cancel: "Cancel",
            placeholder: "Password (optional)",
            keyboard: Keyboard.Default);

        return password is null ? (false, null) : (true, password);
    }
}
