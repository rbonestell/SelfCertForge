using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Abstractions;

public interface ICertificateStore
{
    IReadOnlyList<StoredCertificate> All { get; }
    event EventHandler? Changed;

    Task LoadAsync(CancellationToken ct = default);
    Task AddAsync(StoredCertificate cert, CancellationToken ct = default);
    Task UpdateAsync(StoredCertificate cert, CancellationToken ct = default);
    Task RemoveAsync(string id, CancellationToken ct = default);
}
