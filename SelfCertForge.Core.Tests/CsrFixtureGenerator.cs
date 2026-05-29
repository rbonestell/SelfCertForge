using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SelfCertForge.Core.Tests;

internal static class CsrFixtureGenerator
{
    public static string ValidRsa(int bits, string subjectDn,
        IEnumerable<string>? sanDnsNames = null,
        IEnumerable<string>? sanIpAddresses = null,
        X509KeyUsageFlags? keyUsage = null,
        IEnumerable<string>? ekuOids = null)
    {
        using var rsa = RSA.Create(bits);
        var req = new CertificateRequest(subjectDn, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        if (sanDnsNames is not null || sanIpAddresses is not null)
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            if (sanDnsNames is not null)
                foreach (var dns in sanDnsNames) sanBuilder.AddDnsName(dns);
            if (sanIpAddresses is not null)
                foreach (var ip in sanIpAddresses) sanBuilder.AddIpAddress(System.Net.IPAddress.Parse(ip));
            req.CertificateExtensions.Add(sanBuilder.Build(critical: false));
        }
        if (keyUsage is not null)
            req.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage.Value, critical: false));
        if (ekuOids is not null)
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                ToOidCollection(ekuOids), critical: false));

        return req.CreateSigningRequestPem();
    }

    public static string ValidEcdsa(string subjectDn)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest(subjectDn, ecdsa, HashAlgorithmName.SHA256);
        return req.CreateSigningRequestPem();
    }

    public static string TamperedRsa(int bits, string subjectDn)
    {
        var pem = ValidRsa(bits, subjectDn);

        // Corrupt proof-of-possession by flipping one bit of the signature, working on the DECODED
        // DER rather than the base64 text. In a CertificationRequest the signature BIT STRING is the
        // final element, so the last DER byte is signature content; XOR-ing it preserves every ASN.1
        // length and the overall structure (the CSR still PARSES) while making the signature fail to
        // verify -> InvalidProofOfPossession. Editing base64 characters instead is non-deterministic:
        // when the edit lands on a padding position it changes the decoded length, corrupting the DER
        // so it no longer parses (Malformed) — the source of the historical flake.
        if (!PemEncoding.TryFind(pem, out var fields))
            throw new InvalidOperationException("Generated CSR was not valid PEM.");

        var base64 = pem[fields.Base64Data].Replace("\r", "").Replace("\n", "");
        var der = Convert.FromBase64String(base64);
        der[^1] ^= 0x01;

        return new string(PemEncoding.Write(pem[fields.Label], der));
    }

    public static string Truncated() =>
        "-----BEGIN CERTIFICATE REQUEST-----\nMIICijCC\n-----END CERTIFICATE REQUEST-----\n";

    public static string NotACsr() => "this is not a certificate signing request";

    private static OidCollection ToOidCollection(IEnumerable<string> values)
    {
        var oids = new OidCollection();
        foreach (var v in values) oids.Add(new Oid(v));
        return oids;
    }
}
