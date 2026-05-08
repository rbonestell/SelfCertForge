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

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private List<StoredCertificate>? _items;

    public JsonCertificateStore(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "certificates.json");
    }

    public IReadOnlyList<StoredCertificate> All => _items ?? (IReadOnlyList<StoredCertificate>)Array.Empty<StoredCertificate>();

    public event EventHandler? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _items = await ReadFromDiskAsync(ct).ConfigureAwait(false);
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
            _items ??= await ReadFromDiskAsync(ct).ConfigureAwait(false);
            mutation(_items);
            await WriteToDiskAsync(_items, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
        RaiseChanged();
    }

    private async Task<List<StoredCertificate>> ReadFromDiskAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath)) return new List<StoredCertificate>();
        await using var stream = File.OpenRead(_filePath);
        var loaded = await JsonSerializer.DeserializeAsync<List<StoredCertificate>>(stream, JsonOptions, ct)
            .ConfigureAwait(false);
        return loaded ?? new List<StoredCertificate>();
    }

    private async Task WriteToDiskAsync(List<StoredCertificate> items, CancellationToken ct)
    {
        var tmp = _filePath + ".tmp";
        await using (var stream = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(stream, items, JsonOptions, ct).ConfigureAwait(false);
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
