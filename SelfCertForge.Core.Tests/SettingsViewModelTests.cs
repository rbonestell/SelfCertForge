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
    public async Task DownloadAndInstall_ShowsLightbox_WithDownloadingThenInstalling()
    {
        var update = new UpdateInfo("2.0.0", null, null, null);
        var service = FakeUpdateService.WithUpdate(update);
        var overlay = new FakeLoadingOverlay();
        var vm = new SettingsViewModel(service, null, null, null, null, loadingOverlay: overlay);

        await vm.CheckForUpdateAsync();
        await vm.DownloadAndInstallCommand.ExecuteAsync();

        overlay.Messages.Should().Equal("Downloading Update…", "Installing Update…");
        overlay.MaxConcurrentDepth.Should().Be(2, "the install phase runs nested inside the download phase, so the overlay is re-entered in place rather than reopened");
        service.WasApplied.Should().BeTrue();
        vm.IsDownloading.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadAndInstall_WhenApplyFails_ResetsIsDownloadingAndSurfacesError()
    {
        var update = new UpdateInfo("2.0.0", null, null, null);
        var service = FakeUpdateService.WithUpdate(update);
        service.ThrowOnApply = true;
        var overlay = new FakeLoadingOverlay();
        var vm = new SettingsViewModel(service, null, null, null, null, loadingOverlay: overlay);

        await vm.CheckForUpdateAsync();
        await vm.DownloadAndInstallCommand.ExecuteAsync();

        vm.IsDownloading.Should().BeFalse();
        vm.UpdateStatusMessage.Should().Contain("failed");
        overlay.Messages.Should().Contain("Downloading Update…");
    }

    [Fact]
    public async Task DownloadAndInstall_WhenNoUpdateAvailable_IsNoOp()
    {
        var service = FakeUpdateService.WithNoUpdate();
        var overlay = new FakeLoadingOverlay();
        var vm = new SettingsViewModel(service, null, null, null, null, loadingOverlay: overlay);

        // Deliberately no CheckForUpdateAsync — AvailableUpdate stays null.
        await vm.DownloadAndInstallCommand.ExecuteAsync();

        overlay.Messages.Should().BeEmpty();
        service.WasApplied.Should().BeFalse();
        vm.IsDownloading.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadAndInstall_WhenAlreadyDownloading_SecondInvocationIsNoOp()
    {
        var gate = new TaskCompletionSource();
        var service = FakeUpdateService.WithUpdate(new UpdateInfo("2.0.0", null, null, null));
        service.HoldDownloadUntil = gate.Task;
        var vm = new SettingsViewModel(service);

        await vm.CheckForUpdateAsync();

        var first = vm.DownloadAndInstallCommand.ExecuteAsync();
        var second = vm.DownloadAndInstallCommand.ExecuteAsync(); // IsDownloading == true → guard returns

        gate.SetResult();
        await Task.WhenAll(first, second);

        service.ApplyCallCount.Should().Be(1, "the in-flight guard must prevent a concurrent second apply");
    }

    [Fact]
    public async Task DownloadAndInstallCommand_IsDisabledUntilUpdateIsAvailable()
    {
        var update = new UpdateInfo("2.0.0", null, null, null);
        var service = FakeUpdateService.WithUpdate(update);
        var vm = new SettingsViewModel(service);

        vm.DownloadAndInstallCommand.CanExecute(null).Should().BeFalse("no update is known yet");

        await vm.CheckForUpdateAsync();

        vm.DownloadAndInstallCommand.CanExecute(null).Should().BeTrue("an update is now available");
    }

    [Fact]
    public async Task Commands_AreDisabledWhileDownloading()
    {
        var gate = new TaskCompletionSource();
        var service = FakeUpdateService.WithUpdate(new UpdateInfo("2.0.0", null, null, null));
        service.HoldDownloadUntil = gate.Task;
        var vm = new SettingsViewModel(service);

        await vm.CheckForUpdateAsync();
        var downloadTask = vm.DownloadAndInstallCommand.ExecuteAsync();

        vm.DownloadAndInstallCommand.CanExecute(null).Should().BeFalse("a download is in progress");
        vm.CheckForUpdateCommand.CanExecute(null).Should().BeFalse("a download is in progress");

        gate.SetResult();
        await downloadTask;
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

        public bool ThrowOnDownload { get; set; }
        public bool ThrowOnApply { get; set; }
        public Task? HoldDownloadUntil { get; set; }
        public int ApplyCallCount { get; private set; }
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
            return HoldDownloadUntil ?? Task.CompletedTask;
        }

        public bool WasApplied => ApplyCallCount > 0;

        public Task ApplyUpdateAndRestartAsync(UpdateInfo update)
        {
            ApplyCallCount++;
            if (ThrowOnApply) throw new InvalidOperationException("apply failed");
            return Task.CompletedTask;
        }
    }
}
