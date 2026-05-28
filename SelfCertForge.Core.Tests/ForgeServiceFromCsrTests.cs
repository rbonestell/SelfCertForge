using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Infrastructure;

namespace SelfCertForge.Core.Tests;

public sealed class ForgeServiceFromCsrTests : IDisposable
{
    private readonly string _appDataDir;
    private readonly ICertificateStore _store;
    private readonly IActivityLog _log;
    private readonly DotNetCryptoCertificateWorkflowService _workflow = new();
    private readonly ForgeService _forge;
    private readonly StoredCertificate _ca;

    public ForgeServiceFromCsrTests()
    {
        _appDataDir = Path.Combine(Path.GetTempPath(), "scf-forge-csr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_appDataDir);
        _store = new InMemoryCertificateStore();
        _log = new InMemoryActivityLog();
        _forge = new ForgeService(_store, _log, _workflow, _appDataDir);

        // Bootstrap a real CA so ForgeFromCsrAsync can locate issuer files on disk.
        _ca = _forge.ForgeAsync(new ForgeRequest(
            Mode: ForgeMode.Root,
            CommonName: "Test CA",
            ValidityDays: 1825,
            KeyBits: 2048,
            IssuerId: null,
            Sans: [],
            InstallInTrustStore: false)).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (Directory.Exists(_appDataDir)) Directory.Delete(_appDataDir, recursive: true);
    }

    [Fact]
    public async Task ForgeFromCsr_persists_StoredCertificate_with_IssuedFromCsr_true_and_no_private_key()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-009");
        var req = new ForgeFromCsrRequest(MinimalRequest(_ca.Id, csrPem, "device-009.csr"));

        var stored = await _forge.ForgeFromCsrAsync(req);

        stored.IssuedFromCsr.Should().BeTrue();
        stored.PrivateKeyPath.Should().BeNull();
        stored.SourceCsrFilename.Should().Be("device-009.csr");
        stored.Kind.Should().Be(StoredCertificateKind.Child);
        stored.IssuerId.Should().Be(_ca.Id);
        stored.IssuerName.Should().Be(_ca.CommonName);
        stored.InstalledInTrustStore.Should().BeFalse();
        stored.CertificatePath.Should().NotBeNullOrWhiteSpace();
        File.Exists(stored.CertificatePath).Should().BeTrue();
        _store.All.Should().ContainSingle(c => c.Id == stored.Id);
    }

    [Fact]
    public async Task ForgeFromCsr_appends_activity_entry_with_SignedFromCsr_kind()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-010");
        var req = new ForgeFromCsrRequest(MinimalRequest(_ca.Id, csrPem, "device-010.csr"));

        // Use a fresh log so we only see the entry from this call (the CA forge also logged).
        var freshLog = new InMemoryActivityLog();
        var forge2 = new ForgeService(_store, freshLog, _workflow, _appDataDir);

        await forge2.ForgeFromCsrAsync(req);

        freshLog.Recent.Should().ContainSingle()
            .Which.Kind.Should().Be("SignedFromCsr");
    }

    [Fact]
    public async Task ForgeFromCsr_throws_InvalidOperationException_when_csr_is_tampered()
    {
        var csrPem = CsrFixtureGenerator.TamperedRsa(2048, "CN=device-011");
        var act = () => _forge.ForgeFromCsrAsync(
            new ForgeFromCsrRequest(MinimalRequest(_ca.Id, csrPem, "device-011.csr")));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*CSR failed re-inspection*");
    }

    [Fact]
    public async Task ForgeFromCsr_throws_InvalidOperationException_when_ca_not_found()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-012");
        var act = () => _forge.ForgeFromCsrAsync(
            new ForgeFromCsrRequest(MinimalRequest("does-not-exist", csrPem, "device-012.csr")));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Issuing root authority not found*");
    }

    private static CsrSigningRequest MinimalRequest(string caId, string csrPem, string filename) => new(
        SigningAuthorityId: caId,
        RawCsrPem: csrPem,
        SourceCsrFilename: filename,
        ValidityDays: 397,
        Sans: Array.Empty<CsrSignedSanEntry>(),
        KeyUsageDigitalSignature: true,
        KeyUsageNonRepudiation: false,
        KeyUsageKeyEncipherment: false,
        KeyUsageDataEncipherment: false,
        KeyUsageKeyAgreement: false,
        KeyUsageKeyCertSign: false,
        KeyUsageCrlSign: false,
        EkuServerAuth: false,
        EkuClientAuth: false,
        EkuCodeSigning: false,
        EkuTimeStamping: false,
        EkuEmailProtection: false,
        SignatureHashAlgorithm: HashAlgorithmKind.Sha256);
}

file sealed class InMemoryCertificateStore : ICertificateStore
{
    private readonly List<StoredCertificate> _items = new();
    public IReadOnlyList<StoredCertificate> All => _items;
    public event EventHandler? Changed;

    public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task AddAsync(StoredCertificate cert, CancellationToken ct = default)
    {
        _items.Add(cert);
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string id, CancellationToken ct = default)
    {
        _items.RemoveAll(c => c.Id == id);
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(StoredCertificate cert, CancellationToken ct = default)
    {
        var idx = _items.FindIndex(c => c.Id == cert.Id);
        if (idx >= 0) _items[idx] = cert;
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}

file sealed class InMemoryActivityLog : IActivityLog
{
    private readonly List<ActivityEntry> _entries = new();
    public IReadOnlyList<ActivityEntry> Recent => _entries;
    public int MaxEntries { get; set; } = -1;
    public event EventHandler? Changed;

    public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task AppendAsync(ActivityEntry entry, CancellationToken ct = default)
    {
        _entries.Add(entry);
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        _entries.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}
