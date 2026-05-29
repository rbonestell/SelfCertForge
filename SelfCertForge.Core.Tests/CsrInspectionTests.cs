using FluentAssertions;
using SelfCertForge.Core.Models;
using SelfCertForge.Infrastructure;
using Xunit;

namespace SelfCertForge.Core.Tests;

public sealed class CsrInspectionTests
{
    private static readonly DotNetCryptoCertificateWorkflowService Svc = new();

    [Fact]
    public async Task ValidRsa2048_returns_IsValid_with_summary()
    {
        var pem = CsrFixtureGenerator.ValidRsa(2048, "CN=example.local");
        var result = await Svc.InspectCsrAsync(pem);

        result.IsValid.Should().BeTrue();
        result.Summary.Should().NotBeNull();
        result.Summary!.PublicKeyAlgorithm.Should().Be("RSA");
        result.Summary.PublicKeyBits.Should().Be(2048);
        result.Summary.SubjectDistinguishedName.Should().Contain("CN=example.local");
        result.Errors.Should().BeEmpty();
        result.Summary.PublicKeyFingerprintSha256.Should().MatchRegex("^[0-9A-F]{64}$");
    }

    [Fact]
    public async Task ValidRsa4096_returns_IsValid_with_4096_bits()
    {
        var pem = CsrFixtureGenerator.ValidRsa(4096, "CN=large-key.example");
        var result = await Svc.InspectCsrAsync(pem);

        result.IsValid.Should().BeTrue();
        result.Summary!.PublicKeyAlgorithm.Should().Be("RSA");
        result.Summary.PublicKeyBits.Should().Be(4096);
    }

    [Fact]
    public async Task ValidRsa_with_ip_sans_includes_ips_in_RequestedSans()
    {
        var pem = CsrFixtureGenerator.ValidRsa(2048, "CN=example.local",
            sanDnsNames: new[] { "example.local" },
            sanIpAddresses: new[] { "10.0.0.1" });
        var result = await Svc.InspectCsrAsync(pem);

        result.IsValid.Should().BeTrue();
        result.Summary!.RequestedSans.Should().Contain("example.local");
        result.Summary.RequestedSans.Should().Contain("IP:10.0.0.1");
    }

    [Fact]
    public async Task Rsa1024_tampered_returns_KeyTooSmall_and_InvalidProofOfPossession()
    {
        var pem = CsrFixtureGenerator.TamperedRsa(1024, "CN=example.local");
        var result = await Svc.InspectCsrAsync(pem);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(CsrValidationError.KeyTooSmall);
        result.Errors.Should().Contain(CsrValidationError.InvalidProofOfPossession);
    }

    [Fact]
    public async Task ValidRsa_with_sans_populates_RequestedSans()
    {
        var pem = CsrFixtureGenerator.ValidRsa(2048, "CN=example.local",
            sanDnsNames: new[] { "example.local", "api.example.local" });
        var result = await Svc.InspectCsrAsync(pem);

        result.IsValid.Should().BeTrue();
        result.Summary!.RequestedSans.Should().Equal(new[] { "example.local", "api.example.local" });
    }

    [Fact]
    public async Task ValidRsa_with_ku_eku_populates_requested_extensions()
    {
        var pem = CsrFixtureGenerator.ValidRsa(2048, "CN=example.local",
            keyUsage: System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.DigitalSignature
                    | System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.KeyEncipherment,
            ekuOids: new[] { "1.3.6.1.5.5.7.3.1" /* server auth */ });
        var result = await Svc.InspectCsrAsync(pem);

        result.IsValid.Should().BeTrue();
        result.Summary!.RequestedKeyUsage.Should().NotBeNull();
        result.Summary.RequestedKeyUsage!.DigitalSignature.Should().BeTrue();
        result.Summary.RequestedKeyUsage.KeyEncipherment.Should().BeTrue();
        result.Summary.RequestedEkus.Should().NotBeNull();
        result.Summary.RequestedEkus!.ServerAuth.Should().BeTrue();
    }

    [Fact]
    public async Task Ecdsa_csr_returns_UnsupportedKeyAlgorithm()
    {
        var pem = CsrFixtureGenerator.ValidEcdsa("CN=example.local");
        var result = await Svc.InspectCsrAsync(pem);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(CsrValidationError.UnsupportedKeyAlgorithm);
    }

    [Fact]
    public async Task Rsa1024_returns_KeyTooSmall()
    {
        var pem = CsrFixtureGenerator.ValidRsa(1024, "CN=example.local");
        var result = await Svc.InspectCsrAsync(pem);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(CsrValidationError.KeyTooSmall);
    }

    [Fact]
    public async Task TamperedRsa_returns_InvalidProofOfPossession()
    {
        var pem = CsrFixtureGenerator.TamperedRsa(2048, "CN=example.local");
        var result = await Svc.InspectCsrAsync(pem);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(CsrValidationError.InvalidProofOfPossession);
    }

    [Fact]
    public async Task EmptySubject_returns_SubjectDnEmptyOrMalformed()
    {
        var pem = CsrFixtureGenerator.ValidRsa(2048, "");
        var result = await Svc.InspectCsrAsync(pem);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(CsrValidationError.SubjectDnEmptyOrMalformed);
    }

    [Fact]
    public async Task NotACsr_returns_Malformed()
    {
        var result = await Svc.InspectCsrAsync(CsrFixtureGenerator.NotACsr());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(CsrValidationError.Malformed);
    }

    [Fact]
    public async Task Truncated_returns_Malformed()
    {
        var result = await Svc.InspectCsrAsync(CsrFixtureGenerator.Truncated());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(CsrValidationError.Malformed);
    }
}
