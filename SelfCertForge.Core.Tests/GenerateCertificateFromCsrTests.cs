using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SelfCertForge.Core.Models;
using SelfCertForge.Infrastructure;
using Xunit;

namespace SelfCertForge.Core.Tests;

public sealed class GenerateCertificateFromCsrTests : IDisposable
{
    private readonly string _tmpDir;
    private readonly string _caCertPath;
    private readonly string _caKeyPath;
    private readonly DotNetCryptoCertificateWorkflowService _svc = new();

    public GenerateCertificateFromCsrTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "scf-csr-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpDir);

        using var caKey = RSA.Create(2048);
        var caReq = new CertificateRequest("CN=Test CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        caReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        caReq.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(caReq.PublicKey, false));
        caReq.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        using var caCert = caReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));

        _caCertPath = Path.Combine(_tmpDir, "ca.pem");
        _caKeyPath = Path.Combine(_tmpDir, "ca.key");
        File.WriteAllText(_caCertPath, caCert.ExportCertificatePem());
        File.WriteAllText(_caKeyPath, caKey.ExportRSAPrivateKeyPem());
    }

    public void Dispose() => Directory.Delete(_tmpDir, true);

    [Fact]
    public async Task Signs_cert_with_csr_public_key_and_writes_pem_and_crt()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-001");
        var outDir = Path.Combine(_tmpDir, "out");
        var req = MakeRequest(csrPem);

        var result = await _svc.GenerateCertificateFromCsrAsync(
            req, _caCertPath, _caKeyPath, outDir, "device-001");

        File.Exists(result.CertPemPath).Should().BeTrue();
        File.Exists(Path.ChangeExtension(result.CertPemPath, ".crt")).Should().BeTrue();
        result.KeyPath.Should().BeNull();

        var pem = File.ReadAllText(result.CertPemPath);
        using var issued = X509Certificate2.CreateFromPem(pem);
        issued.Subject.Should().Contain("CN=device-001");
    }

    [Fact]
    public async Task Issued_cert_signed_by_CA()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-002");
        var outDir = Path.Combine(_tmpDir, "out2");
        var req = MakeRequest(csrPem);

        var result = await _svc.GenerateCertificateFromCsrAsync(
            req, _caCertPath, _caKeyPath, outDir, "device-002");

        var pem = File.ReadAllText(result.CertPemPath);
        using var issued = X509Certificate2.CreateFromPem(pem);
        using var ca = X509Certificate2.CreateFromPem(File.ReadAllText(_caCertPath));

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;

        chain.Build(issued).Should().BeTrue();
    }

    [Fact]
    public async Task Honors_operator_chosen_KU_and_EKU_when_csr_has_none()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-003");
        var outDir = Path.Combine(_tmpDir, "out3");
        var req = MakeRequest(csrPem) with
        {
            KeyUsageDigitalSignature = true,
            KeyUsageKeyEncipherment = true,
            EkuServerAuth = true,
            EkuClientAuth = true,
        };

        var result = await _svc.GenerateCertificateFromCsrAsync(
            req, _caCertPath, _caKeyPath, outDir, "device-003");

        var pem = File.ReadAllText(result.CertPemPath);
        using var issued = X509Certificate2.CreateFromPem(pem);

        var ku = issued.Extensions.OfType<X509KeyUsageExtension>().Single();
        ku.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature).Should().BeTrue();
        ku.KeyUsages.HasFlag(X509KeyUsageFlags.KeyEncipherment).Should().BeTrue();

        var eku = issued.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single();
        eku.EnhancedKeyUsages.Cast<Oid>().Should().Contain(o => o.Value == "1.3.6.1.5.5.7.3.1");
        eku.EnhancedKeyUsages.Cast<Oid>().Should().Contain(o => o.Value == "1.3.6.1.5.5.7.3.2");
    }

    [Fact]
    public async Task Sets_AKI_from_issuer_SKI()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-004");
        var outDir = Path.Combine(_tmpDir, "out4");
        var result = await _svc.GenerateCertificateFromCsrAsync(
            MakeRequest(csrPem), _caCertPath, _caKeyPath, outDir, "device-004");

        var pem = File.ReadAllText(result.CertPemPath);
        using var issued = X509Certificate2.CreateFromPem(pem);
        issued.Extensions.Any(e => e.Oid?.Value == "2.5.29.35" /* AuthorityKeyIdentifier */)
            .Should().BeTrue();
    }

    [Fact]
    public async Task Includes_operator_SANs_in_issued_cert()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-005",
            sanDnsNames: new[] { "csr.example" });
        var outDir = Path.Combine(_tmpDir, "out5");

        var req = MakeRequest(csrPem) with
        {
            Sans = new[]
            {
                new CsrSignedSanEntry("csr.example", CsrSignedSanOrigin.FromCsr),
                new CsrSignedSanEntry("added.example", CsrSignedSanOrigin.AddedByOperator),
            },
        };

        var result = await _svc.GenerateCertificateFromCsrAsync(
            req, _caCertPath, _caKeyPath, outDir, "device-005");

        var pem = File.ReadAllText(result.CertPemPath);
        using var issued = X509Certificate2.CreateFromPem(pem);
        var san = issued.Extensions.OfType<X509SubjectAlternativeNameExtension>().Single();
        var names = san.EnumerateDnsNames().ToList();

        names.Should().Contain("csr.example");
        names.Should().Contain("added.example");
    }

    [Fact]
    public async Task Strips_KeyCertSign_and_CrlSign_even_when_requested()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-ca-attempt");
        var outDir = Path.Combine(_tmpDir, "out-ca-strip");

        var req = MakeRequest(csrPem) with
        {
            KeyUsageDigitalSignature = true,
            KeyUsageKeyCertSign = true,
            KeyUsageCrlSign = true,
        };

        var result = await _svc.GenerateCertificateFromCsrAsync(
            req, _caCertPath, _caKeyPath, outDir, "device-ca-attempt");

        var pem = File.ReadAllText(result.CertPemPath);
        using var issued = X509Certificate2.CreateFromPem(pem);

        var ku = issued.Extensions.OfType<X509KeyUsageExtension>().Single();
        ku.KeyUsages.HasFlag(X509KeyUsageFlags.KeyCertSign).Should().BeFalse();
        ku.KeyUsages.HasFlag(X509KeyUsageFlags.CrlSign).Should().BeFalse();
        ku.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature).Should().BeTrue();

        var bc = issued.Extensions.OfType<X509BasicConstraintsExtension>().Single();
        bc.CertificateAuthority.Should().BeFalse();
    }

    [Fact]
    public async Task Preserves_IP_address_SANs_from_csr_through_signing()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-ip",
            sanDnsNames: new[] { "device.example" },
            sanIpAddresses: new[] { "192.0.2.42" });
        var outDir = Path.Combine(_tmpDir, "out-ip-san");

        var req = MakeRequest(csrPem) with
        {
            Sans = new[]
            {
                new CsrSignedSanEntry("device.example", CsrSignedSanOrigin.FromCsr),
                new CsrSignedSanEntry("IP:192.0.2.42", CsrSignedSanOrigin.FromCsr),
            },
        };

        var result = await _svc.GenerateCertificateFromCsrAsync(
            req, _caCertPath, _caKeyPath, outDir, "device-ip");

        var pem = File.ReadAllText(result.CertPemPath);
        using var issued = X509Certificate2.CreateFromPem(pem);
        var san = issued.Extensions.OfType<X509SubjectAlternativeNameExtension>().Single();

        san.EnumerateDnsNames().Should().Contain("device.example");
        san.EnumerateIPAddresses().Select(ip => ip.ToString())
            .Should().Contain("192.0.2.42");
    }

    [Fact]
    public async Task Honors_EkuEmailProtection_from_csr()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-email");
        var outDir = Path.Combine(_tmpDir, "out-eku-email");

        var req = MakeRequest(csrPem) with { EkuEmailProtection = true };

        var result = await _svc.GenerateCertificateFromCsrAsync(
            req, _caCertPath, _caKeyPath, outDir, "device-email");

        var pem = File.ReadAllText(result.CertPemPath);
        using var issued = X509Certificate2.CreateFromPem(pem);

        var eku = issued.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single();
        eku.EnhancedKeyUsages.Cast<Oid>().Should().Contain(o => o.Value == "1.3.6.1.5.5.7.3.4");
    }

    private CsrSigningRequest MakeRequest(string csrPem) => new(
        SigningAuthorityId: "test-ca",
        RawCsrPem: csrPem,
        SourceCsrFilename: "test.csr",
        ValidityDays: 397,
        Sans: Array.Empty<CsrSignedSanEntry>(),
        KeyUsageDigitalSignature: false,
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
