using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Abstractions;

public interface IForgeService
{
    Task<StoredCertificate> ForgeAsync(ForgeRequest request, CancellationToken ct = default);
}
