using SelfCertForge.Infrastructure;

namespace SelfCertForge.Core.Tests;

public sealed class DataFolderLayoutTests : IDisposable
{
    private readonly string _root;

    public DataFolderLayoutTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "scf-datalayout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void Resolve_returns_dedicated_subfolder_and_creates_it()
    {
        var target = DataFolderLayout.Resolve(_root);

        target.Should().Be(Path.Combine(_root, "SelfCertForge"));
        Directory.Exists(target).Should().BeTrue();
    }

    [Fact]
    public void Resolve_relocates_legacy_files_and_certificates_folder()
    {
        File.WriteAllText(Path.Combine(_root, "certificates.json"), "[certs]");
        File.WriteAllText(Path.Combine(_root, "activity.json"), "[activity]");
        File.WriteAllText(Path.Combine(_root, "preferences.json"), "{prefs}");
        var legacyCertDir = Path.Combine(_root, "certificates");
        Directory.CreateDirectory(Path.Combine(legacyCertDir, "abc123"));
        File.WriteAllText(Path.Combine(legacyCertDir, "abc123", "root.pem"), "PEM");

        var target = DataFolderLayout.Resolve(_root);

        // Moved into the subfolder...
        File.ReadAllText(Path.Combine(target, "certificates.json")).Should().Be("[certs]");
        File.ReadAllText(Path.Combine(target, "activity.json")).Should().Be("[activity]");
        File.ReadAllText(Path.Combine(target, "preferences.json")).Should().Be("{prefs}");
        File.ReadAllText(Path.Combine(target, "certificates", "abc123", "root.pem")).Should().Be("PEM");

        // ...and gone from the legacy root.
        File.Exists(Path.Combine(_root, "certificates.json")).Should().BeFalse();
        File.Exists(Path.Combine(_root, "activity.json")).Should().BeFalse();
        File.Exists(Path.Combine(_root, "preferences.json")).Should().BeFalse();
        Directory.Exists(legacyCertDir).Should().BeFalse();
    }

    [Fact]
    public void Resolve_does_not_clobber_existing_data_in_new_location()
    {
        var target = Path.Combine(_root, "SelfCertForge");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "certificates.json"), "NEW");
        File.WriteAllText(Path.Combine(_root, "certificates.json"), "LEGACY");

        DataFolderLayout.Resolve(_root);

        // New data wins; legacy left in place untouched.
        File.ReadAllText(Path.Combine(target, "certificates.json")).Should().Be("NEW");
        File.ReadAllText(Path.Combine(_root, "certificates.json")).Should().Be("LEGACY");
    }

    [Fact]
    public void Resolve_is_idempotent_when_no_legacy_data_present()
    {
        var first = DataFolderLayout.Resolve(_root);
        var second = DataFolderLayout.Resolve(_root);

        first.Should().Be(second);
        Directory.Exists(first).Should().BeTrue();
    }

    [Fact]
    public void MigrateLegacyData_noops_when_roots_are_identical()
    {
        File.WriteAllText(Path.Combine(_root, "certificates.json"), "keep");

        DataFolderLayout.MigrateLegacyData(_root, _root);

        File.ReadAllText(Path.Combine(_root, "certificates.json")).Should().Be("keep");
    }
}
