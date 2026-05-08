using System.Text.Json;
using System.Text.Json.Serialization;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.Infrastructure;

/// <summary>
/// JSON-backed user preferences store. Mirrors <see cref="JsonCertificateStore"/>'s
/// gate-and-temp-write pattern. Falls back to <see cref="UserPreferences.Default"/>
/// on missing or corrupt files so a bad disk state never blocks app launch.
/// </summary>
public sealed class JsonUserPreferencesStore : IUserPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private UserPreferences _current = UserPreferences.Default;

    public JsonUserPreferencesStore(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "preferences.json");
    }

    public UserPreferences Current => _current;

    public event EventHandler<UserPreferences>? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _current = await ReadFromDiskAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
        RaiseChanged(_current);
    }

    public async Task SaveAsync(UserPreferences prefs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prefs);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await WriteToDiskAsync(prefs, ct).ConfigureAwait(false);
            _current = prefs;
        }
        finally
        {
            _gate.Release();
        }
        RaiseChanged(prefs);
    }

    private async Task<UserPreferences> ReadFromDiskAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath)) return UserPreferences.Default;
        try
        {
            await using var stream = File.OpenRead(_filePath);
            var loaded = await JsonSerializer.DeserializeAsync<UserPreferences>(stream, JsonOptions, ct)
                .ConfigureAwait(false);
            return loaded ?? UserPreferences.Default;
        }
        catch
        {
            // Corrupt file — return defaults rather than crash on launch.
            return UserPreferences.Default;
        }
    }

    private async Task WriteToDiskAsync(UserPreferences prefs, CancellationToken ct)
    {
        var tmp = _filePath + ".tmp";
        await using (var stream = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(stream, prefs, JsonOptions, ct).ConfigureAwait(false);
        }
        File.Move(tmp, _filePath, overwrite: true);
    }

    private void RaiseChanged(UserPreferences prefs)
    {
        if (_uiContext is not null && SynchronizationContext.Current != _uiContext)
            _uiContext.Post(_ => Changed?.Invoke(this, prefs), null);
        else
            Changed?.Invoke(this, prefs);
    }
}
