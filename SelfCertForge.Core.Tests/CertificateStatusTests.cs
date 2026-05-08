using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.Core.Tests;

public sealed class CertificateStatusTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 5, 4, 12, 0, 0, TimeSpan.Zero);

    private static StoredCertificate Root(string id = "r1", bool installed = true) => new(
        Id: id, Kind: StoredCertificateKind.Root, CommonName: "Root",
        Subject: "CN=Root", IssuerId: null, IssuerName: null,
        Sans: Array.Empty<string>(), Algorithm: "RSA",
        Serial: "0", Sha256: "", Sha1: "",
        IssuedAt: Now.AddYears(-1), ExpiresAt: Now.AddYears(10),
        InstalledInTrustStore: installed);

    private static StoredCertificate Child(string id, string? issuerId, DateTimeOffset expires) => new(
        Id: id, Kind: StoredCertificateKind.Child, CommonName: id,
        Subject: $"CN={id}", IssuerId: issuerId, IssuerName: "Root",
        Sans: Array.Empty<string>(), Algorithm: "ECDSA",
        Serial: "0", Sha256: "", Sha1: "",
        IssuedAt: Now.AddDays(-30), ExpiresAt: expires,
        InstalledInTrustStore: false);

    [Fact]
    public void Root_Trusted_KindIsInstalled()
    {
        CertificateStatus.DeriveRootKind(Root(), isTrusted: true).Should().Be("installed");
    }

    [Fact]
    public void Root_NotTrusted_KindIsUninstalled()
    {
        CertificateStatus.DeriveRootKind(Root(), isTrusted: false).Should().Be("uninstalled");
    }

    [Fact]
    public void Root_DerivingChildKind_Throws()
    {
        var act = () => CertificateStatus.DeriveChildKind(Root(), Array.Empty<StoredCertificate>(), Now);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Child_WithMissingIssuer_IsOrphaned()
    {
        var child = Child("c1", issuerId: "missing", expires: Now.AddYears(1));
        CertificateStatus.DeriveChildKind(child, new[] { child }, Now).Should().Be("orphaned");
    }

    [Fact]
    public void Child_WithNullIssuer_IsOrphaned()
    {
        var child = Child("c1", issuerId: null, expires: Now.AddYears(1));
        CertificateStatus.DeriveChildKind(child, new[] { child }, Now).Should().Be("orphaned");
    }

    [Fact]
    public void Child_PastExpiry_IsExpired()
    {
        var root = Root("r1");
        var child = Child("c1", "r1", expires: Now.AddDays(-1));
        CertificateStatus.DeriveChildKind(child, new[] { root, child }, Now).Should().Be("expired");
    }

    [Fact]
    public void Child_WithinExpiringWindow_IsExpiring()
    {
        var root = Root("r1");
        var child = Child("c1", "r1", expires: Now.AddDays(15));
        CertificateStatus.DeriveChildKind(child, new[] { root, child }, Now).Should().Be("expiring");
    }

    [Fact]
    public void Child_WellFuture_IsValid()
    {
        var root = Root("r1");
        var child = Child("c1", "r1", expires: Now.AddYears(1));
        CertificateStatus.DeriveChildKind(child, new[] { root, child }, Now).Should().Be("valid");
    }
}
