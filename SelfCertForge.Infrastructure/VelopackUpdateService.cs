using System.Runtime.InteropServices;
using SelfCertForge.Core.Abstractions;
using CoreUpdateInfo = SelfCertForge.Core.Models.UpdateInfo;
using VeloUpdateInfo = Velopack.UpdateInfo;

namespace SelfCertForge.Infrastructure;

public sealed class VelopackUpdateService : IUpdateService
{
    private const string RepoUrl = "https://github.com/rbonestell/SelfCertForge";

    private readonly Velopack.UpdateManager? _manager;
    private VeloUpdateInfo? _pendingUpdate;

    public VelopackUpdateService()
    {
        try
        {
            // On Mac Catalyst HttpClientHandler is backed by NSUrlSessionHandler, which throws when
            // MaxAutomaticRedirections is lowered. Velopack's default downloader sets it to 10, so
            // every update request would fail before it is sent.
            var downloader = OperatingSystem.IsMacCatalyst() ? new CatalystFileDownloader() : null;
            _manager = new Velopack.UpdateManager(
                new Velopack.Sources.GithubSource(RepoUrl, null, false, downloader),
                new Velopack.UpdateOptions { ExplicitChannel = GetPlatformChannel() });
        }
        catch
        {
            // VelopackLocator not initialized — not running under a managed install.
        }
    }

    private static string GetPlatformChannel()
    {
        // macOS ships a single universal (x64+arm64 lipo) .app under the "osx"
        // channel; Windows ships per-arch installers since PE has no equivalent
        // of fat binaries.
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
            return "osx";

        var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        return $"win-{arch}";
    }

    public bool IsUpdateSupported => _manager?.IsInstalled ?? false;

    public async Task<CoreUpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        if (!IsUpdateSupported)
            return null;

        _pendingUpdate = await _manager.CheckForUpdatesAsync();
        if (_pendingUpdate is null)
            return null;

        var asset = _pendingUpdate.TargetFullRelease;
        return new CoreUpdateInfo(
            Version: asset.Version?.ToString() ?? string.Empty,
            ReleaseNotes: asset.NotesMarkdown,
            DownloadUrl: null,
            PublishedAt: null);
    }

    public async Task DownloadUpdateAsync(CoreUpdateInfo update, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        if (_pendingUpdate is null)
            throw new InvalidOperationException("No pending update. Call CheckForUpdateAsync first.");

        await _manager.DownloadUpdatesAsync(_pendingUpdate, p => progress?.Report(p), ct);
    }

    public Task ApplyUpdateAndRestartAsync(CoreUpdateInfo update)
    {
        if (_pendingUpdate is null)
            throw new InvalidOperationException("No pending update. Call CheckForUpdateAsync first.");

        _manager.ApplyUpdatesAndRestart(_pendingUpdate);
        return Task.CompletedTask;
    }

    // Same as Velopack's default handler minus MaxAutomaticRedirections (unsupported by NSUrlSessionHandler).
    private sealed class CatalystFileDownloader : Velopack.Sources.HttpClientFileDownloader
    {
        protected override HttpClientHandler CreateHttpClientHandler() => new() { AllowAutoRedirect = true };
    }
}
