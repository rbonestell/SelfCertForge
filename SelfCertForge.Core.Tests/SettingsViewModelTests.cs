using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.Core.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task CheckForUpdate_WhenUpdateAvailable_SetsIsUpdateAvailableTrue()
    {
        var update = new UpdateInfo("2.0.0", "Bug fixes.", null, null);
        var service = FakeUpdateService.WithUpdate(update);
        var vm = new SettingsViewModel(service);

        await vm.CheckForUpdateAsync();

        vm.IsUpdateAvailable.Should().BeTrue();
        vm.AvailableUpdate.Should().Be(update);
        vm.UpdateStatusMessage.Should().Contain("2.0.0");
        vm.HasUpdateStatusMessage.Should().BeTrue();
    }

    [Fact]
    public async Task CheckForUpdate_WhenNoUpdate_SetsIsUpdateAvailableFalse()
    {
        var service = FakeUpdateService.WithNoUpdate();
        var vm = new SettingsViewModel(service);

        await vm.CheckForUpdateAsync();

        vm.IsUpdateAvailable.Should().BeFalse();
        vm.AvailableUpdate.Should().BeNull();
        vm.UpdateStatusMessage.Should().Contain("latest");
    }

    [Fact]
    public async Task CheckForUpdate_WhenServiceThrows_SetsErrorMessage()
    {
        var service = FakeUpdateService.WithError(new InvalidOperationException("network error"));
        var vm = new SettingsViewModel(service);

        await vm.CheckForUpdateAsync();

        vm.IsUpdateAvailable.Should().BeFalse();
        vm.UpdateStatusMessage.Should().Contain("failed");
        vm.IsCheckingForUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdate_SetsIsCheckingDuringCheck()
    {
        var tcs = new TaskCompletionSource<UpdateInfo?>();
        var service = FakeUpdateService.WithAsyncTask(tcs.Task);
        var vm = new SettingsViewModel(service);

        var checkTask = vm.CheckForUpdateAsync();
        vm.IsCheckingForUpdate.Should().BeTrue();

        tcs.SetResult(null);
        await checkTask;

        vm.IsCheckingForUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadAndInstall_WhenDownloadFails_ResetsIsDownloading()
    {
        var update = new UpdateInfo("2.0.0", null, null, null);
        var service = FakeUpdateService.WithUpdate(update);
        service.ThrowOnDownload = true;
        var vm = new SettingsViewModel(service);

        await vm.CheckForUpdateAsync();
        await vm.DownloadAndInstallCommand.ExecuteAsync();

        vm.IsDownloading.Should().BeFalse();
        vm.UpdateStatusMessage.Should().Contain("failed");
    }

    [Fact]
    public void DownloadProgressNormalized_ReflectsDownloadProgress()
    {
        var service = FakeUpdateService.WithNoUpdate();
        var vm = new SettingsViewModel(service);

        vm.DownloadProgressNormalized.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void IsUpdateSupported_WhenServiceUnsupported_CommandDoesNotCrash()
    {
        var service = FakeUpdateService.Unsupported();
        var vm = new SettingsViewModel(service);

        vm.CheckForUpdateCommand.CanExecute(null).Should().BeTrue();
    }

    // -- Fakes ----------------------------------------------------------------

    private sealed class FakeUpdateService : IUpdateService
    {
        private readonly UpdateInfo? _update;
        private readonly Exception? _ex;
        private readonly Task<UpdateInfo?>? _asyncTask;

        public int DownloadProgressToReport { get; set; }
        public bool ThrowOnDownload { get; set; }
        public bool IsUpdateSupported { get; }

        public static FakeUpdateService WithUpdate(UpdateInfo update) => new(update: update);
        public static FakeUpdateService WithNoUpdate() => new(update: null);
        public static FakeUpdateService WithError(Exception ex) => new(ex: ex);
        public static FakeUpdateService WithAsyncTask(Task<UpdateInfo?> task) => new(asyncTask: task);
        public static FakeUpdateService Unsupported() => new(supported: false);

        private FakeUpdateService(UpdateInfo? update = null, Exception? ex = null, Task<UpdateInfo?>? asyncTask = null, bool supported = true)
        {
            _update = update;
            _ex = ex;
            _asyncTask = asyncTask;
            IsUpdateSupported = supported;
        }

        public Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
        {
            if (_asyncTask is not null) return _asyncTask;
            if (_ex is not null) throw _ex;
            return Task.FromResult(_update);
        }

        public Task DownloadUpdateAsync(UpdateInfo update, IProgress<int>? progress = null, CancellationToken ct = default)
        {
            if (ThrowOnDownload) throw new InvalidOperationException("download failed");
            progress?.Report(DownloadProgressToReport);
            return Task.CompletedTask;
        }

        public Task ApplyUpdateAndRestartAsync(UpdateInfo update) => Task.CompletedTask;
    }
}
