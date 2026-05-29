using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;
using Xunit;

namespace SelfCertForge.Core.Tests;

public sealed class CertificatesViewModelCsrTests
{
    [Fact]
    public void IsFromCsr_true_when_StoredCertificate_IssuedFromCsr_true()
    {
        var c = MakeStored(issuedFromCsr: true);
        var row = new CertificateRowViewModel(c, isTrusted: false);
        Assert.True(row.IsFromCsr);
        Assert.False(row.HasPrivateKey);
        Assert.False(row.CanExportPfx);
        // PEM export only needs the cert path — CSR-signed certs without a
        // private key still emit a valid cert .pem.
        Assert.True(row.CanExportKeyPem);
    }

    [Fact]
    public void IsFromCsr_false_for_regular_signed_cert()
    {
        var c = MakeStored(issuedFromCsr: false, privateKeyPath: "/tmp/x.key");
        var row = new CertificateRowViewModel(c, isTrusted: false);
        Assert.False(row.IsFromCsr);
        Assert.True(row.HasPrivateKey);
        Assert.True(row.CanExportPfx);
        Assert.True(row.CanExportKeyPem);
    }

    private static StoredCertificate MakeStored(bool issuedFromCsr, string? privateKeyPath = null) => new(
        "id", StoredCertificateKind.Child, "device", "CN=device",
        "ca-id", "Test CA", Array.Empty<string>(),
        "RSA", "01", "AA", "BB",
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1),
        false,
        CertificatePath: "/tmp/x.pem",
        PrivateKeyPath: privateKeyPath,
        OutputDirectory: null, KeyUsages: null, ExtendedKeyUsages: null,
        IssuedFromCsr: issuedFromCsr,
        SourceCsrFilename: issuedFromCsr ? "x.csr" : null);
}
