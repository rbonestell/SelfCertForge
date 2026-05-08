using System.Text.Json;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.Infrastructure;

public sealed class JsonActivityLog : IActivityLog
{
    private const int CapWhenWriting = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private List<ActivityEntry>? _entries;

    public JsonActivityLog(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "activity.json");
    }

    public IReadOnlyList<ActivityEntry> Recent =>
        _entries is null
            ? Array.Empty<ActivityEntry>()
            : _entries.OrderByDescending(e => e.At).ToArray();

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
            if (_entries.Count > CapWhenWriting)
            {
                _entries = _entries.OrderByDescending(e => e.At).Take(CapWhenWriting).ToList();
            }
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
        await using var stream = File.OpenRead(_filePath);
        var loaded = await JsonSerializer.DeserializeAsync<List<ActivityEntry>>(stream, JsonOptions, ct)
            .ConfigureAwait(false);
        return loaded ?? new List<ActivityEntry>();
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
