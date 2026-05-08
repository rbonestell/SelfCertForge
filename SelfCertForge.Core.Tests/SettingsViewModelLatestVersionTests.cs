using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.Core.Tests;

public sealed class SettingsViewModelLatestVersionTests
{
    [Fact]
    public void LatestPublishedVersion_HydratesFromService_AtConstruction()
    {
        var github = new FakeGithub("0.0.5");
        var vm = MakeVm(github);

        vm.LatestPublishedVersion.Should().Be("0.0.5");
        vm.HasLatestPublishedVersion.Should().BeTrue();
    }

    [Fact]
    public void LatestPublishedVersion_UpdatesWhenServiceFires()
    {
        var github = new FakeGithub(null);
        var vm = MakeVm(github);
        vm.HasLatestPublishedVersion.Should().BeFalse();

        github.Fire("0.0.7");

        vm.LatestPublishedVersion.Should().Be("0.0.7");
        vm.HasLatestPublishedVersion.Should().BeTrue();
    }

    [Fact]
    public void IsOnLatestPublishedVersion_TrueWhenInstalledMatches()
    {
        // The CurrentVersion comes from AssemblyInformationalVersion at runtime;
        // we just verify the comparison logic against whatever it resolves to.
        var vm = MakeVm(new FakeGithub(null));
        var installed = vm.CurrentVersion;

        ((FakeGithub)GetGithub(vm)).Fire(installed);

        vm.IsOnLatestPublishedVersion.Should().BeTrue();
    }

    [Fact]
    public void IsOnLatestPublishedVersion_FalseWhenDifferent()
    {
        var vm = MakeVm(new FakeGithub(null));

        ((FakeGithub)GetGithub(vm)).Fire("99.99.99");

        vm.IsOnLatestPublishedVersion.Should().BeFalse();
    }

    [Fact]
    public void IsOnLatestPublishedVersion_FalseWhenLatestUnknown()
    {
        var vm = MakeVm(new FakeGithub(null));
        vm.IsOnLatestPublishedVersion.Should().BeFalse();
    }

    [Fact]
    public void NoGithubService_VmStillConstructs_LatestStaysNull()
    {
        var vm = new SettingsViewModel(new NoopUpdate());
        vm.LatestPublishedVersion.Should().BeNull();
        vm.HasLatestPublishedVersion.Should().BeFalse();
    }

    private static SettingsViewModel MakeVm(IGithubReleaseService github) =>
        new(new NoopUpdate(), null, null, null, null, github);

    /// <summary>Reflection helper so tests don't need a public accessor on the VM for the service.</summary>
    private static IGithubReleaseService GetGithub(SettingsViewModel vm)
    {
        var field = typeof(SettingsViewModel).GetField("_githubRelease",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return (IGithubReleaseService)field!.GetValue(vm)!;
    }

    private sealed class FakeGithub : IGithubReleaseService
    {
        public FakeGithub(string? initial) { LatestPublishedVersion = initial; }
        public string? LatestPublishedVersion { get; private set; }
        public event EventHandler<string?>? Changed;
        public Task RefreshAsync(CancellationToken ct = default) => Task.CompletedTask;

        public void Fire(string? version)
        {
            LatestPublishedVersion = version;
            Changed?.Invoke(this, version);
        }
    }

    private sealed class NoopUpdate : IUpdateService
    {
        public bool IsUpdateSupported => false;
        public Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default) => Task.FromResult<UpdateInfo?>(null);
        public Task DownloadUpdateAsync(UpdateInfo update, IProgress<int>? progress = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyUpdateAndRestartAsync(UpdateInfo update) => Task.CompletedTask;
    }
}
