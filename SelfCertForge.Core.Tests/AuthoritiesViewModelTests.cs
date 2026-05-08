using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;
using SelfCertForge.Infrastructure;

namespace SelfCertForge.Core.Tests;

public sealed class AuthoritiesViewModelTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 5, 4, 12, 0, 0, TimeSpan.Zero);

    private static AuthoritiesViewModel MakeVm(FakeStore store, ITrustStoreChecker? trustChecker = null) =>
        new(store, new NoOpCreateRootDialog(), new NoOpCreateSignedCertDialog(), new NoOpNavigationService(),
            new NoOpExportService(), new NoOpFolderPicker(), new NoOpPfxPasswordDialog(), new NoOpConfirmationDialog(),
            trustChecker);

    [Fact]
    public void EmptyStore_IsEmptyTrue()
    {
        var vm = MakeVm(new FakeStore());
        vm.IsEmpty.Should().BeTrue();
        vm.HasContent.Should().BeFalse();
        vm.Rows.Should().BeEmpty();
    }

    [Fact]
    public void IncludesOnlyRoots_NotChildren()
    {
        var store = new FakeStore(
            Root("r1", installed: true),
            Root("r2", installed: false),
            Child("c1", "r1"));
        var vm = MakeVm(store);

        vm.Rows.Should().HaveCount(2);
        vm.Rows.Select(r => r.Id).Should().BeEquivalentTo(new[] { "r1", "r2" });
    }

    [Fact]
    public void Pill_ReflectsTrustState()
    {
        var store = new FakeStore(
            Root("r1", sha1: "AA"),
            Root("r2", sha1: "BB"));
        var checker = new FakeTrustStoreChecker("AA"); // only r1 trusted
        var vm = MakeVm(store, checker);

        var r1 = vm.Rows.Single(r => r.Id == "r1");
        var r2 = vm.Rows.Single(r => r.Id == "r2");

        r1.PillKind.Should().Be("installed");
        r1.PillLabel.Should().Be("Trusted");
        r2.PillKind.Should().Be("uninstalled");
        r2.PillLabel.Should().Be("Not Trusted");
    }

    [Fact]
    public void StoreChange_RefreshesRows()
    {
        var store = new FakeStore();
        var vm = MakeVm(store);
        vm.IsEmpty.Should().BeTrue();
        store.Add(Root("r1"));
        vm.IsEmpty.Should().BeFalse();
        vm.Rows.Should().ContainSingle();
    }

    private static StoredCertificate Root(string id, bool installed = true, string sha1 = "") => new(
        Id: id, Kind: StoredCertificateKind.Root, CommonName: id,
        Subject: $"CN={id}", IssuerId: null, IssuerName: null,
        Sans: Array.Empty<string>(), Algorithm: "RSA",
        Serial: "0", Sha256: "", Sha1: sha1,
        IssuedAt: Now.AddYears(-1), ExpiresAt: Now.AddYears(10),
        InstalledInTrustStore: installed);

    private static StoredCertificate Child(string id, string issuerId) => new(
        Id: id, Kind: StoredCertificateKind.Child, CommonName: id,
        Subject: $"CN={id}", IssuerId: issuerId, IssuerName: "Root",
        Sans: Array.Empty<string>(), Algorithm: "ECDSA",
        Serial: "0", Sha256: "", Sha1: "",
        IssuedAt: Now.AddDays(-30), ExpiresAt: Now.AddYears(1),
        InstalledInTrustStore: false);

    private sealed class NoOpCreateRootDialog : ICreateRootDialog
    {
        public Task ShowAsync() => Task.CompletedTask;
    }

    private sealed class NoOpCreateSignedCertDialog : ICreateSignedCertDialog
    {
        public Task<StoredCertificate?> ShowAsync(string issuerId, string issuerName) =>
            Task.FromResult<StoredCertificate?>(null);
    }

    private sealed class NoOpNavigationService : INavigationService
    {
        public void NavigateToCertificate(string certId) { }
    }

    private sealed class NoOpExportService : ICertificateExportService
    {
        public Task ExportKeyPemAsync(StoredCertificate cert, string outputFolder, CancellationToken ct = default) => Task.CompletedTask;
        public Task ExportPfxAsync(StoredCertificate cert, string outputFolder, string? password, CancellationToken ct = default) => Task.CompletedTask;
        public Task ExportDerAsync(StoredCertificate cert, string outputFolder, CancellationToken ct = default) => Task.CompletedTask;
        public Task ExportP7bAsync(StoredCertificate cert, string outputFolder, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NoOpFolderPicker : IFolderPicker
    {
        public Task<string?> PickAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
    }

    private sealed class NoOpPfxPasswordDialog : IPfxPasswordDialog
    {
        public Task<(bool Confirmed, string? Password)> ShowAsync() => Task.FromResult((false, (string?)null));
    }

    private sealed class NoOpConfirmationDialog : IConfirmationDialog
    {
        public Task<bool> ShowAsync(string title, string message, string confirmLabel = "Confirm", string cancelLabel = "Cancel") =>
            Task.FromResult(false);
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
}
