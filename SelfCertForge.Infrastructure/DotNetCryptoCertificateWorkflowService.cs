using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Validation;

namespace SelfCertForge.Infrastructure;

/// <summary>
/// Certificate workflow service implemented entirely with .NET's built-in
/// System.Security.Cryptography APIs — no external openssl binary required.
/// </summary>
public sealed class DotNetCryptoCertificateWorkflowService : ICertificateWorkflowService
{
    public Task<CertificateGenerationResult> GenerateRootCertificateAsync(
        RootCertificateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
            throw new InvalidOperationException("Output directory is required.");

        var rootName = RequireSafeToken(request.RootName, "Root name");
        Directory.CreateDirectory(request.OutputDirectory);

        using var key = RSA.Create(request.KeySizeBits);

        var req = new CertificateRequest(
            request.SubjectDn,
            key,
            ToHashName(request.HashAlgorithm),
            RSASignaturePadding.Pkcs1);

        req.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        req.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(req.PublicKey, critical: false));
        req.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));

        var notBefore = DateTimeOffset.UtcNow.AddSeconds(-5);
        var notAfter = notBefore.AddDays(request.ValidForDays);
        using var cert = req.CreateSelfSigned(notBefore, notAfter);

        var keyPath = Path.Combine(request.OutputDirectory, $"{rootName}.key");
        var pemPath = Path.Combine(request.OutputDirectory, $"{rootName}.pem");

        File.WriteAllText(keyPath, key.ExportRSAPrivateKeyPem());
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
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
        SignedCertificateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.CertificateName))
            throw new InvalidOperationException("Certificate name is required.");
        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
            throw new InvalidOperationException("Output directory is required.");

        var certName = RequireSafeToken(request.CertificateName, "Certificate name");
        ValidateSourceContract(request);
        var outputDir = Path.Combine(request.OutputDirectory, certName);
        Directory.CreateDirectory(outputDir);

        var (rootCertPem, rootKeyPem) = LoadRootMaterial(request);

        using var rootCert = X509Certificate2.CreateFromPem(rootCertPem);
        using var rootKey = RSA.Create();
        rootKey.ImportFromPem(rootKeyPem);
        using var rootCertWithKey = rootCert.CopyWithPrivateKey(rootKey);

        var keyPath = Path.Combine(outputDir, $"{certName}.key");
        var reusingKey = request.ReuseExistingDeviceKey && File.Exists(keyPath);
        using var leafKey = reusingKey
            ? CreateLeafKeyFromExisting(keyPath)
            : RSA.Create(request.KeySizeBits);

        var req = new CertificateRequest(
            request.SubjectDn,
            leafKey,
            ToHashName(request.HashAlgorithm),
            RSASignaturePadding.Pkcs1);

        req.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        req.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(req.PublicKey, critical: false));
        req.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(rootCert, includeKeyIdentifier: true, includeIssuerAndSerial: false));
        var kuFlags = BuildKeyUsageFlags(request);
        if (kuFlags != X509KeyUsageFlags.None)
            req.CertificateExtensions.Add(new X509KeyUsageExtension(kuFlags, critical: true));

        var ekuOids = new OidCollection();
        if (request.EkuServerAuth) ekuOids.Add(new Oid("1.3.6.1.5.5.7.3.1"));
        if (request.EkuClientAuth) ekuOids.Add(new Oid("1.3.6.1.5.5.7.3.2"));
        if (ekuOids.Count > 0)
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(ekuOids, critical: false));

        if (request.SubjectAlternativeNames.Count > 0)
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            foreach (var san in request.SubjectAlternativeNames)
            {
                if (san.StartsWith("IP:", StringComparison.OrdinalIgnoreCase))
                {
                    if (System.Net.IPAddress.TryParse(san[3..], out var ip))
                        sanBuilder.AddIpAddress(ip);
                }
                else
                {
                    var dnsValue = san.StartsWith("DNS:", StringComparison.OrdinalIgnoreCase)
                        ? san[4..] : san;
                    sanBuilder.AddDnsName(dnsValue);
                }
            }
            req.CertificateExtensions.Add(sanBuilder.Build());
        }

        var serial = new byte[16];
        RandomNumberGenerator.Fill(serial);

        var notBefore = DateTimeOffset.UtcNow.AddSeconds(-5);
        var notAfter = notBefore.AddDays(request.ValidForDays);

        using var cert = req.Create(rootCertWithKey, notBefore, notAfter, serial);

        var pemPath = Path.Combine(outputDir, $"{certName}.pem");
        var crtPath = Path.Combine(outputDir, $"{certName}.crt");

        if (!reusingKey)
        {
            File.WriteAllText(keyPath, leafKey.ExportRSAPrivateKeyPem());
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        File.WriteAllText(pemPath, cert.ExportCertificatePem());
        File.WriteAllText(crtPath, cert.ExportCertificatePem());

        var generatedFiles = new List<string> { pemPath, crtPath };
        if (!reusingKey)
            generatedFiles.Insert(0, keyPath);

        return Task.FromResult(new CertificateGenerationResult
        {
            OutputDirectory = outputDir,
            GeneratedFiles = generatedFiles,
            CertPemPath = pemPath,
            KeyPath = keyPath,
        });
    }

    public Task<CsrInspectionResult> InspectCsrAsync(string csrPem, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(csrPem))
            return Task.FromResult(new CsrInspectionResult(false, null, new[] { CsrValidationError.Malformed }));

        CertificateRequest req;
        try
        {
            req = CertificateRequest.LoadSigningRequestPem(
                csrPem,
                HashAlgorithmName.SHA256,
                CertificateRequestLoadOptions.SkipSignatureValidation
                    | CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            return Task.FromResult(new CsrInspectionResult(false, null, new[] { CsrValidationError.Malformed }));
        }

        var errors = new List<CsrValidationError>();

        // PoP signature
        try
        {
            _ = CertificateRequest.LoadSigningRequestPem(
                csrPem,
                HashAlgorithmName.SHA256,
                CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions);
        }
        catch (CryptographicException)
        {
            errors.Add(CsrValidationError.InvalidProofOfPossession);
        }

        // Subject
        var subjectDn = req.SubjectName.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(subjectDn))
            errors.Add(CsrValidationError.SubjectDnEmptyOrMalformed);

        // Algorithm + key size
        string algorithm = "Unknown";
        int bits = 0;
        using (var rsa = TryGetRsaPublicKey(req))
        {
            if (rsa is not null)
            {
                algorithm = "RSA";
                bits = rsa.KeySize;
                if (bits < 2048)
                    errors.Add(CsrValidationError.KeyTooSmall);
            }
            else
            {
                errors.Add(CsrValidationError.UnsupportedKeyAlgorithm);
            }
        }

        if (errors.Count > 0)
            return Task.FromResult(new CsrInspectionResult(false, null, errors));

        var summary = BuildSummary(req, csrPem, algorithm, bits);
        return Task.FromResult(new CsrInspectionResult(true, summary, Array.Empty<CsrValidationError>()));
    }

    private static RSA? TryGetRsaPublicKey(CertificateRequest req)
    {
        try { return req.PublicKey.GetRSAPublicKey(); }
        catch (CryptographicException) { return null; }
    }

    private static CsrSummary BuildSummary(CertificateRequest req, string pem, string algorithm, int bits)
    {
        var spki = req.PublicKey.ExportSubjectPublicKeyInfo();
        var fp = Convert.ToHexString(SHA256.HashData(spki));

        IReadOnlyList<string> sans = ExtractSans(req);
        var ku = ExtractRequestedKeyUsage(req);
        var ekus = ExtractRequestedEkus(req);

        return new CsrSummary(
            SubjectDistinguishedName: req.SubjectName.Name ?? string.Empty,
            PublicKeyAlgorithm: algorithm,
            PublicKeyBits: bits,
            PublicKeyFingerprintSha256: fp,
            RawCsrPem: pem,
            RequestedSans: sans,
            RequestedKeyUsage: ku,
            RequestedEkus: ekus);
    }

    private static IReadOnlyList<string> ExtractSans(CertificateRequest req)
    {
        foreach (var ext in req.CertificateExtensions)
        {
            if (ext is X509SubjectAlternativeNameExtension san)
                return san.EnumerateDnsNames().ToArray();
        }
        return Array.Empty<string>();
    }

    private static CsrRequestedKeyUsages? ExtractRequestedKeyUsage(CertificateRequest req)
    {
        foreach (var ext in req.CertificateExtensions)
        {
            if (ext is X509KeyUsageExtension ku)
            {
                var f = ku.KeyUsages;
                return new CsrRequestedKeyUsages(
                    DigitalSignature: f.HasFlag(X509KeyUsageFlags.DigitalSignature),
                    NonRepudiation:   f.HasFlag(X509KeyUsageFlags.NonRepudiation),
                    KeyEncipherment:  f.HasFlag(X509KeyUsageFlags.KeyEncipherment),
                    DataEncipherment: f.HasFlag(X509KeyUsageFlags.DataEncipherment),
                    KeyAgreement:     f.HasFlag(X509KeyUsageFlags.KeyAgreement),
                    KeyCertSign:      f.HasFlag(X509KeyUsageFlags.KeyCertSign),
                    CrlSign:          f.HasFlag(X509KeyUsageFlags.CrlSign));
            }
        }
        return null;
    }

    private static CsrRequestedEkus? ExtractRequestedEkus(CertificateRequest req)
    {
        foreach (var ext in req.CertificateExtensions)
        {
            if (ext is X509EnhancedKeyUsageExtension eku)
            {
                bool s = false, c = false, cs = false, e = false, t = false;
                foreach (var oid in eku.EnhancedKeyUsages)
                {
                    switch (oid.Value)
                    {
                        case "1.3.6.1.5.5.7.3.1": s = true; break;
                        case "1.3.6.1.5.5.7.3.2": c = true; break;
                        case "1.3.6.1.5.5.7.3.3": cs = true; break;
                        case "1.3.6.1.5.5.7.3.4": e = true; break;
                        case "1.3.6.1.5.5.7.3.8": t = true; break;
                    }
                }
                return new CsrRequestedEkus(s, c, cs, e, t);
            }
        }
        return null;
    }

    public Task<CertificateGenerationResult> GenerateCertificateFromCsrAsync(
        CsrSigningRequest request,
        string issuerCertificatePath,
        string issuerPrivateKeyPath,
        string outputDirectory,
        string outputFileBaseName,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Implemented in Task 5.");

    private static void ValidateSourceContract(SignedCertificateRequest request)
    {
        var hasSeparateFields = HasValue(request.RootPrivateKeyPath) || HasValue(request.RootCertificatePath);
        var hasPfxFields = HasValue(request.RootPfxBundlePath) || HasValue(request.RootPfxPassword);

        switch (request.SourceMode)
        {
            case SignedCertificateSourceMode.SeparateFiles:
                var rootPrivateKeyPath = RequireValue(request.RootPrivateKeyPath, "Root private key path");
                RequireAllowedExtension(rootPrivateKeyPath, "Root private key path",
                    SigningSourceFileRules.AllowedSeparateModeRootKeyExtensions,
                    SigningSourceFileRules.DescribeAllowedSeparateModeRootKeyExtensions());
                if (!HasValue(request.RootCertificatePath))
                    throw new InvalidOperationException("Root certificate path is required for separate-files signing. Select an explicit root certificate file.");
                RequireAllowedExtension(request.RootCertificatePath!, "Root certificate path",
                    SigningSourceFileRules.AllowedSeparateModeRootCertificateExtensions,
                    SigningSourceFileRules.DescribeAllowedSeparateModeRootCertificateExtensions());
                if (hasPfxFields)
                    throw new InvalidOperationException("Root PFX bundle fields must not be set when source mode is SeparateFiles.");
                RequireExistingFile(rootPrivateKeyPath, "Root private key");
                RequireExistingFile(request.RootCertificatePath!, "Root certificate");
                break;

            case SignedCertificateSourceMode.PfxBundle:
                var rootPfxBundlePath = RequireValue(request.RootPfxBundlePath, "Root PFX bundle path");
                RequireValue(request.RootPfxPassword, "Root PFX password");
                RequireAllowedExtension(rootPfxBundlePath, "Root PFX bundle path",
                    SigningSourceFileRules.AllowedPfxBundleExtensions,
                    SigningSourceFileRules.DescribeAllowedPfxBundleExtensions());
                if (hasSeparateFields)
                    throw new InvalidOperationException("Root private key/certificate fields must not be set when source mode is PfxBundle.");
                RequireExistingFile(rootPfxBundlePath, "Root PFX bundle");
                break;

            case SignedCertificateSourceMode.Unknown:
            default:
                throw new InvalidOperationException("Source mode is required. Choose SeparateFiles or PfxBundle.");
        }
    }

    private static (string certPem, string keyPem) LoadRootMaterial(SignedCertificateRequest request)
    {
        if (request.SourceMode == SignedCertificateSourceMode.PfxBundle)
        {
            var pfxPath = RequireValue(request.RootPfxBundlePath, "Root PFX bundle path");
            var pfxPassword = RequireValue(request.RootPfxPassword, "Root PFX password");
            using var pfx = X509CertificateLoader.LoadPkcs12FromFile(pfxPath, pfxPassword, X509KeyStorageFlags.Exportable);
            var certPem = pfx.ExportCertificatePem();
            using var rsaKey = pfx.GetRSAPrivateKey()
                ?? throw new InvalidOperationException("The PFX bundle does not contain an RSA private key.");
            var keyPem = rsaKey.ExportRSAPrivateKeyPem();
            return (certPem, keyPem);
        }

        var certFilePath = RequireValue(request.RootCertificatePath, "Root certificate path");
        var keyFilePath = RequireValue(request.RootPrivateKeyPath, "Root private key path");

        return (File.ReadAllText(certFilePath), File.ReadAllText(keyFilePath));
    }

    private static RSA CreateLeafKeyFromExisting(string keyPath)
    {
        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(File.ReadAllText(keyPath));
            return rsa;
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }

    private static X509KeyUsageFlags BuildKeyUsageFlags(SignedCertificateRequest request)
    {
        var flags = X509KeyUsageFlags.None;
        if (request.KeyUsageDigitalSignature) flags |= X509KeyUsageFlags.DigitalSignature;
        if (request.KeyUsageNonRepudiation) flags |= X509KeyUsageFlags.NonRepudiation;
        if (request.KeyUsageKeyEncipherment) flags |= X509KeyUsageFlags.KeyEncipherment;
        if (request.KeyUsageDataEncipherment) flags |= X509KeyUsageFlags.DataEncipherment;
        if (request.KeyUsageKeyAgreement) flags |= X509KeyUsageFlags.KeyAgreement;
        if (request.KeyUsageEncipherOnly) flags |= X509KeyUsageFlags.EncipherOnly;
        if (request.KeyUsageDecipherOnly) flags |= X509KeyUsageFlags.DecipherOnly;
        return flags;
    }

    private static string RequireValue(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{fieldName} is required.");
        return value.Trim();
    }

    private static string RequireSafeToken(string? value, string fieldName)
    {
        var token = RequireValue(value, fieldName);

        if (token.Contains('/') || token.Contains('\\'))
            throw new InvalidOperationException(
                $"{fieldName} must be a safe file-name token. Path separators are not allowed.");
        if (token.Contains("..", StringComparison.Ordinal) || token is "." or "..")
            throw new InvalidOperationException(
                $"{fieldName} must be a safe file-name token. Dot-segments are not allowed.");
        if (token.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidOperationException(
                $"{fieldName} must be a safe file-name token. Invalid file-name characters are not allowed.");

        return token;
    }

    private static void RequireAllowedExtension(string path, string label,
        IReadOnlyCollection<string> allowedExtensions, string allowedExtensionsDescription)
    {
        var extension = Path.GetExtension(path.Trim());
        if (string.IsNullOrWhiteSpace(extension)
            || !allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{label} has an unsupported extension '{extension}'. Use one of: {allowedExtensionsDescription}.");
        }
    }

    private static void RequireExistingFile(string path, string label)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"{label} file was not found: {path}", path);
    }

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);

    private static HashAlgorithmName ToHashName(HashAlgorithmKind kind) => kind switch
    {
        HashAlgorithmKind.Sha384 => HashAlgorithmName.SHA384,
        HashAlgorithmKind.Sha512 => HashAlgorithmName.SHA512,
        _                        => HashAlgorithmName.SHA256,
    };
}
