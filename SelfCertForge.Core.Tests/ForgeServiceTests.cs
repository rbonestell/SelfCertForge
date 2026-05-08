using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Infrastructure;

namespace SelfCertForge.Core.Tests;

public sealed class ForgeServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(),
        "selfcertforge-forge-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private ForgeService MakeSut() =>
        new(new JsonCertificateStore(_dir), new JsonActivityLog(_dir),
            new FakeCertificateWorkflowService(), _dir);

    [Fact]
    public async Task ForgeRoot_PersistsAsRoot_WithTrustState()
    {
        var store = new JsonCertificateStore(_dir);
        var log = new JsonActivityLog(_dir);
        var svc = new ForgeService(store, log, new FakeCertificateWorkflowService(), _dir);

        var stored = await svc.ForgeAsync(new ForgeRequest(
            Mode: ForgeMode.Root,
            CommonName: "Self-Signed Root Authority",
            ValidityDays: 9125,
            KeyBits: 4096,
            IssuerId: null,
            Sans: [],
            InstallInTrustStore: true));

        stored.Kind.Should().Be(StoredCertificateKind.Root);
        stored.InstalledInTrustStore.Should().BeTrue();
        stored.IssuerId.Should().BeNull();
        stored.IssuerName.Should().BeNull();
        stored.CertificatePath.Should().NotBeNullOrEmpty();
        stored.PrivateKeyPath.Should().NotBeNullOrEmpty();
        store.All.Should().ContainSingle().Which.Id.Should().Be(stored.Id);
        log.Recent.Should().ContainSingle()
            .Which.Kind.Should().Be("forged-root");
    }

    [Fact]
    public async Task ForgeChild_LinksToIssuer_AndParsesSans()
    {
        var store = new JsonCertificateStore(_dir);
        var log = new JsonActivityLog(_dir);
        var svc = new ForgeService(store, log, new FakeCertificateWorkflowService(), _dir);

        var root = await svc.ForgeAsync(new ForgeRequest(
            ForgeMode.Root, "Root", 9125, 4096, null, [], false));

        var child = await svc.ForgeAsync(new ForgeRequest(
            ForgeMode.Child,
            CommonName: "api.local",
            ValidityDays: 397,
            KeyBits: 2048,
            IssuerId: root.Id,
            Sans: ["DNS:api.local", "DNS:*.api.local", "IP:127.0.0.1"],
            InstallInTrustStore: false));

        child.Kind.Should().Be(StoredCertificateKind.Child);
        child.IssuerId.Should().Be(root.Id);
        child.IssuerName.Should().Be("Root");
        child.Sans.Should().BeEquivalentTo(new[] { "DNS:api.local", "DNS:*.api.local", "IP:127.0.0.1" });
        child.InstalledInTrustStore.Should().BeFalse();
        child.CertificatePath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Forge_RejectsBlankCommonName()
    {
        var act = () => MakeSut().ForgeAsync(new ForgeRequest(
            ForgeMode.Root, "", 9125, 4096, null, [], false));
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Forge_RejectsZeroValidity()
    {
        var act = () => MakeSut().ForgeAsync(new ForgeRequest(
            ForgeMode.Root, "CN", 0, 4096, null, [], false));
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Forge_ProducesRealCertMetadata()
    {
        var stored = await MakeSut().ForgeAsync(new ForgeRequest(
            ForgeMode.Root, "test", 365, 2048, null, [], false));

        // SHA-256 is always 32 bytes; SHA-1 always 20 bytes. Serial varies by issuer.
        stored.Sha256.Should().MatchRegex(@"^([0-9A-F]{2}:){31}[0-9A-F]{2}$");
        stored.Sha1.Should().MatchRegex(@"^([0-9A-F]{2}:){19}[0-9A-F]{2}$");
        stored.Serial.Should().MatchRegex(@"^[0-9A-F]{2}(:[0-9A-F]{2})*$");
        stored.CertificatePath.Should().NotBeNullOrEmpty();
        File.Exists(stored.CertificatePath).Should().BeTrue();
    }
}

/// <summary>
/// Creates real parseable X.509 certificates using .NET's CertificateRequest API,
/// bypassing the OpenSSL CLI so tests run without an installed openssl binary.
/// </summary>
file sealed class FakeCertificateWorkflowService : ICertificateWorkflowService
{
    public Task<CertificateGenerationResult> GenerateRootCertificateAsync(
        RootCertificateRequest request, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(request.OutputDirectory);

        using var key = RSA.Create(2048);
        var req = new CertificateRequest(
            $"CN={request.RootName}, O=SelfCertForge",
            key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));

        var notBefore = DateTimeOffset.UtcNow.AddSeconds(-1);
        var notAfter = notBefore.AddDays(request.ValidForDays);
        using var cert = req.CreateSelfSigned(notBefore, notAfter);

        var keyPath = Path.Combine(request.OutputDirectory, $"{request.RootName}.key");
        var pemPath = Path.Combine(request.OutputDirectory, $"{request.RootName}.pem");
        File.WriteAllText(keyPath, key.ExportRSAPrivateKeyPem());
        File.WriteAllText(pemPath, cert.ExportCertificatePem());

        return Task.FromResult(new CertificateGenerationResult
        {
            OutputDirectory = request.OutputDirectory,
            GeneratedFiles = [keyPath, pemPath],
            CertPemPath = pemPath,
            KeyPath = keyPath,
        });
    }

    public Task<CertificateGenerationResult> GenerateSignedCertificateAsync(
        SignedCertificateRequest request, CancellationToken cancellationToken = default)
    {
        var outputDir = Path.Combine(request.OutputDirectory, request.CertificateName);
        Directory.CreateDirectory(outputDir);

        using var key = RSA.Create(2048);
        var certReq = new CertificateRequest(
            $"CN={request.CertificateName}, O=SelfCertForge",
            key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var notBefore = DateTimeOffset.UtcNow.AddSeconds(-1);
        var notAfter = notBefore.AddDays(request.ValidForDays);
        using var cert = certReq.CreateSelfSigned(notBefore, notAfter);

        var keyPath = Path.Combine(outputDir, $"{request.CertificateName}.key");
        var csrPath = Path.Combine(outputDir, $"{request.CertificateName}.csr");
        var crtPath = Path.Combine(outputDir, $"{request.CertificateName}.crt");
        var pemPath = Path.Combine(outputDir, $"{request.CertificateName}.pem");

        File.WriteAllText(csrPath, "FAKE CSR");
        File.WriteAllText(crtPath, cert.ExportCertificatePem());
        File.WriteAllText(pemPath, cert.ExportCertificatePem());
        File.WriteAllText(keyPath, key.ExportRSAPrivateKeyPem());

        return Task.FromResult(new CertificateGenerationResult
        {
            OutputDirectory = outputDir,
            GeneratedFiles = [csrPath, crtPath, pemPath, keyPath],
            CertPemPath = pemPath,
            KeyPath = keyPath,
        });
    }
}
