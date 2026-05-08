using SelfCertForge.Core.Models;
using SelfCertForge.Infrastructure;

namespace SelfCertForge.Core.Tests;

public sealed class JsonCertificateStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(),
        "selfcertforge-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private Task<JsonCertificateStore> MakeStoreAsync() =>
        Task.Run(() => new JsonCertificateStore(_dir));

    private static StoredCertificate SampleRoot(string id = "r1") => new(
        Id: id,
        Kind: StoredCertificateKind.Root,
        CommonName: "Self-Signed Root Authority",
        Subject: "CN=Self-Signed Root Authority,O=SelfCertForge",
        IssuerId: null,
        IssuerName: null,
        Sans: Array.Empty<string>(),
        Algorithm: "RSA 4096",
        Serial: "0A:3F:21",
        Sha256: "AA:BB",
        Sha1: "CC:DD",
        IssuedAt: new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        ExpiresAt: new DateTimeOffset(2050, 1, 1, 0, 0, 0, TimeSpan.Zero),
        InstalledInTrustStore: true);

    [Fact]
    public async Task EmptyDirectory_StartsWithNoCertificates()
    {
        var store = new JsonCertificateStore(_dir);
        await store.LoadAsync();
        store.All.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_PersistsAcrossInstances()
    {
        var first = new JsonCertificateStore(_dir);
        await first.AddAsync(SampleRoot());

        var second = new JsonCertificateStore(_dir);
        await second.LoadAsync();

        second.All.Should().ContainSingle()
            .Which.Id.Should().Be("r1");
    }

    [Fact]
    public async Task Update_ReplacesExistingById()
    {
        var store = new JsonCertificateStore(_dir);
        await store.AddAsync(SampleRoot("r1") with { CommonName = "old" });
        await store.UpdateAsync(SampleRoot("r1") with { CommonName = "new" });

        store.All.Should().ContainSingle()
            .Which.CommonName.Should().Be("new");
    }

    [Fact]
    public async Task Remove_DropsTheRecord()
    {
        var store = new JsonCertificateStore(_dir);
        await store.AddAsync(SampleRoot("r1"));
        await store.AddAsync(SampleRoot("r2"));
        await store.RemoveAsync("r1");

        store.All.Should().ContainSingle()
            .Which.Id.Should().Be("r2");
    }

    [Fact]
    public async Task Add_FiresChangedEvent()
    {
        var store = new JsonCertificateStore(_dir);
        var fired = 0;
        store.Changed += (_, _) => fired++;
        await store.AddAsync(SampleRoot());
        fired.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_FiresChangedEvent()
    {
        var store = await MakeStoreAsync();
        var fired = 0;
        store.Changed += (_, _) => fired++;
        await store.UpdateAsync(SampleRoot("r1"));
        fired.Should().Be(1);
    }

    [Fact]
    public async Task RemoveAsync_FiresChangedEvent()
    {
        var store = await MakeStoreAsync();
        var fired = 0;
        store.Changed += (_, _) => fired++;
        await store.AddAsync(SampleRoot("r1"));
        var firedBeforeRemove = fired;
        await store.RemoveAsync("r1");
        fired.Should().Be(firedBeforeRemove + 1);
    }

    [Fact]
    public async Task LoadAsync_FiresChangedEvent()
    {
        var store = await MakeStoreAsync();
        var fired = 0;
        store.Changed += (_, _) => fired++;
        await store.LoadAsync();
        fired.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_UpsertsByCertId()
    {
        var store = new JsonCertificateStore(_dir);
        await store.UpdateAsync(SampleRoot("r99") with { CommonName = "upserted" });

        store.All.Should().ContainSingle()
            .Which.Id.Should().Be("r99");
    }

    [Fact]
    public async Task RemoveAsync_IsNoOp_WhenIdNotFound()
    {
        var store = new JsonCertificateStore(_dir);
        await store.AddAsync(SampleRoot("r1"));

        Func<Task> act = () => store.RemoveAsync("does-not-exist");

        await act.Should().NotThrowAsync();
        store.All.Should().ContainSingle()
            .Which.Id.Should().Be("r1");
    }

    [Fact]
    public async Task AddAsync_DeduplicatesById()
    {
        var store = new JsonCertificateStore(_dir);
        await store.AddAsync(SampleRoot("r1") with { CommonName = "first" });
        await store.AddAsync(SampleRoot("r1") with { CommonName = "second" });

        store.All.Should().ContainSingle()
            .Which.CommonName.Should().Be("second");
    }
}
