using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.Infrastructure;

public sealed class ForgeService : IForgeService
{
    private readonly ICertificateStore _store;
    private readonly IActivityLog _log;
    private readonly ICertificateWorkflowService _workflow;
    private readonly string _appDataDirectory;

    public ForgeService(
        ICertificateStore store,
        IActivityLog log,
        ICertificateWorkflowService workflow,
        string appDataDirectory)
    {
        _store = store;
        _log = log;
        _workflow = workflow;
        _appDataDirectory = appDataDirectory;
    }

    public async Task<StoredCertificate> ForgeAsync(ForgeRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CommonName);
        if (request.ValidityDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.ValidityDays));

        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid().ToString("N");
        var outputDir = Path.Combine(_appDataDirectory, "certificates", id);
        var safeName = ToSafeFileName(request.CommonName);
        var sans = request.Sans.ToList();

        StoredCertificate? issuer = null;
        if (request.Mode == ForgeMode.Child && request.IssuerId is not null)
            issuer = _store.All.FirstOrDefault(c => c.Id == request.IssuerId);

        CertificateGenerationResult result;

        if (request.Mode == ForgeMode.Root)
        {
            result = await _workflow.GenerateRootCertificateAsync(new RootCertificateRequest
            {
                OutputDirectory = outputDir,
                RootName = safeName,
                KeySizeBits = request.KeyBits,
                ValidForDays = request.ValidityDays,
                SubjectDn = BuildSubjectDn(request),
            }, ct).ConfigureAwait(false);
        }
        else
        {
            if (issuer is null)
                throw new InvalidOperationException("Issuing root authority not found.");
            if (issuer.CertificatePath is null || issuer.PrivateKeyPath is null)
                throw new InvalidOperationException(
                    "Issuing root authority has no key files on disk. It may have been created outside SelfCertForge.");

            result = await _workflow.GenerateSignedCertificateAsync(new SignedCertificateRequest
            {
                SourceMode = SignedCertificateSourceMode.SeparateFiles,
                CertificateName = safeName,
                OutputDirectory = outputDir,
                RootCertificatePath = issuer.CertificatePath,
                RootPrivateKeyPath = issuer.PrivateKeyPath,
                SubjectDn = BuildSubjectDn(request),
                SubjectAlternativeNames = sans,
                ReuseExistingDeviceKey = false,
                KeySizeBits = request.KeyBits,
                ValidForDays = request.ValidityDays,
                KeyUsageDigitalSignature = request.KeyUsageDigitalSignature,
                KeyUsageNonRepudiation = request.KeyUsageNonRepudiation,
                KeyUsageKeyEncipherment = request.KeyUsageKeyEncipherment,
                KeyUsageDataEncipherment = request.KeyUsageDataEncipherment,
                KeyUsageKeyAgreement = request.KeyUsageKeyAgreement,
                KeyUsageEncipherOnly = request.KeyUsageEncipherOnly,
                KeyUsageDecipherOnly = request.KeyUsageDecipherOnly,
                EkuServerAuth = request.EkuServerAuth,
                EkuClientAuth = request.EkuClientAuth,
            }, ct).ConfigureAwait(false);
        }

        var pemPath = result.CertPemPath;
        var keyPath = result.KeyPath;

        var certPem = await File.ReadAllTextAsync(pemPath, ct).ConfigureAwait(false);
        using var x509 = X509Certificate2.CreateFromPem(certPem);

        var sha256 = FormatColonHex(x509.GetCertHash(HashAlgorithmName.SHA256));
        var sha1 = FormatColonHex(x509.GetCertHash(HashAlgorithmName.SHA1));
        var serial = FormatColonHex(x509.SerialNumberBytes.Span);
        var algorithm = x509.SignatureAlgorithm.FriendlyName is { Length: > 0 } n
            ? n
            : $"RSA {request.KeyBits}";

        var keyUsages = new List<string>();
        var ekuList = new List<string>();
        foreach (var ext in x509.Extensions)
        {
            if (ext is X509KeyUsageExtension ku)
            {
                if (ku.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))  keyUsages.Add("Digital Signature");
                if (ku.KeyUsages.HasFlag(X509KeyUsageFlags.NonRepudiation))    keyUsages.Add("Non-Repudiation");
                if (ku.KeyUsages.HasFlag(X509KeyUsageFlags.KeyEncipherment))   keyUsages.Add("Key Encipherment");
                if (ku.KeyUsages.HasFlag(X509KeyUsageFlags.DataEncipherment))  keyUsages.Add("Data Encipherment");
                if (ku.KeyUsages.HasFlag(X509KeyUsageFlags.KeyAgreement))      keyUsages.Add("Key Agreement");
                if (ku.KeyUsages.HasFlag(X509KeyUsageFlags.KeyCertSign))       keyUsages.Add("Certificate Signing");
                if (ku.KeyUsages.HasFlag(X509KeyUsageFlags.CrlSign))           keyUsages.Add("CRL Signing");
                if (ku.KeyUsages.HasFlag(X509KeyUsageFlags.EncipherOnly))      keyUsages.Add("Encipher Only");
                if (ku.KeyUsages.HasFlag(X509KeyUsageFlags.DecipherOnly))      keyUsages.Add("Decipher Only");
            }
            else if (ext is X509EnhancedKeyUsageExtension eku)
            {
                foreach (var oid in eku.EnhancedKeyUsages)
                    ekuList.Add(oid.FriendlyName is { Length: > 0 } fn ? fn : oid.Value ?? "Unknown");
            }
        }

        var stored = new StoredCertificate(
            Id: id,
            Kind: request.Mode == ForgeMode.Root ? StoredCertificateKind.Root : StoredCertificateKind.Child,
            CommonName: request.CommonName,
            Subject: x509.Subject,
            IssuerId: issuer?.Id,
            IssuerName: issuer?.CommonName,
            Sans: sans,
            Algorithm: algorithm,
            Serial: serial,
            Sha256: sha256,
            Sha1: sha1,
            IssuedAt: ToUtcOffset(x509.NotBefore),
            ExpiresAt: ToUtcOffset(x509.NotAfter),
            InstalledInTrustStore: request.Mode == ForgeMode.Root && request.InstallInTrustStore,
            CertificatePath: pemPath,
            PrivateKeyPath: keyPath,
            OutputDirectory: result.OutputDirectory,
            KeyUsages: keyUsages.Count > 0 ? keyUsages : null,
            ExtendedKeyUsages: ekuList.Count > 0 ? ekuList : null);

        await _store.AddAsync(stored, ct).ConfigureAwait(false);

        var (kind, message) = request.Mode switch
        {
            ForgeMode.Root  => ("forged-root",  $"Forged root authority \"{stored.CommonName}\"."),
            ForgeMode.Child => ("forged-child", $"Forged certificate \"{stored.CommonName}\"" +
                                                (issuer is not null ? $" issued by {issuer.CommonName}." : ".")),
            _ => ("forged", $"Forged certificate \"{stored.CommonName}\"."),
        };

        await _log.AppendAsync(new ActivityEntry(
            Id: Guid.NewGuid().ToString("N"),
            At: now,
            Kind: kind,
            Message: message,
            CertificateId: stored.Id), ct).ConfigureAwait(false);

        return stored;
    }

    private static string BuildSubjectDn(ForgeRequest r)
    {
        var parts = new List<string>();
        parts.Add($"CN={r.CommonName}");
        if (!string.IsNullOrWhiteSpace(r.EmailAddress))
            parts.Add($"E={r.EmailAddress.Trim()}");
        if (!string.IsNullOrWhiteSpace(r.Organization))
            parts.Add($"O={r.Organization.Trim()}");
        if (!string.IsNullOrWhiteSpace(r.OrganizationalUnit))
            parts.Add($"OU={r.OrganizationalUnit.Trim()}");
        if (!string.IsNullOrWhiteSpace(r.Locality))
            parts.Add($"L={r.Locality.Trim()}");
        if (!string.IsNullOrWhiteSpace(r.StateOrProvince))
            parts.Add($"ST={r.StateOrProvince.Trim()}");
        if (!string.IsNullOrWhiteSpace(r.Country))
            parts.Add($"C={r.Country.Trim().ToUpperInvariant()}");
        return string.Join(", ", parts);
    }

    private static string ToSafeFileName(string name) =>
        new string(name.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_').ToArray()).Trim('_');

    private static DateTimeOffset ToUtcOffset(DateTime dt) =>
        dt.Kind == DateTimeKind.Unspecified
            ? new DateTimeOffset(dt, TimeSpan.Zero)
            : new DateTimeOffset(dt).ToUniversalTime();

    private static string FormatColonHex(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length * 3);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (i > 0) sb.Append(':');
            sb.Append(bytes[i].ToString("X2"));
        }
        return sb.ToString();
    }
}
