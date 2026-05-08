using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Abstractions;

public interface IActivityLog
{
    /// <summary>Newest first.</summary>
    IReadOnlyList<ActivityEntry> Recent { get; }
    event EventHandler? Changed;

    Task LoadAsync(CancellationToken ct = default);
    Task AppendAsync(ActivityEntry entry, CancellationToken ct = default);
}
