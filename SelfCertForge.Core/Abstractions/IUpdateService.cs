using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Abstractions;

public interface IUpdateService
{
    bool IsUpdateSupported { get; }
    Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default);
    Task DownloadUpdateAsync(UpdateInfo update, IProgress<int>? progress = null, CancellationToken ct = default);
    Task ApplyUpdateAndRestartAsync(UpdateInfo update);
}
