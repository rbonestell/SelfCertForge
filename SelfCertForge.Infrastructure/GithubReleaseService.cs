using System.Text.Json;
using SelfCertForge.Core.Abstractions;

namespace SelfCertForge.Infrastructure;

/// <summary>
/// Hits GitHub's `GET /repos/{owner}/{repo}/releases/latest` endpoint to
/// surface the most recent published version, regardless of whether it
/// matches the installed version. See <see cref="IGithubReleaseService"/>
/// for the informational-only contract.
/// </summary>
public sealed class GithubReleaseService : IGithubReleaseService
{
    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private string? _latestPublishedVersion;

    public GithubReleaseService(HttpClient http, string owner, string repo)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repo);

        _http = http;
        _endpoint = new Uri($"https://api.github.com/repos/{owner}/{repo}/releases/latest");
    }

    public string? LatestPublishedVersion => _latestPublishedVersion;

    public event EventHandler<string?>? Changed;

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, _endpoint);
            // GitHub requires a User-Agent on all API requests.
            req.Headers.UserAgent.ParseAdd("SelfCertForge");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct)
                .ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            if (!doc.RootElement.TryGetProperty("tag_name", out var tagElement)) return;
            var tag = tagElement.GetString();
            if (string.IsNullOrWhiteSpace(tag)) return;

            // Strip leading 'v' / 'V' so the displayed string matches the
            // installed-version format (which comes from AssemblyInformationalVersion).
            var version = tag.AsSpan().TrimStart('v').TrimStart('V').ToString();
            if (string.IsNullOrWhiteSpace(version)) return;

            if (_latestPublishedVersion != version)
            {
                _latestPublishedVersion = version;
                RaiseChanged(version);
            }
        }
        catch
        {
            // Best-effort: rate-limited, offline, malformed JSON, etc. — never crash.
        }
    }

    private void RaiseChanged(string? version)
    {
        if (_uiContext is not null && SynchronizationContext.Current != _uiContext)
            _uiContext.Post(_ => Changed?.Invoke(this, version), null);
        else
            Changed?.Invoke(this, version);
    }
}
