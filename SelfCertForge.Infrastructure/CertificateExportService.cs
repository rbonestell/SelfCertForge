using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.Infrastructure;

public sealed class CertificateExportService : ICertificateExportService
{
    private readonly ICertificateStore _store;

    public CertificateExportService(ICertificateStore store) => _store = store;

    public Task ExportKeyPemAsync(StoredCertificate cert, string outputFolder, CancellationToken ct = default)
    {
        if (cert.CertificatePath is null)
            throw new InvalidOperationException($"Certificate '{cert.CommonName}' has no stored file path.");

        Directory.CreateDirectory(outputFolder);

        var baseName = SanitizeName(cert.CommonName);

        File.WriteAllText(Path.Combine(outputFolder, $"{baseName}.pem"), File.ReadAllText(cert.CertificatePath));

        if (cert.PrivateKeyPath is not null)
        {
            var keyDest = Path.Combine(outputFolder, $"{baseName}.key");
            File.Copy(cert.PrivateKeyPath, keyDest, overwrite: true);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(keyDest, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return Task.CompletedTask;
    }

    public Task ExportPfxAsync(StoredCertificate cert, string outputFolder, string? password, CancellationToken ct = default)
    {
        if (cert.CertificatePath is null)
            throw new InvalidOperationException($"Certificate '{cert.CommonName}' has no stored file path.");
        if (cert.PrivateKeyPath is null)
            throw new InvalidOperationException($"Certificate '{cert.CommonName}' has no stored private key path.");

        Directory.CreateDirectory(outputFolder);

        var certPem = File.ReadAllText(cert.CertificatePath);
        var keyPem = File.ReadAllText(cert.PrivateKeyPath);

        using var x509 = X509Certificate2.CreateFromPem(certPem, keyPem);

        var pfxBytes = string.IsNullOrEmpty(password)
            ? x509.Export(X509ContentType.Pfx)
            : x509.Export(X509ContentType.Pfx, password);

        File.WriteAllBytes(Path.Combine(outputFolder, $"{SanitizeName(cert.CommonName)}.pfx"), pfxBytes);

        return Task.CompletedTask;
    }

    public Task ExportDerAsync(StoredCertificate cert, string outputFolder, CancellationToken ct = default)
    {
        if (cert.CertificatePath is null)
            throw new InvalidOperationException($"Certificate '{cert.CommonName}' has no stored file path.");

        Directory.CreateDirectory(outputFolder);

        var baseName = SanitizeName(cert.CommonName);

        var certPem = File.ReadAllText(cert.CertificatePath);
        using var x509 = X509Certificate2.CreateFromPem(certPem);
        File.WriteAllBytes(Path.Combine(outputFolder, $"{baseName}.der"), x509.RawData);

        if (cert.PrivateKeyPath is not null)
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(cert.PrivateKeyPath));
            var keyDest = Path.Combine(outputFolder, $"{baseName}.key");
            File.WriteAllBytes(keyDest, rsa.ExportRSAPrivateKey());
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(keyDest, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return Task.CompletedTask;
    }

    public Task ExportP7bAsync(StoredCertificate cert, string outputFolder, CancellationToken ct = default)
    {
        if (cert.CertificatePath is null)
            throw new InvalidOperationException($"Certificate '{cert.CommonName}' has no stored file path.");

        Directory.CreateDirectory(outputFolder);

        var baseName = SanitizeName(cert.CommonName);

        var collection = new X509Certificate2Collection();
        using var leafCert = X509Certificate2.CreateFromPem(File.ReadAllText(cert.CertificatePath));
        collection.Add(leafCert);

        if (cert.IssuerId is not null)
        {
            var issuer = _store.All.FirstOrDefault(c => c.Id == cert.IssuerId);
            if (issuer?.CertificatePath is not null)
            {
                using var issuerCert = X509Certificate2.CreateFromPem(File.ReadAllText(issuer.CertificatePath));
                collection.Add(issuerCert);
            }
        }

        var p7bBytes = collection.Export(X509ContentType.Pkcs7)
            ?? throw new InvalidOperationException("Failed to export certificate chain as PKCS#7.");
        File.WriteAllBytes(Path.Combine(outputFolder, $"{baseName}.p7b"), p7bBytes);

        if (cert.PrivateKeyPath is not null)
        {
            var keyDest = Path.Combine(outputFolder, $"{baseName}.key");
            File.Copy(cert.PrivateKeyPath, keyDest, overwrite: true);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(keyDest, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return Task.CompletedTask;
    }

    private static string SanitizeName(string name) =>
        Regex.Replace(name, @"[^\w\-.]", "_").Trim('_');
}
