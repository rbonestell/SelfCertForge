using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Infrastructure;

namespace SelfCertForge.Core.Tests;

public sealed class JsonActivityLogRetentionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(),
        "selfcertforge-retentiontests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static ActivityEntry Entry(string id, DateTimeOffset at) =>
        new(id, at, "test", "msg", CertificateId: null);

    [Fact]
    public async Task MaxEntries_HonoredOnAppend_TrimsOldest()
    {
        var log = new JsonActivityLog(_dir, maxEntries: 100);
        var t = DateTimeOffset.UtcNow;
        for (var i = 0; i < 150; i++)
            await log.AppendAsync(Entry($"e{i}", t.AddMinutes(i)));

        log.Recent.Count.Should().Be(100);
        log.Recent.Should().NotContain(e => e.Id == "e0");
        log.Recent.First().Id.Should().Be("e149");
    }

    [Fact]
    public async Task MaxEntries_NegativeMeansUnlimited()
    {
        var log = new JsonActivityLog(_dir, maxEntries: -1);
        var t = DateTimeOffset.UtcNow;
        for (var i = 0; i < 1200; i++)
            await log.AppendAsync(Entry($"e{i}", t.AddMinutes(i)));

        log.Recent.Count.Should().Be(1200);
    }

    [Fact]
    public async Task ClearAsync_EmptiesRecent_AndPersists()
    {
        var log = new JsonActivityLog(_dir, maxEntries: 500);
        await log.AppendAsync(Entry("a", DateTimeOffset.UtcNow));
        await log.AppendAsync(Entry("b", DateTimeOffset.UtcNow.AddSeconds(1)));

        await log.ClearAsync();

        log.Recent.Should().BeEmpty();

        var reopened = new JsonActivityLog(_dir);
        await reopened.LoadAsync();
        reopened.Recent.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearAsync_FiresChangedEvent()
    {
        // Construct on a worker thread so the captured SynchronizationContext is null
        // and Changed fires synchronously — same approach as the existing Append test.
        var log = await Task.Run(() => new JsonActivityLog(_dir));
        await log.AppendAsync(Entry("a", DateTimeOffset.UtcNow));
        var fired = 0;
        log.Changed += (_, _) => fired++;

        await log.ClearAsync();

        fired.Should().Be(1);
    }

    [Fact]
    public async Task PreferencesCtor_ReactsToRetentionChange()
    {
        var prefs = new FakePrefsStore(UserPreferences.Default with { ActivityRetention = ActivityRetention.OneHundred });
        var log = new JsonActivityLog(_dir, prefs);

        var t = DateTimeOffset.UtcNow;
        for (var i = 0; i < 120; i++)
            await log.AppendAsync(Entry($"e{i}", t.AddMinutes(i)));

        log.Recent.Count.Should().Be(100);

        // Bump retention; the log should respect the new cap on the next append.
        prefs.RaiseChanged(UserPreferences.Default with { ActivityRetention = ActivityRetention.OneThousand });

        for (var i = 120; i < 600; i++)
            await log.AppendAsync(Entry($"e{i}", t.AddMinutes(i)));

        log.Recent.Count.Should().Be(580); // 100 from before + 480 new, all under the new 1000 cap
    }

    private sealed class FakePrefsStore : IUserPreferencesStore
    {
        public FakePrefsStore(UserPreferences current) { Current = current; }
        public UserPreferences Current { get; private set; }
        public event EventHandler<UserPreferences>? Changed;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAsync(UserPreferences prefs, CancellationToken ct = default)
        {
            Current = prefs;
            Changed?.Invoke(this, prefs);
            return Task.CompletedTask;
        }
        public void RaiseChanged(UserPreferences prefs)
        {
            Current = prefs;
            Changed?.Invoke(this, prefs);
        }
    }
}
