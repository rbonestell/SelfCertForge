using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Abstractions;

/// <summary>
/// Persistence abstraction for <see cref="UserPreferences"/>. Implementations
/// must raise <see cref="Changed"/> after a successful <see cref="SaveAsync"/>.
/// </summary>
public interface IUserPreferencesStore
{
    /// <summary>Snapshot of the currently-persisted preferences. Never null.</summary>
    UserPreferences Current { get; }

    /// <summary>Loads from disk if not already loaded. Safe to call multiple times.</summary>
    Task LoadAsync(CancellationToken ct = default);

    /// <summary>Persists the supplied preferences and updates <see cref="Current"/>.</summary>
    Task SaveAsync(UserPreferences prefs, CancellationToken ct = default);

    /// <summary>Raised after <see cref="SaveAsync"/> or <see cref="LoadAsync"/> succeeds.</summary>
    event EventHandler<UserPreferences>? Changed;
}
