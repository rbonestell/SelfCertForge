using SelfCertForge.Core.Models;
using SelfCertForge.Infrastructure;

namespace SelfCertForge.Core.Tests;

public sealed class JsonActivityLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(),
        "selfcertforge-activitytests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private Task<JsonActivityLog> MakeLogAsync() =>
        Task.Run(() => new JsonActivityLog(_dir));

    private static ActivityEntry Entry(string id, DateTimeOffset at, string message = "msg") =>
        new(id, at, "test", message, CertificateId: null);

    [Fact]
    public async Task EmptyDirectory_StartsEmpty()
    {
        var log = new JsonActivityLog(_dir);
        await log.LoadAsync();
        log.Recent.Should().BeEmpty();
    }

    [Fact]
    public async Task Recent_OrdersNewestFirst()
    {
        var log = new JsonActivityLog(_dir);
        var t = DateTimeOffset.UtcNow;
        await log.AppendAsync(Entry("a", t));
        await log.AppendAsync(Entry("b", t.AddMinutes(1)));
        await log.AppendAsync(Entry("c", t.AddMinutes(-1)));

        log.Recent.Select(e => e.Id).Should().Equal("b", "a", "c");
    }

    [Fact]
    public async Task Append_PersistsAcrossInstances()
    {
        var first = new JsonActivityLog(_dir);
        await first.AppendAsync(Entry("a", DateTimeOffset.UtcNow));

        var second = new JsonActivityLog(_dir);
        await second.LoadAsync();

        second.Recent.Should().ContainSingle()
            .Which.Id.Should().Be("a");
    }

    [Fact]
    public async Task Append_FiresChangedEvent()
    {
        var log = await MakeLogAsync();
        var fired = 0;
        log.Changed += (_, _) => fired++;
        await log.AppendAsync(Entry("a", DateTimeOffset.UtcNow));
        fired.Should().Be(1);
    }

    [Fact]
    public async Task Append_CapsAt500_KeepsMostRecent()
    {
        var log = new JsonActivityLog(_dir);
        var baseTime = DateTimeOffset.UtcNow;
        for (var i = 0; i < 501; i++)
        {
            await log.AppendAsync(Entry($"e{i}", baseTime.AddMinutes(i)));
        }

        log.Recent.Count.Should().Be(500);
        log.Recent.Should().NotContain(e => e.Id == "e0");
    }

    [Fact]
    public async Task LoadAsync_FiresChangedEvent()
    {
        var log = await MakeLogAsync();
        var fired = 0;
        log.Changed += (_, _) => fired++;
        await log.LoadAsync();
        fired.Should().Be(1);
    }

    [Fact]
    public async Task AppendAsync_WithoutLoad_LazyLoads()
    {
        var log = new JsonActivityLog(_dir);
        var t = DateTimeOffset.UtcNow;
        await log.AppendAsync(Entry("x1", t));
        await log.AppendAsync(Entry("x2", t.AddMinutes(1)));

        log.Recent.Count.Should().Be(2);
    }
}
