namespace SelfCertForge.Core.Abstractions;

/// <summary>
/// Fetches the latest published release version from the configured GitHub repo.
/// Used purely for *informational* display in Settings (showing what version is
/// available even when it equals the installed one). Velopack remains the
/// authority for actual update download/install — this service does not perform
/// signature checks and must never trigger an install based on its result.
/// </summary>
public interface IGithubReleaseService
{
    /// <summary>
    /// Latest tag from the GitHub releases API with any leading "v" stripped
    /// (e.g. "0.0.2"). Null until the first successful refresh, or if every
    /// refresh has failed since process start.
    /// </summary>
    string? LatestPublishedVersion { get; }

    /// <summary>
    /// Best-effort fetch from `/releases/latest`. Network failures, non-2xx
    /// responses, and parse errors are silently swallowed — the property
    /// stays at its previous value. Safe to call from any thread.
    /// </summary>
    Task RefreshAsync(CancellationToken ct = default);

    /// <summary>Raised after <see cref="RefreshAsync"/> changes <see cref="LatestPublishedVersion"/>.</summary>
    event EventHandler<string?>? Changed;
}
