using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Infrastructure;

namespace SelfCertForge.Core.Tests;

public sealed class CertificateExportServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(),
        "selfcertforge-export-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private sealed class FakeStore(IReadOnlyList<StoredCertificate> certs) : ICertificateStore
    {
        public IReadOnlyList<StoredCertificate> All => certs;
#pragma warning disable CS0067
        public event EventHandler? Changed;
#pragma warning restore CS0067
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task AddAsync(StoredCertificate cert, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(StoredCertificate cert, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private CertificateExportService MakeSut(IReadOnlyList<StoredCertificate>? storeContents = null) =>
        new(new FakeStore(storeContents ?? []));

    private static StoredCertificate MakeCert(string id, string commonName,
        string? certPath = null, string? keyPath = null, string? issuerId = null) =>
        new(id, StoredCertificateKind.Root, commonName,
            $"CN={commonName}", issuerId, null,
            [], "RSA-2048", "00", "AA", "BB",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1),
            false, certPath, keyPath);

    private (string certPath, string keyPath) WriteTestCertAndKey(string baseName)
    {
        Directory.CreateDirectory(_dir);
        using var key = RSA.Create(2048);
        var req = new CertificateRequest($"CN={baseName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddYears(1));
        var certPath = Path.Combine(_dir, $"{baseName}.pem");
        var keyPath = Path.Combine(_dir, $"{baseName}.key");
        File.WriteAllText(certPath, cert.ExportCertificatePem());
        File.WriteAllText(keyPath, key.ExportRSAPrivateKeyPem());
        return (certPath, keyPath);
    }

    private string WriteTestCertOnly(string baseName)
    {
        Directory.CreateDirectory(_dir);
        using var key = RSA.Create(2048);
        var req = new CertificateRequest($"CN={baseName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddYears(1));
        var certPath = Path.Combine(_dir, $"{baseName}.pem");
        File.WriteAllText(certPath, cert.ExportCertificatePem());
        return certPath;
    }

    // ExportKeyPemAsync

    [Fact]
    public async Task ExportKeyPemAsync_NoCertPath_ThrowsInvalidOperationException()
    {
        var cert = MakeCert("1", "Test");
        var sut = MakeSut();
        var act = () => sut.ExportKeyPemAsync(cert, _dir);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExportKeyPemAsync_CertOnly_WritesPemNoKey()
    {
        var certPath = WriteTestCertOnly("cert-only");
        var cert = MakeCert("1", "cert-only", certPath: certPath);
        var outDir = Path.Combine(_dir, "out");
        var sut = MakeSut();
        await sut.ExportKeyPemAsync(cert, outDir);
        File.Exists(Path.Combine(outDir, "cert-only.pem")).Should().BeTrue();
        File.Exists(Path.Combine(outDir, "cert-only.key")).Should().BeFalse();
    }

    [Fact]
    public async Task ExportKeyPemAsync_WithKey_WritesPemAndKey()
    {
        var (certPath, keyPath) = WriteTestCertAndKey("with-key");
        var cert = MakeCert("1", "with-key", certPath: certPath, keyPath: keyPath);
        var outDir = Path.Combine(_dir, "out");
        var sut = MakeSut();
        await sut.ExportKeyPemAsync(cert, outDir);
        File.Exists(Path.Combine(outDir, "with-key.pem")).Should().BeTrue();
        File.Exists(Path.Combine(outDir, "with-key.key")).Should().BeTrue();
    }

    // ExportPfxAsync

    [Fact]
    public async Task ExportPfxAsync_NoCertPath_ThrowsInvalidOperationException()
    {
        var cert = MakeCert("1", "Test");
        var sut = MakeSut();
        var act = () => sut.ExportPfxAsync(cert, _dir, "pass");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExportPfxAsync_NoKeyPath_ThrowsInvalidOperationException()
    {
        var certPath = WriteTestCertOnly("pfx-no-key");
        var cert = MakeCert("1", "pfx-no-key", certPath: certPath);
        var sut = MakeSut();
        var act = () => sut.ExportPfxAsync(cert, _dir, "pass");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExportPfxAsync_WithPassword_WritesPfxReadableWithPassword()
    {
        var (certPath, keyPath) = WriteTestCertAndKey("pfx-with-pass");
        var cert = MakeCert("1", "pfx-with-pass", certPath: certPath, keyPath: keyPath);
        var outDir = Path.Combine(_dir, "out");
        var sut = MakeSut();
        await sut.ExportPfxAsync(cert, outDir, "s3cr3t");
        var pfxPath = Path.Combine(outDir, "pfx-with-pass.pfx");
        File.Exists(pfxPath).Should().BeTrue();
        var act = () => X509CertificateLoader.LoadPkcs12FromFile(pfxPath, "s3cr3t");
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ExportPfxAsync_NoPassword_WritesPfx()
    {
        var (certPath, keyPath) = WriteTestCertAndKey("pfx-no-pass");
        var cert = MakeCert("1", "pfx-no-pass", certPath: certPath, keyPath: keyPath);
        var outDir = Path.Combine(_dir, "out");
        var sut = MakeSut();
        await sut.ExportPfxAsync(cert, outDir, null);
        File.Exists(Path.Combine(outDir, "pfx-no-pass.pfx")).Should().BeTrue();
    }

    // ExportDerAsync

    [Fact]
    public async Task ExportDerAsync_NoCertPath_ThrowsInvalidOperationException()
    {
        var cert = MakeCert("1", "Test");
        var sut = MakeSut();
        var act = () => sut.ExportDerAsync(cert, _dir);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExportDerAsync_CertOnly_WritesDerNoKey()
    {
        var certPath = WriteTestCertOnly("der-cert-only");
        var cert = MakeCert("1", "der-cert-only", certPath: certPath);
        var outDir = Path.Combine(_dir, "out");
        var sut = MakeSut();
        await sut.ExportDerAsync(cert, outDir);
        File.Exists(Path.Combine(outDir, "der-cert-only.der")).Should().BeTrue();
        File.Exists(Path.Combine(outDir, "der-cert-only.key")).Should().BeFalse();
    }

    [Fact]
    public async Task ExportDerAsync_WithKey_WritesDerAndImportableKey()
    {
        var (certPath, keyPath) = WriteTestCertAndKey("der-with-key");
        var cert = MakeCert("1", "der-with-key", certPath: certPath, keyPath: keyPath);
        var outDir = Path.Combine(_dir, "out");
        var sut = MakeSut();
        await sut.ExportDerAsync(cert, outDir);

        var derPath = Path.Combine(outDir, "der-with-key.der");
        var exportedKeyPath = Path.Combine(outDir, "der-with-key.key");

        File.Exists(derPath).Should().BeTrue();
        File.ReadAllBytes(derPath).Should().NotBeEmpty();

        File.Exists(exportedKeyPath).Should().BeTrue();
        var keyBytes = File.ReadAllBytes(exportedKeyPath);
        using var imported = RSA.Create();
        imported.ImportRSAPrivateKey(keyBytes, out _);
    }

    // ExportP7bAsync

    [Fact]
    public async Task ExportP7bAsync_NoCertPath_ThrowsInvalidOperationException()
    {
        var cert = MakeCert("1", "Test");
        var sut = MakeSut();
        var act = () => sut.ExportP7bAsync(cert, _dir);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExportP7bAsync_LeafOnly_WritesP7b()
    {
        var certPath = WriteTestCertOnly("p7b-leaf");
        var cert = MakeCert("1", "p7b-leaf", certPath: certPath);
        var outDir = Path.Combine(_dir, "out");
        var sut = MakeSut();
        await sut.ExportP7bAsync(cert, outDir);
        File.Exists(Path.Combine(outDir, "p7b-leaf.p7b")).Should().BeTrue();
    }

    [Fact(Skip = "X509Certificate2Collection.Export(Pkcs7) throws NullReferenceException on macOS/.NET when certs are loaded via CreateFromPem (no native SecCertificate handle). Passes on Windows.")]
    public async Task ExportP7bAsync_WithIssuerInStore_WritesP7b()
    {
        Directory.CreateDirectory(_dir);
        using var rootKey = RSA.Create(2048);
        var rootReq = new CertificateRequest("CN=p7b-issuer", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var rootCert = rootReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddYears(2));

        using var leafKey = RSA.Create(2048);
        var leafReq = new CertificateRequest("CN=p7b-leaf-chain", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        leafReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        var serial = new byte[8];
        Random.Shared.NextBytes(serial);
        using var leafCert = leafReq.Create(rootCert, DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddYears(1), serial);

        var issuerCertPath = Path.Combine(_dir, "p7b-issuer.pem");
        var leafCertPath = Path.Combine(_dir, "p7b-leaf-chain.pem");
        File.WriteAllText(issuerCertPath, rootCert.ExportCertificatePem());
        File.WriteAllText(leafCertPath, leafCert.ExportCertificatePem());

        var issuer = MakeCert("issuer-id", "p7b-issuer", certPath: issuerCertPath);
        var leaf = MakeCert("leaf-id", "p7b-leaf-chain", certPath: leafCertPath, issuerId: "issuer-id");

        var outDir = Path.Combine(_dir, "out");
        var sut = MakeSut([issuer, leaf]);
        await sut.ExportP7bAsync(leaf, outDir);
        File.Exists(Path.Combine(outDir, "p7b-leaf-chain.p7b")).Should().BeTrue();
    }

    [Fact]
    public async Task ExportP7bAsync_WithKey_WritesKeyAlongside()
    {
        var (certPath, keyPath) = WriteTestCertAndKey("p7b-with-key");
        var cert = MakeCert("1", "p7b-with-key", certPath: certPath, keyPath: keyPath);
        var outDir = Path.Combine(_dir, "out");
        var sut = MakeSut();
        await sut.ExportP7bAsync(cert, outDir);
        File.Exists(Path.Combine(outDir, "p7b-with-key.p7b")).Should().BeTrue();
        File.Exists(Path.Combine(outDir, "p7b-with-key.key")).Should().BeTrue();
    }
}
