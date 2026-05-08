using SelfCertForge.Core.Abstractions;
using CTFolderPicker = CommunityToolkit.Maui.Storage.FolderPicker;

namespace SelfCertForge.App.Services;

public sealed class MauiFolderPicker : IFolderPicker
{
    public async Task<string?> PickAsync(CancellationToken ct = default)
    {
        var initial = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var result = await CTFolderPicker.Default.PickAsync(initial, ct);
        return result.IsSuccessful ? result.Folder?.Path : null;
    }
}
