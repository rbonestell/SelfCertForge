using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SelfCertForge.Core.Tests;

internal static class CsrFixtureGenerator
{
    public static string ValidRsa(int bits, string subjectDn,
        IEnumerable<string>? sanDnsNames = null,
        X509KeyUsageFlags? keyUsage = null,
        IEnumerable<string>? ekuOids = null)
    {
        using var rsa = RSA.Create(bits);
        var req = new CertificateRequest(subjectDn, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        if (sanDnsNames is not null)
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            foreach (var dns in sanDnsNames) sanBuilder.AddDnsName(dns);
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
        // Flip a char in the last body line (near the signature region) to corrupt proof-of-possession.
        var lines = pem.Split('\n');
        var bodyLine = Array.FindLastIndex(lines, l => !l.StartsWith("-----") && !string.IsNullOrWhiteSpace(l));
        var line = lines[bodyLine];
        lines[bodyLine] = line[..^2] + (line[^2] == 'A' ? "B" : "A") + line[^1];
        return string.Join('\n', lines);
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
