using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Abstractions;

public interface ICreateFromCsrDialog
{
    Task<StoredCertificate?> ShowAsync(
        string signingAuthorityId,
        string signingAuthorityName,
        CsrSummary csrSummary,
        string sourceCsrFilename,
        CancellationToken ct = default);
}
