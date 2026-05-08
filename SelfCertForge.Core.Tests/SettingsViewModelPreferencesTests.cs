using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.Core.Tests;

public sealed class SettingsViewModelPreferencesTests
{
    [Fact]
    public void Construction_HydratesFromStore()
    {
        var prefs = new UserPreferences
        {
            RootValidityDays = 1234,
            SignedValidityDays = 90,
            KeyBits = 3072,
            HashAlgorithm = HashAlgorithmKind.Sha512,
            DefaultOrganization = "Org",
            ActivityRetention = ActivityRetention.OneThousand,
        };
        var store = new InMemoryPrefs(prefs);
        var vm = MakeVm(store);

        vm.RootValidityDays.Should().Be(1234);
        vm.SignedValidityDays.Should().Be(90);
        vm.KeyBits.Should().Be(3072);
        vm.HashAlgorithm.Should().Be(HashAlgorithmKind.Sha512);
        vm.DefaultOrganization.Should().Be("Org");
        vm.ActivityRetention.Should().Be(ActivityRetention.OneThousand);
        vm.IsKeyBits3072.Should().BeTrue();
        vm.IsHashSha512.Should().BeTrue();
        vm.IsRetention1000.Should().BeTrue();
    }

    [Fact]
    public void Construction_SuppressesInitialSave()
    {
        var store = new InMemoryPrefs(UserPreferences.Default with { KeyBits = 4096 });
        var vm = MakeVm(store);
        store.SaveCount.Should().Be(0);
        vm.KeyBits.Should().Be(4096);
    }

    [Fact]
    public async Task SaveNowAsync_PersistsAllFields()
    {
        var store = new InMemoryPrefs(UserPreferences.Default);
        var vm = MakeVm(store);

        vm.DefaultOrganization = "  Acme  ";
        vm.KeyBits = 4096;
        vm.HashAlgorithm = HashAlgorithmKind.Sha384;
        vm.ActivityRetention = ActivityRetention.Unlimited;
        vm.RootValidityDays = 365;
        vm.SignedValidityDays = 90;

        await vm.SaveNowAsync();

        store.LastSaved.Should().NotBeNull();
        store.LastSaved!.DefaultOrganization.Should().Be("Acme"); // trimmed + non-blank
        store.LastSaved.KeyBits.Should().Be(4096);
        store.LastSaved.HashAlgorithm.Should().Be(HashAlgorithmKind.Sha384);
        store.LastSaved.ActivityRetention.Should().Be(ActivityRetention.Unlimited);
        store.LastSaved.RootValidityDays.Should().Be(365);
        store.LastSaved.SignedValidityDays.Should().Be(90);
    }

    [Fact]
    public async Task SaveNowAsync_BlankFields_StoredAsNull()
    {
        var store = new InMemoryPrefs(UserPreferences.Default with { DefaultEmail = "x@y.test" });
        var vm = MakeVm(store);

        vm.DefaultEmail = "   ";
        await vm.SaveNowAsync();

        store.LastSaved!.DefaultEmail.Should().BeNull();
    }

    [Fact]
    public async Task ClearActivityLog_WhenConfirmed_CallsClear()
    {
        var log = new InMemoryLog();
        await log.AppendAsync(new ActivityEntry("a", DateTimeOffset.UtcNow, "k", "m", null));
        var confirm = new AlwaysConfirm();
        var vm = new SettingsViewModel(new NoopUpdate(), null, log, null, confirm);

        await vm.ClearActivityLogCommand.ExecuteAsync();

        log.Recent.Should().BeEmpty();
        confirm.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ClearActivityLog_WhenDeclined_DoesNothing()
    {
        var log = new InMemoryLog();
        await log.AppendAsync(new ActivityEntry("a", DateTimeOffset.UtcNow, "k", "m", null));
        var confirm = new AlwaysDeny();
        var vm = new SettingsViewModel(new NoopUpdate(), null, log, null, confirm);

        await vm.ClearActivityLogCommand.ExecuteAsync();

        log.Recent.Should().HaveCount(1);
    }

    [Fact]
    public async Task RevealDataFolder_InvokesService()
    {
        var folder = new RecordingFolder();
        var vm = new SettingsViewModel(new NoopUpdate(), null, null, folder, null);

        await vm.RevealDataFolderCommand.ExecuteAsync();

        folder.Reveals.Should().Be(1);
        vm.DataFolderPath.Should().Be("/test/path");
        vm.RevealLabel.Should().Be("Reveal");
        vm.HasDataFolder.Should().BeTrue();
    }

    [Fact]
    public void NoServices_StillConstructs_WithoutCrashing()
    {
        // Ensures backwards-compat ctor path still works.
        var vm = new SettingsViewModel(new NoopUpdate());
        vm.HasDataFolder.Should().BeFalse();
        vm.CanClearLog.Should().BeFalse();
        vm.RootValidityDays.Should().Be(9125);
    }

    [Fact]
    public void RootValidityDays_SetToZero_FlagsErrorAndDoesNotSchedulePersist()
    {
        var store = new InMemoryPrefs(UserPreferences.Default);
        var vm = MakeVm(store);
        vm.RootValidityDays = 0;
        vm.RootValidityHasError.Should().BeTrue();
        // Auto-save shouldn't fire while invalid; SaveCount stays at 0.
        store.SaveCount.Should().Be(0);
    }

    [Fact]
    public void SignedValidityDays_SetToNegative_FlagsError()
    {
        var store = new InMemoryPrefs(UserPreferences.Default);
        var vm = MakeVm(store);
        vm.SignedValidityDays = -1;
        vm.SignedValidityHasError.Should().BeTrue();
        store.SaveCount.Should().Be(0);
    }

    [Fact]
    public void RootValidityDays_RecoversFromError_ClearsFlag()
    {
        var store = new InMemoryPrefs(UserPreferences.Default);
        var vm = MakeVm(store);
        vm.RootValidityDays = 0;
        vm.RootValidityHasError.Should().BeTrue();

        vm.RootValidityDays = 365;
        vm.RootValidityHasError.Should().BeFalse();
    }

    [Fact]
    public async Task SaveToast_OnlyCertDefaults_WhenCertFieldChanges()
    {
        var store = new InMemoryPrefs(UserPreferences.Default);
        var vm = MakeVm(store);

        vm.KeyBits = 4096; // CertDefaults card
        // Wait past 500ms debounce + a margin
        await Task.Delay(800);

        vm.ShowSavedDefaults.Should().BeTrue();
        vm.ShowSavedApp.Should().BeFalse();
    }

    [Fact]
    public async Task SaveToast_OnlyApp_WhenRetentionChanges()
    {
        var store = new InMemoryPrefs(UserPreferences.Default);
        var vm = MakeVm(store);

        vm.ActivityRetention = ActivityRetention.OneHundred;
        await Task.Delay(800);

        vm.ShowSavedApp.Should().BeTrue();
        vm.ShowSavedDefaults.Should().BeFalse();
    }

    private static SettingsViewModel MakeVm(IUserPreferencesStore prefs) =>
        new(new NoopUpdate(), prefs, null, null, null);

    // ---- Fakes -------------------------------------------------------------

    private sealed class InMemoryPrefs : IUserPreferencesStore
    {
        public InMemoryPrefs(UserPreferences current) => Current = current;
        public UserPreferences Current { get; private set; }
        public UserPreferences? LastSaved { get; private set; }
        public int SaveCount { get; private set; }
        public event EventHandler<UserPreferences>? Changed;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAsync(UserPreferences prefs, CancellationToken ct = default)
        {
            Current = prefs;
            LastSaved = prefs;
            SaveCount++;
            Changed?.Invoke(this, prefs);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryLog : IActivityLog
    {
        private readonly List<ActivityEntry> _items = new();
        public IReadOnlyList<ActivityEntry> Recent => _items.OrderByDescending(e => e.At).ToList();
        public int MaxEntries { get; set; } = 500;
        public event EventHandler? Changed;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task AppendAsync(ActivityEntry e, CancellationToken ct = default)
        {
            _items.Add(e);
            Changed?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
        public Task ClearAsync(CancellationToken ct = default)
        {
            _items.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingFolder : IDataFolderService
    {
        public string DataFolderPath => "/test/path";
        public string RevealLabel => "Reveal";
        public int Reveals { get; private set; }
        public Task RevealAsync() { Reveals++; return Task.CompletedTask; }
    }

    private sealed class AlwaysConfirm : IConfirmationDialog
    {
        public int Calls { get; private set; }
        public Task<bool> ShowAsync(string title, string message, string confirmLabel = "Confirm", string cancelLabel = "Cancel")
        { Calls++; return Task.FromResult(true); }
    }

    private sealed class AlwaysDeny : IConfirmationDialog
    {
        public Task<bool> ShowAsync(string title, string message, string confirmLabel = "Confirm", string cancelLabel = "Cancel")
            => Task.FromResult(false);
    }

    private sealed class NoopUpdate : IUpdateService
    {
        public bool IsUpdateSupported => false;
        public Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default) => Task.FromResult<UpdateInfo?>(null);
        public Task DownloadUpdateAsync(UpdateInfo update, IProgress<int>? progress = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyUpdateAndRestartAsync(UpdateInfo update) => Task.CompletedTask;
    }
}
