using System.Text.Json;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.Infrastructure;

public sealed class JsonActivityLog : IActivityLog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private List<ActivityEntry>? _entries;
    // volatile because the prefs-Changed handler writes from the UI thread post
    // while AppendAsync reads from whichever thread the caller used; the
    // semaphore gates the list mutation but not this scalar.
    private volatile int _maxEntries;

    public JsonActivityLog(string dataDirectory)
        : this(dataDirectory, (int)ActivityRetention.FiveHundred) { }

    public JsonActivityLog(string dataDirectory, int maxEntries)
    {
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "activity.json");
        _maxEntries = maxEntries;
    }

    /// <summary>
    /// Convenience ctor that wires retention from a preferences store and reacts
    /// to live changes (the user toggling retention in Settings updates the cap
    /// without restart).
    /// </summary>
    public JsonActivityLog(string dataDirectory, IUserPreferencesStore prefs)
        : this(dataDirectory, (int)prefs.Current.ActivityRetention)
    {
        prefs.Changed += (_, p) => MaxEntries = (int)p.ActivityRetention;
    }

    public IReadOnlyList<ActivityEntry> Recent =>
        _entries is null
            ? Array.Empty<ActivityEntry>()
            : _entries.OrderByDescending(e => e.At).ToArray();

    public int MaxEntries
    {
        get => _maxEntries;
        set => _maxEntries = value;
    }

    public event EventHandler? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _entries = await ReadFromDiskAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
        RaiseChanged();
    }

    public async Task AppendAsync(ActivityEntry entry, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _entries ??= await ReadFromDiskAsync(ct).ConfigureAwait(false);
            _entries.Add(entry);
            // Cap the on-disk log to avoid unbounded growth. Keep the most recent.
            // Negative MaxEntries means "unlimited" — skip pruning entirely.
            if (_maxEntries >= 0 && _entries.Count > _maxEntries)
            {
                _entries = _entries.OrderByDescending(e => e.At).Take(_maxEntries).ToList();
            }
            await WriteToDiskAsync(_entries, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
        RaiseChanged();
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _entries = new List<ActivityEntry>();
            await WriteToDiskAsync(_entries, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
        RaiseChanged();
    }

    private async Task<List<ActivityEntry>> ReadFromDiskAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath)) return new List<ActivityEntry>();
        try
        {
            await using var stream = File.OpenRead(_filePath);
            var loaded = await JsonSerializer.DeserializeAsync<List<ActivityEntry>>(stream, JsonOptions, ct)
                .ConfigureAwait(false);
            return loaded ?? new List<ActivityEntry>();
        }
        catch
        {
            // Corrupt or 0-byte file — start fresh rather than crash the log subsystem.
            return new List<ActivityEntry>();
        }
    }

    private async Task WriteToDiskAsync(List<ActivityEntry> entries, CancellationToken ct)
    {
        var tmp = _filePath + ".tmp";
        await using (var stream = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(stream, entries, JsonOptions, ct).ConfigureAwait(false);
        }
        File.Move(tmp, _filePath, overwrite: true);
    }

    private void RaiseChanged()
    {
        if (_uiContext is not null && SynchronizationContext.Current != _uiContext)
            _uiContext.Post(_ => Changed?.Invoke(this, EventArgs.Empty), null);
        else
            Changed?.Invoke(this, EventArgs.Empty);
    }
}
