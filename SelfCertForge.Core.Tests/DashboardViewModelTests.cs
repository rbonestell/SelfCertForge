using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.Core.Tests;

public sealed class DashboardViewModelTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 5, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EmptyStore_AllStatsZero()
    {
        var vm = new DashboardViewModel(new FakeStore(), new FakeLog(), () => Now);
        vm.TotalCertificates.Should().Be(0);
        vm.RootAuthorities.Should().Be(0);
        vm.InstalledRoots.Should().Be(0);
        vm.ExpiringSoon.Should().Be(0);
        vm.IsActivityEmpty.Should().BeTrue();
    }

    [Fact]
    public void Stats_CountByKindAndTrustState()
    {
        var store = new FakeStore(
            Root("r1", sha1: "AA"),
            Root("r2", sha1: "BB"),
            Child("c1", "r1", expires: Now.AddYears(1)),
            Child("c2", "r1", expires: Now.AddDays(15)),    // expiring
            Child("c3", "r1", expires: Now.AddDays(-5)),    // expired
            Child("c4", "r1", expires: Now.AddYears(2)));
        var checker = new FakeTrustStoreChecker("AA"); // only r1 trusted

        var vm = new DashboardViewModel(store, new FakeLog(), () => Now, checker);

        vm.TotalCertificates.Should().Be(4);
        vm.RootAuthorities.Should().Be(2);
        vm.InstalledRoots.Should().Be(1);
        vm.ExpiringSoon.Should().Be(2);   // c2 + c3
    }

    [Fact]
    public async Task Activity_PopulatesFromLog()
    {
        var log = new FakeLog();
        await log.AppendAsync(new ActivityEntry("a1", Now.AddMinutes(-2), "test", "Forged certificate.", null));
        var vm = new DashboardViewModel(new FakeStore(), log, () => Now);
        vm.HasActivity.Should().BeTrue();
        vm.Activity.Should().ContainSingle().Which.Message.Should().Be("Forged certificate.");
    }

    [Fact]
    public void StoreChange_TriggersRefresh()
    {
        var store = new FakeStore();
        var vm = new DashboardViewModel(store, new FakeLog(), () => Now);
        vm.RootAuthorities.Should().Be(0);
        store.Add(Root("r1"));
        vm.RootAuthorities.Should().Be(1);
    }

    private static StoredCertificate Root(string id, bool installed = true, string sha1 = "") => new(
        Id: id, Kind: StoredCertificateKind.Root, CommonName: id,
        Subject: $"CN={id}", IssuerId: null, IssuerName: null,
        Sans: Array.Empty<string>(), Algorithm: "RSA",
        Serial: "0", Sha256: "", Sha1: sha1,
        IssuedAt: Now.AddYears(-1), ExpiresAt: Now.AddYears(10),
        InstalledInTrustStore: installed);

    private static StoredCertificate Child(string id, string issuerId, DateTimeOffset expires) => new(
        Id: id, Kind: StoredCertificateKind.Child, CommonName: id,
        Subject: $"CN={id}", IssuerId: issuerId, IssuerName: "Root",
        Sans: Array.Empty<string>(), Algorithm: "ECDSA",
        Serial: "0", Sha256: "", Sha1: "",
        IssuedAt: Now.AddDays(-30), ExpiresAt: expires,
        InstalledInTrustStore: false);

    private sealed class FakeStore : ICertificateStore
    {
        private readonly List<StoredCertificate> _items;
        public FakeStore(params StoredCertificate[] items) { _items = new(items); }
        public IReadOnlyList<StoredCertificate> All => _items;
        public event EventHandler? Changed;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task AddAsync(StoredCertificate cert, CancellationToken ct = default) { Add(cert); return Task.CompletedTask; }
        public Task UpdateAsync(StoredCertificate cert, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
        public void Add(StoredCertificate c) { _items.Add(c); Changed?.Invoke(this, EventArgs.Empty); }
    }

    private sealed class FakeTrustStoreChecker : ITrustStoreChecker
    {
        private readonly HashSet<string> _trusted;
        public FakeTrustStoreChecker(params string[] trustedSha1s) =>
            _trusted = new HashSet<string>(trustedSha1s, StringComparer.OrdinalIgnoreCase);
        public event EventHandler? Changed;
        public bool IsTrusted(string sha1) => _trusted.Contains(sha1.Replace(":", ""));
        public Task<(bool Success, string? ErrorMessage)> InstallAsync(string _) =>
            Task.FromResult((true, (string?)null));
    }

    private sealed class FakeLog : IActivityLog
    {
        private readonly List<ActivityEntry> _items = new();
        public IReadOnlyList<ActivityEntry> Recent => _items.OrderByDescending(e => e.At).ToList();
        public int MaxEntries { get; set; } = 500;
        public event EventHandler? Changed;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task AppendAsync(ActivityEntry entry, CancellationToken ct = default)
        {
            _items.Add(entry);
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
}
