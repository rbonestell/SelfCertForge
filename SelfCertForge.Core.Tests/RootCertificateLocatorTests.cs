using SelfCertForge.Infrastructure;

namespace SelfCertForge.Core.Tests;

public sealed class RootCertificateLocatorTests : IDisposable
{
    private readonly string _tempDirectory;

    public RootCertificateLocatorTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"selfcertforge-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void ResolveRootCertPath_ReturnsExplicitPath_WhenProvided()
    {
        var rootKeyPath = Path.Combine(_tempDirectory, "homeRoot.key");
        var explicitCert = Path.Combine(_tempDirectory, "explicit-root.pem");
        File.WriteAllText(rootKeyPath, "fake key");
        File.WriteAllText(explicitCert, "fake cert");

        var resolved = RootCertificateLocator.ResolveRootCertPath(
            rootKeyPath,
            explicitCert);

        resolved.Should().Be(explicitCert);
    }

    [Fact]
    public void ResolveRootCertPath_Throws_WhenExplicitCertPathMissing()
    {
        var rootKeyPath = Path.Combine(_tempDirectory, "homeRoot.key");
        File.WriteAllText(rootKeyPath, "fake key");

        var act = () => RootCertificateLocator.ResolveRootCertPath(rootKeyPath, null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Root certificate path is required*");
    }

    [Fact]
    public void ResolveRootCertPath_Throws_WhenKeyPathIsNull()
    {
        var act = () => RootCertificateLocator.ResolveRootCertPath(null!, "some-cert.pem");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("rootPrivateKeyPath");
    }

    [Fact]
    public void ResolveRootCertPath_Throws_WhenKeyFileDoesNotExist()
    {
        var missingKeyPath = Path.Combine(_tempDirectory, "nonexistent.key");
        var certPath = Path.Combine(_tempDirectory, "cert.pem");

        var act = () => RootCertificateLocator.ResolveRootCertPath(missingKeyPath, certPath);

        act.Should().Throw<FileNotFoundException>()
            .Which.FileName.Should().Be(missingKeyPath);
    }

    [Fact]
    public void ResolveRootCertPath_Throws_WhenCertPathIsWhitespace()
    {
        var rootKeyPath = Path.Combine(_tempDirectory, "root.key");
        File.WriteAllText(rootKeyPath, "fake key");

        var act = () => RootCertificateLocator.ResolveRootCertPath(rootKeyPath, "  ");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Root certificate path is required*");
    }

    [Fact]
    public void ResolveRootCertPath_Throws_WhenCertFileDoesNotExist()
    {
        var rootKeyPath = Path.Combine(_tempDirectory, "root.key");
        File.WriteAllText(rootKeyPath, "fake key");
        var missingCertPath = Path.Combine(_tempDirectory, "nonexistent.pem");

        var act = () => RootCertificateLocator.ResolveRootCertPath(rootKeyPath, missingCertPath);

        act.Should().Throw<FileNotFoundException>()
            .Which.FileName.Should().Be(missingCertPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
