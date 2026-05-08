using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SelfCertForge.Core.Models;
using SelfCertForge.Infrastructure;

namespace SelfCertForge.Core.Tests;

public sealed class DotNetCryptoCertificateWorkflowServiceValidationTests : IDisposable
{
    private readonly string _workingDirectory;
    private readonly DotNetCryptoCertificateWorkflowService _service = new();

    public DotNetCryptoCertificateWorkflowServiceValidationTests()
    {
        _workingDirectory = Path.Combine(AppContext.BaseDirectory, $"dotnet-workflow-service-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workingDirectory);
    }

    // ── Validation tests (ported from OpenSslCliCertificateWorkflowServiceValidationTests) ─────

    [Fact]
    public async Task GenerateSignedCertificateAsync_SeparateMode_RequiresExplicitRootCertificatePath()
    {
        var rootKeyPath = Path.Combine(_workingDirectory, "homeRoot.key");
        var siblingPemPath = Path.Combine(_workingDirectory, "homeRoot.pem");
        File.WriteAllText(rootKeyPath, "fake key");
        File.WriteAllText(siblingPemPath, "fake cert");

        var request = new SignedCertificateRequest
        {
            SourceMode = SignedCertificateSourceMode.SeparateFiles,
            CertificateName = "api",
            OutputDirectory = _workingDirectory,
            RootPrivateKeyPath = rootKeyPath,
            RootCertificatePath = null,
            SubjectDn = "/CN=api.local"
        };

        var act = () => _service.GenerateSignedCertificateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Root certificate path is required for separate-files signing*");
    }

    [Fact]
    public async Task GenerateSignedCertificateAsync_PfxMode_RequiresExistingBundleFile()
    {
        var request = new SignedCertificateRequest
        {
            SourceMode = SignedCertificateSourceMode.PfxBundle,
            CertificateName = "api",
            OutputDirectory = _workingDirectory,
            RootPfxBundlePath = Path.Combine(_workingDirectory, "missing-root.pfx"),
            RootPfxPassword = "secret",
            SubjectDn = "/CN=api.local"
        };

        var act = () => _service.GenerateSignedCertificateAsync(request);

        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*Root PFX bundle file was not found*");
    }

    [Fact]
    public async Task GenerateSignedCertificateAsync_SeparateMode_RejectsPfxFields()
    {
        var rootKeyPath = Path.Combine(_workingDirectory, "homeRoot.key");
        var rootCertPath = Path.Combine(_workingDirectory, "homeRoot.pem");
        File.WriteAllText(rootKeyPath, "fake key");
        File.WriteAllText(rootCertPath, "fake cert");

        var request = new SignedCertificateRequest
        {
            SourceMode = SignedCertificateSourceMode.SeparateFiles,
            CertificateName = "api",
            OutputDirectory = _workingDirectory,
            RootPrivateKeyPath = rootKeyPath,
            RootCertificatePath = rootCertPath,
            RootPfxBundlePath = Path.Combine(_workingDirectory, "root.pfx"),
            RootPfxPassword = "secret",
            SubjectDn = "/CN=api.local"
        };

        var act = () => _service.GenerateSignedCertificateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must not be set when source mode is SeparateFiles*");
    }

    [Fact]
    public async Task GenerateSignedCertificateAsync_PfxMode_RejectsSeparateRootFields()
    {
        var request = new SignedCertificateRequest
        {
            SourceMode = SignedCertificateSourceMode.PfxBundle,
            CertificateName = "api",
            OutputDirectory = _workingDirectory,
            RootPrivateKeyPath = Path.Combine(_workingDirectory, "homeRoot.key"),
            RootCertificatePath = Path.Combine(_workingDirectory, "homeRoot.pem"),
            RootPfxBundlePath = Path.Combine(_workingDirectory, "missing-root.pfx"),
            RootPfxPassword = "secret",
            SubjectDn = "/CN=api.local"
        };

        var act = () => _service.GenerateSignedCertificateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must not be set when source mode is PfxBundle*");
    }

    [Fact]
    public async Task GenerateSignedCertificateAsync_UnknownSourceMode_IsRejected()
    {
        var request = new SignedCertificateRequest
        {
            SourceMode = SignedCertificateSourceMode.Unknown,
            CertificateName = "api",
            OutputDirectory = _workingDirectory,
            SubjectDn = "/CN=api.local"
        };

        var act = () => _service.GenerateSignedCertificateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Source mode is required*");
    }

    [Fact]
    public async Task GenerateRootCertificateAsync_RejectsUnsafeRootName()
    {
        var request = new RootCertificateRequest
        {
            OutputDirectory = _workingDirectory,
            RootName = "../homeRoot",
            SubjectDn = "/CN=homeRoot"
        };

        var act = () => _service.GenerateRootCertificateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Root name*safe file-name token*");
    }

    [Fact]
    public async Task GenerateSignedCertificateAsync_RejectsUnsafeCertificateName()
    {
        var request = new SignedCertificateRequest
        {
            SourceMode = SignedCertificateSourceMode.PfxBundle,
            CertificateName = "..",
            OutputDirectory = _workingDirectory,
            RootPfxBundlePath = Path.Combine(_workingDirectory, "missing-root.pfx"),
            RootPfxPassword = "secret",
            SubjectDn = "/CN=api.local"
        };

        var act = () => _service.GenerateSignedCertificateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Certificate name*safe file-name token*");
    }

    [Fact]
    public async Task GenerateSignedCertificateAsync_SeparateMode_RejectsInvalidRootKeyExtension()
    {
        var rootKeyPath = Path.Combine(_workingDirectory, "homeRoot.txt");
        var rootCertPath = Path.Combine(_workingDirectory, "homeRoot.pem");
        File.WriteAllText(rootKeyPath, "fake key");
        File.WriteAllText(rootCertPath, "fake cert");

        var request = new SignedCertificateRequest
        {
            SourceMode = SignedCertificateSourceMode.SeparateFiles,
            CertificateName = "api",
            OutputDirectory = _workingDirectory,
            RootPrivateKeyPath = rootKeyPath,
            RootCertificatePath = rootCertPath,
            SubjectDn = "/CN=api.local"
        };

        var act = () => _service.GenerateSignedCertificateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Root private key path*unsupported extension*");
    }

    [Fact]
    public async Task GenerateSignedCertificateAsync_SeparateMode_AcceptsPemRootKeyExtension()
    {
        var rootKeyPath = Path.Combine(_workingDirectory, "homeRoot.pem");
        var rootCertPath = Path.Combine(_workingDirectory, "homeRoot.crt");
        File.WriteAllText(rootCertPath, "fake cert");

        var request = new SignedCertificateRequest
        {
            SourceMode = SignedCertificateSourceMode.SeparateFiles,
            CertificateName = "api",
            OutputDirectory = _workingDirectory,
            RootPrivateKeyPath = rootKeyPath,
            RootCertificatePath = rootCertPath,
            SubjectDn = "/CN=api.local"
        };

        var act = () => _service.GenerateSignedCertificateAsync(request);

        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*Root private key file was not found*");
    }

    [Fact]
    public async Task GenerateSignedCertificateAsync_SeparateMode_RejectsInvalidRootCertificateExtension()
    {
        var rootKeyPath = Path.Combine(_workingDirectory, "homeRoot.key");
        var rootCertPath = Path.Combine(_workingDirectory, "homeRoot.txt");
        File.WriteAllText(rootKeyPath, "fake key");
        File.WriteAllText(rootCertPath, "fake cert");

        var request = new SignedCertificateRequest
        {
            SourceMode = SignedCertificateSourceMode.SeparateFiles,
            CertificateName = "api",
            OutputDirectory = _workingDirectory,
            RootPrivateKeyPath = rootKeyPath,
            RootCertificatePath = rootCertPath,
            SubjectDn = "/CN=api.local"
        };

        var act = () => _service.GenerateSignedCertificateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Root certificate path*unsupported extension*");
    }

    [Fact]
    public async Task GenerateSignedCertificateAsync_PfxMode_RejectsInvalidBundleExtension()
    {
        var bundlePath = Path.Combine(_workingDirectory, "root.pem");
        File.WriteAllText(bundlePath, "fake cert");

        var request = new SignedCertificateRequest
        {
            SourceMode = SignedCertificateSourceMode.PfxBundle,
            CertificateName = "api",
            OutputDirectory = _workingDirectory,
            RootPfxBundlePath = bundlePath,
            RootPfxPassword = "secret",
            SubjectDn = "/CN=api.local"
        };

        var act = () => _service.GenerateSignedCertificateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Root PFX bundle path*unsupported extension*");
    }

    // ── DotNet-service-specific tests (require real cert generation) ──────────────────────────

    private async Task<(string certPemPath, string keyPath)> GenerateRootAsync(string rootName = "testRoot")
    {
        var result = await _service.GenerateRootCertificateAsync(new RootCertificateRequest
        {
            OutputDirectory = _workingDirectory,
            RootName = rootName,
            SubjectDn = $"CN={rootName}"
        });
        return (result.CertPemPath, result.KeyPath);
    }

    private async Task<CertificateGenerationResult> GenerateSignedAsync(
        string certName,
        string rootCertPath,
        string rootKeyPath,
        Action<SignedCertificateRequest>? configure = null)
    {
        var request = new SignedCertificateRequest
        {
            SourceMode = SignedCertificateSourceMode.SeparateFiles,
            CertificateName = certName,
            OutputDirectory = _workingDirectory,
            RootCertificatePath = rootCertPath,
            RootPrivateKeyPath = rootKeyPath,
            SubjectDn = $"CN={certName}.local"
        };
        configure?.Invoke(request);
        return await _service.GenerateSignedCertificateAsync(request);
    }

    [Fact]
    public async Task GenerateSignedCertificateAsync_KeyUsageFlags_ReflectedInCertificate()
    {
        var (rootCertPath, rootKeyPath) = await GenerateRootAsync();

        var result = await GenerateSignedAsync("leaf-ku", rootCertPath, rootKeyPath, req =>
        {
            req.KeyUsageDigitalSignature = true;
            req.KeyUsageNonRepudiation = true;
            req.KeyUsageKeyEncipherment = false;
        });

        using var cert = X509Certificate2.CreateFromPem(File.ReadAllText(result.CertPemPath));
        var kuExt = cert.Extensions.OfType<X509KeyUsageExtension>().Single();

        kuExt.KeyUsages.Should().HaveFlag(X509KeyUsageFlags.DigitalSignature);
        kuExt.KeyUsages.Should().HaveFlag(X509KeyUsageFlags.NonRepudiation);
        kuExt.KeyUsages.Should().NotHaveFlag(X509KeyUsageFlags.KeyEncipherment);
    }

    [Fact]
    public async Task GenerateSignedCertificateAsync_EkuNotPresent_WhenBothFlagsFalse()
    {
        var (rootCertPath, rootKeyPath) = await GenerateRootAsync();

        var result = await GenerateSignedAsync("leaf-no-eku", rootCertPath, rootKeyPath, req =>
        {
            req.EkuServerAuth = false;
            req.EkuClientAuth = false;
        });

        using var cert = X509Certificate2.CreateFromPem(File.ReadAllText(result.CertPemPath));
        var ekuExt = cert.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();

        ekuExt.Should().BeNull();
    }

    [Fact]
    public async Task GenerateSignedCertificateAsync_EkuServerAuthOnly_WhenOnlyServerAuthTrue()
    {
        var (rootCertPath, rootKeyPath) = await GenerateRootAsync();

        var result = await GenerateSignedAsync("leaf-server-eku", rootCertPath, rootKeyPath, req =>
        {
            req.EkuServerAuth = true;
            req.EkuClientAuth = false;
        });

        using var cert = X509Certificate2.CreateFromPem(File.ReadAllText(result.CertPemPath));
        var ekuExt = cert.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single();

        var oids = ekuExt.EnhancedKeyUsages.OfType<Oid>().Select(o => o.Value).ToList();
        oids.Should().ContainSingle().Which.Should().Be("1.3.6.1.5.5.7.3.1"); // serverAuth only
    }

    [Fact]
    public async Task GenerateSignedCertificateAsync_ReuseExistingDeviceKey_PreservesPrivateKey()
    {
        var (rootCertPath, rootKeyPath) = await GenerateRootAsync();

        // First generation — creates new key
        var result1 = await GenerateSignedAsync("leaf-reuse", rootCertPath, rootKeyPath, req =>
        {
            req.ReuseExistingDeviceKey = false;
        });

        var originalKeyBytes = await File.ReadAllBytesAsync(result1.KeyPath);

        // Second generation — should reuse the existing key
        var result2 = await GenerateSignedAsync("leaf-reuse", rootCertPath, rootKeyPath, req =>
        {
            req.ReuseExistingDeviceKey = true;
        });

        var reusedKeyBytes = await File.ReadAllBytesAsync(result2.KeyPath);

        reusedKeyBytes.Should().Equal(originalKeyBytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }
}
