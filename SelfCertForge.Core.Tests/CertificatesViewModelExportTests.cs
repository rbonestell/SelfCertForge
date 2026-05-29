using System.Windows.Input;
using FluentAssertions;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;
using Xunit;

namespace SelfCertForge.Core.Tests;

public sealed class CertificatesViewModelExportTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 5, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExportDer_RunsThroughOverlay_WithCaption()
    {
        var overlay = new FakeLoadingOverlay();
        var store = new FakeStore(Child("c1", "r1"));
        var vm = new CertificatesViewModel(
            store,
            () => DateTimeOffset.UtcNow,
            exportService: new NoOpExportService(),
            folderPicker: new StubFolderPicker("/tmp/export"),
            loadingOverlay: overlay);
        await vm.LoadAsync();
        vm.SelectById("c1");

        ((ICommand)vm.ExportDerCommand).Execute(null);

        overlay.Messages.Should().ContainSingle().Which.Should().Be("Exporting Certificate…");
    }

    private static StoredCertificate Child(string id, string issuerId) => new(
        Id: id, Kind: StoredCertificateKind.Child, CommonName: id,
        Subject: $"CN={id}", IssuerId: issuerId, IssuerName: "Root",
        Sans: Array.Empty<string>(), Algorithm: "ECDSA",
        Serial: "0", Sha256: "", Sha1: "",
        IssuedAt: Now.AddDays(-30), ExpiresAt: Now.AddYears(1),
        InstalledInTrustStore: false);

    private sealed class NoOpExportService : ICertificateExportService
    {
        public Task ExportKeyPemAsync(StoredCertificate cert, string outputFolder, CancellationToken ct = default) => Task.CompletedTask;
        public Task ExportPfxAsync(StoredCertificate cert, string outputFolder, string? password, CancellationToken ct = default) => Task.CompletedTask;
        public Task ExportDerAsync(StoredCertificate cert, string outputFolder, CancellationToken ct = default) => Task.CompletedTask;
        public Task ExportP7bAsync(StoredCertificate cert, string outputFolder, CancellationToken ct = default) => Task.CompletedTask;
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

    private sealed class StubFolderPicker : IFolderPicker
    {
        private readonly string _path;
        public StubFolderPicker(string path) => _path = path;
        public Task<string?> PickAsync(CancellationToken ct = default) => Task.FromResult<string?>(_path);
    }
}
