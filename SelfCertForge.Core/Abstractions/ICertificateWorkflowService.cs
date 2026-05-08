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
}
