using System.Text.Json;
using SelfCertForge.Core.Models;
using SelfCertForge.Infrastructure;

namespace SelfCertForge.Core.Tests;

/// <summary>
/// Path persistence contract for <see cref="JsonCertificateStore"/>: paths are stored
/// relative to the data folder so the store survives a data-folder relocation, and any
/// legacy/stale absolute paths are healed (and the file rewritten) on load.
/// </summary>
public sealed class JsonCertificateStorePathTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(),
        "selfcertforge-pathtests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private string JsonFile => Path.Combine(_dir, "certificates.json");

    private static StoredCertificate Leaf(string id, string? certPath, string? keyPath, string? outDir) => new(
        Id: id,
        Kind: StoredCertificateKind.Child,
        CommonName: "leaf.local",
        Subject: "CN=leaf.local",
        IssuerId: "root1",
        IssuerName: "Root",
        Sans: new[] { "leaf.local" },
        Algorithm: "RSA 2048",
        Serial: "01:02",
        Sha256: "AA",
        Sha1: "BB",
        IssuedAt: new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        ExpiresAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        InstalledInTrustStore: false,
        CertificatePath: certPath,
        PrivateKeyPath: keyPath,
        OutputDirectory: outDir);

    private static readonly JsonSerializerOptions CamelCase = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public async Task Save_StoresPathsRelativeToDataFolder_NotAbsolute()
    {
        var id = "abc123";
        var certAbs = Path.Combine(_dir, "certificates", id, "leaf.pem");
        var keyAbs = Path.Combine(_dir, "certificates", id, "leaf.key");
        var outAbs = Path.Combine(_dir, "certificates", id);

        var store = new JsonCertificateStore(_dir);
        await store.AddAsync(Leaf(id, certAbs, keyAbs, outAbs));

        var raw = await File.ReadAllTextAsync(JsonFile);

        raw.Should().Contain($"certificates/{id}/leaf.pem");
        raw.Should().Contain($"certificates/{id}/leaf.key");
        raw.Should().NotContain(_dir); // no absolute prefix leaks into the file
    }

    [Fact]
    public async Task Load_ResolvesRelativePaths_ToAbsoluteUnderDataFolder()
    {
        var id = "abc123";
        var certAbs = Path.Combine(_dir, "certificates", id, "leaf.pem");
        var keyAbs = Path.Combine(_dir, "certificates", id, "leaf.key");
        var outAbs = Path.Combine(_dir, "certificates", id);

        await new JsonCertificateStore(_dir).AddAsync(Leaf(id, certAbs, keyAbs, outAbs));

        var reloaded = new JsonCertificateStore(_dir);
        await reloaded.LoadAsync();

        var cert = reloaded.All.Should().ContainSingle().Subject;
        cert.CertificatePath.Should().Be(Path.GetFullPath(certAbs));
        cert.PrivateKeyPath.Should().Be(Path.GetFullPath(keyAbs));
        cert.OutputDirectory.Should().Be(Path.GetFullPath(outAbs));
    }

    [Fact]
    public async Task Load_IsPortable_WhenDataFolderIsRelocated()
    {
        // Write the store in _dir, then move the JSON to a *different* root and load it there.
        var id = "abc123";
        await new JsonCertificateStore(_dir).AddAsync(Leaf(
            id,
            Path.Combine(_dir, "certificates", id, "leaf.pem"),
            Path.Combine(_dir, "certificates", id, "leaf.key"),
            Path.Combine(_dir, "certificates", id)));

        var newRoot = _dir + "-relocated";
        Directory.CreateDirectory(newRoot);
        File.Copy(JsonFile, Path.Combine(newRoot, "certificates.json"));
        try
        {
            var relocated = new JsonCertificateStore(newRoot);
            await relocated.LoadAsync();

            var cert = relocated.All.Should().ContainSingle().Subject;
            cert.CertificatePath.Should().Be(Path.GetFullPath(Path.Combine(newRoot, "certificates", id, "leaf.pem")));
            cert.PrivateKeyPath.Should().Be(Path.GetFullPath(Path.Combine(newRoot, "certificates", id, "leaf.key")));
        }
        finally
        {
            Directory.Delete(newRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Load_HealsLegacyAbsolutePaths_FromAPreviousLocation()
    {
        // Simulate a file written before the relative-path fix: absolute paths that point
        // at an OLD data root (the ~/Library bug). The store must rebase them onto _dir.
        var id = "abc123";
        var staleRoot = Path.Combine(Path.GetTempPath(), "OLD-LIBRARY-ROOT");
        var legacy = new[]
        {
            Leaf(id,
                Path.Combine(staleRoot, "certificates", id, "leaf.pem"),
                Path.Combine(staleRoot, "certificates", id, "leaf.key"),
                Path.Combine(staleRoot, "certificates", id)),
        };
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(JsonFile, JsonSerializer.Serialize(legacy, CamelCase));

        var store = new JsonCertificateStore(_dir);
        await store.LoadAsync();

        var cert = store.All.Should().ContainSingle().Subject;
        cert.CertificatePath.Should().Be(Path.GetFullPath(Path.Combine(_dir, "certificates", id, "leaf.pem")));
        cert.PrivateKeyPath.Should().Be(Path.GetFullPath(Path.Combine(_dir, "certificates", id, "leaf.key")));
        cert.OutputDirectory.Should().Be(Path.GetFullPath(Path.Combine(_dir, "certificates", id)));
    }

    [Fact]
    public async Task Load_EagerlyRewritesStaleFile_ToRelativePaths()
    {
        var id = "abc123";
        var staleRoot = Path.Combine(Path.GetTempPath(), "OLD-LIBRARY-ROOT");
        var legacy = new[]
        {
            Leaf(id,
                Path.Combine(staleRoot, "certificates", id, "leaf.pem"),
                Path.Combine(staleRoot, "certificates", id, "leaf.key"),
                Path.Combine(staleRoot, "certificates", id)),
        };
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(JsonFile, JsonSerializer.Serialize(legacy, CamelCase));

        await new JsonCertificateStore(_dir).LoadAsync();

        // The on-disk file should have been repaired to relative form during load.
        var raw = await File.ReadAllTextAsync(JsonFile);
        raw.Should().Contain($"certificates/{id}/leaf.pem");
        raw.Should().NotContain(staleRoot);
        raw.Should().NotContain(_dir);
    }

    [Fact]
    public async Task Load_DoesNotRewriteFile_WhenAlreadyRelative()
    {
        var id = "abc123";
        await new JsonCertificateStore(_dir).AddAsync(Leaf(
            id,
            Path.Combine(_dir, "certificates", id, "leaf.pem"),
            Path.Combine(_dir, "certificates", id, "leaf.key"),
            Path.Combine(_dir, "certificates", id)));

        var before = await File.ReadAllTextAsync(JsonFile);
        var beforeWrite = File.GetLastWriteTimeUtc(JsonFile);

        var store = new JsonCertificateStore(_dir);
        await store.LoadAsync();

        var after = await File.ReadAllTextAsync(JsonFile);
        after.Should().Be(before);
        File.GetLastWriteTimeUtc(JsonFile).Should().Be(beforeWrite, "an already-canonical file must not be rewritten on load");
    }

    [Fact]
    public async Task NullPaths_RoundTrip_AsNull()
    {
        var store = new JsonCertificateStore(_dir);
        await store.AddAsync(Leaf("noPaths", certPath: null, keyPath: null, outDir: null));

        var reloaded = new JsonCertificateStore(_dir);
        await reloaded.LoadAsync();

        var cert = reloaded.All.Should().ContainSingle().Subject;
        cert.CertificatePath.Should().BeNull();
        cert.PrivateKeyPath.Should().BeNull();
        cert.OutputDirectory.Should().BeNull();
    }
}
