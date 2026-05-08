using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Abstractions;

public interface IActivityLog
{
    /// <summary>Newest first.</summary>
    IReadOnlyList<ActivityEntry> Recent { get; }

    /// <summary>
    /// Maximum entries kept on disk. Negative = unlimited (no pruning).
    /// Setting this trims the in-memory list immediately on the next append.
    /// </summary>
    int MaxEntries { get; set; }

    event EventHandler? Changed;

    Task LoadAsync(CancellationToken ct = default);
    Task AppendAsync(ActivityEntry entry, CancellationToken ct = default);

    /// <summary>Removes every entry and persists the empty state.</summary>
    Task ClearAsync(CancellationToken ct = default);
}
