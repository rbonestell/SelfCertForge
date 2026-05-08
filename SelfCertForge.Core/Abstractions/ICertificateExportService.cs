using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Abstractions;

public interface ICertificateExportService
{
    /// <summary>Exports the certificate as .pem (PEM cert) and .key (PEM private key) into outputFolder.</summary>
    Task ExportKeyPemAsync(StoredCertificate cert, string outputFolder, CancellationToken ct = default);

    /// <summary>Exports the certificate as a PKCS#12 (.pfx) bundle into outputFolder with optional password protection.</summary>
    Task ExportPfxAsync(StoredCertificate cert, string outputFolder, string? password, CancellationToken ct = default);

    /// <summary>Exports the certificate as DER (.der) and private key as PKCS#8 DER (.key) into outputFolder.</summary>
    Task ExportDerAsync(StoredCertificate cert, string outputFolder, CancellationToken ct = default);

    /// <summary>Exports the certificate chain as a PKCS#7 (.p7b) bundle and private key as PEM (.key) into outputFolder.</summary>
    Task ExportP7bAsync(StoredCertificate cert, string outputFolder, CancellationToken ct = default);
}
