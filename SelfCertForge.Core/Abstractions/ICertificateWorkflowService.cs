using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Abstractions;

public interface ICertificateWorkflowService
{
    Task<CertificateGenerationResult> GenerateRootCertificateAsync(
        RootCertificateRequest request,
        CancellationToken cancellationToken = default);

    Task<CertificateGenerationResult> GenerateSignedCertificateAsync(
        SignedCertificateRequest request,
        CancellationToken cancellationToken = default);

    Task<CsrInspectionResult> InspectCsrAsync(string csrPem, CancellationToken cancellationToken = default);

    Task<CertificateGenerationResult> GenerateCertificateFromCsrAsync(
        CsrSigningRequest request,
        string issuerCertificatePath,
        string issuerPrivateKeyPath,
        string outputDirectory,
        string outputFileBaseName,
        CancellationToken cancellationToken = default);
}
