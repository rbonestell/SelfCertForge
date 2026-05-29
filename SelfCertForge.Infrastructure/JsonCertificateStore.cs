using System.Text.Json;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.Infrastructure;

public sealed class JsonCertificateStore : ICertificateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Marker segment for the per-certificate output tree (<c>&lt;dataDir&gt;/certificates/&lt;id&gt;/…</c>).
    /// Stored paths are persisted relative to the data folder starting at this segment, so the store
    /// survives a data-folder relocation and heals absolute paths left behind by an older build.
    /// </summary>
    private const string CertificatesFolderName = "certificates";

    private readonly string _dataDirectory;
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private List<StoredCertificate>? _items;

    public JsonCertificateStore(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _dataDirectory = Path.GetFullPath(dataDirectory);
        _filePath = Path.Combine(_dataDirectory, "certificates.json");
    }

    public IReadOnlyList<StoredCertificate> All => _items ?? (IReadOnlyList<StoredCertificate>)Array.Empty<StoredCertificate>();

    public event EventHandler? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var (items, needsRewrite) = await ReadFromDiskAsync(ct).ConfigureAwait(false);
            _items = items;

            // The file held absolute or otherwise non-canonical paths (e.g. data migrated from a
            // previous location). Heal it on disk now. Best-effort: in-memory paths are already
            // resolved, so a write failure must not break load.
            if (needsRewrite)
            {
                try
                {
                    await WriteToDiskAsync(_items, ct).ConfigureAwait(false);
                }
                catch
                {
                    // ignored — the relocation repair is opportunistic.
                }
            }
        }
        finally
        {
            _gate.Release();
        }
        RaiseChanged();
    }

    public Task AddAsync(StoredCertificate cert, CancellationToken ct = default) =>
        MutateAsync(items =>
        {
            items.RemoveAll(c => c.Id == cert.Id);
            items.Add(cert);
        }, ct);

    public Task UpdateAsync(StoredCertificate cert, CancellationToken ct = default) =>
        MutateAsync(items =>
        {
            var idx = items.FindIndex(c => c.Id == cert.Id);
            if (idx >= 0) items[idx] = cert;
            else items.Add(cert);
        }, ct);

    public Task RemoveAsync(string id, CancellationToken ct = default) =>
        MutateAsync(items => items.RemoveAll(c => c.Id == id), ct);

    private async Task MutateAsync(Action<List<StoredCertificate>> mutation, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _items ??= (await ReadFromDiskAsync(ct).ConfigureAwait(false)).Items;
            mutation(_items);
            await WriteToDiskAsync(_items, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
        RaiseChanged();
    }

    private async Task<(List<StoredCertificate> Items, bool NeedsRewrite)> ReadFromDiskAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath)) return (new List<StoredCertificate>(), false);
        await using var stream = File.OpenRead(_filePath);
        var loaded = await JsonSerializer.DeserializeAsync<List<StoredCertificate>>(stream, JsonOptions, ct)
            .ConfigureAwait(false) ?? new List<StoredCertificate>();

        var needsRewrite = loaded.Any(c => !IsCanonicalRelative(c));
        var items = loaded.Select(ToAbsolutePaths).ToList();
        return (items, needsRewrite);
    }

    private async Task WriteToDiskAsync(List<StoredCertificate> items, CancellationToken ct)
    {
        // Persist paths relative to the data folder so the file is portable across relocations.
        var relative = items.Select(ToRelativePaths).ToList();
        var tmp = _filePath + ".tmp";
        await using (var stream = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(stream, relative, JsonOptions, ct).ConfigureAwait(false);
        }
        File.Move(tmp, _filePath, overwrite: true);
    }

    private StoredCertificate ToAbsolutePaths(StoredCertificate c) => c with
    {
        CertificatePath = ToAbsolute(c.CertificatePath),
        PrivateKeyPath = ToAbsolute(c.PrivateKeyPath),
        OutputDirectory = ToAbsolute(c.OutputDirectory),
    };

    private StoredCertificate ToRelativePaths(StoredCertificate c) => c with
    {
        CertificatePath = ToRelative(c.CertificatePath),
        PrivateKeyPath = ToRelative(c.PrivateKeyPath),
        OutputDirectory = ToRelative(c.OutputDirectory),
    };

    private bool IsCanonicalRelative(StoredCertificate c) =>
        c.CertificatePath == ToRelative(c.CertificatePath) &&
        c.PrivateKeyPath == ToRelative(c.PrivateKeyPath) &&
        c.OutputDirectory == ToRelative(c.OutputDirectory);

    /// <summary>Resolve a stored (relative or legacy-absolute) path to an absolute path under the current data folder.</summary>
    private string? ToAbsolute(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return stored;
        return Path.GetFullPath(Path.Combine(_dataDirectory, ToRelative(stored)!));
    }

    /// <summary>
    /// Canonical on-disk form: the path tail beginning at the last <c>certificates</c> segment, using
    /// forward slashes. Handles relative paths, current-absolute paths, and absolute paths from a prior
    /// data-folder location uniformly. Paths that don't fit the layout fall back to a data-folder-relative
    /// form, or are left untouched when they live outside the data folder entirely.
    /// </summary>
    private string? ToRelative(string? path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        var segments = path.Split('/', '\\');
        var idx = Array.LastIndexOf(segments, CertificatesFolderName);
        if (idx >= 0)
            return string.Join('/', segments[idx..]);

        if (Path.IsPathRooted(path))
        {
            var rel = Path.GetRelativePath(_dataDirectory, Path.GetFullPath(path));
            if (!rel.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(rel))
                return rel.Replace(Path.DirectorySeparatorChar, '/');
        }

        return path;
    }

    private void RaiseChanged()
    {
        if (_uiContext is not null && SynchronizationContext.Current != _uiContext)
            _uiContext.Post(_ => Changed?.Invoke(this, EventArgs.Empty), null);
        else
            Changed?.Invoke(this, EventArgs.Empty);
    }
}
