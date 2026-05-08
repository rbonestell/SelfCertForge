using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SelfCertForge.Core.Abstractions;

namespace SelfCertForge.Infrastructure;

public sealed class SystemTrustStoreChecker : ITrustStoreChecker
{
    public event EventHandler? Changed;

    public bool IsTrusted(string sha1Thumbprint)
    {
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
            return IsTrustedMac(sha1Thumbprint);

        try
        {
            var normalized = sha1Thumbprint.Replace(":", "").ToUpperInvariant();
            foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
            {
                try
                {
                    using var store = new X509Store(StoreName.Root, location);
                    store.Open(OpenFlags.ReadOnly);
                    foreach (var cert in store.Certificates)
                    {
                        if (cert.Thumbprint.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
                catch { }
            }
        }
        catch { }
        return false;
    }

    public async Task<(bool Success, string? ErrorMessage)> InstallAsync(string certificatePemPath)
    {
        if (!File.Exists(certificatePemPath))
            return (false, $"Certificate file not found: {certificatePemPath}");

        (bool success, string? error) result;
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
            result = await InstallMacAsync(certificatePemPath);
        else if (OperatingSystem.IsWindows())
            result = InstallWindows(certificatePemPath);
        else
            return (false, "Trust store installation is not supported on this platform.");

        if (result.success)
            Changed?.Invoke(this, EventArgs.Empty);
        return result;
    }

    private static bool IsTrustedMac(string sha1Thumbprint)
    {
        try
        {
            var normalized = sha1Thumbprint.Replace(":", "").ToUpperInvariant();
            var loginKeychain = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Keychains", "login.keychain-db");

            foreach (var keychain in new[] { loginKeychain, "/Library/Keychains/System.keychain" })
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "security",
                        ArgumentList = { "find-certificate", "-a", "-Z", keychain },
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc is null) continue;

                    string? line;
                    var found = false;
                    while ((line = proc.StandardOutput.ReadLine()) is not null)
                    {
                        var trimmed = line.TrimStart();
                        if (!trimmed.StartsWith("SHA-1 hash:", StringComparison.OrdinalIgnoreCase))
                            continue;
                        var hash = trimmed["SHA-1 hash:".Length..].Trim()
                            .Replace(" ", "").ToUpperInvariant();
                        if (hash != normalized) continue;
                        found = true;
                        break;
                    }

                    proc.Kill();
                    proc.WaitForExit();

                    if (found) return true;
                }
                catch { }
            }
        }
        catch { }
        return false;
    }

    private static async Task<(bool Success, string? ErrorMessage)> InstallMacAsync(string pemPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "security",
                ArgumentList = { "add-trusted-cert", "-r", "trustRoot", "-k",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "Library", "Keychains", "login.keychain-db"),
                    pemPath },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null)
                return (false, "Failed to start the security command.");

            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode == 0)
                return (true, null);

            var message = string.IsNullOrWhiteSpace(stderr)
                ? $"security add-trusted-cert exited with code {proc.ExitCode}."
                : stderr.Trim();
            return (false, message);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static (bool Success, string? ErrorMessage) InstallWindows(string pemPath)
    {
        try
        {
            var cert = X509CertificateLoader.LoadCertificateFromFile(pemPath);
            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(cert);
            return (true, null);
        }
        catch (CryptographicException ex)
        {
            return (false, ex.Message);
        }
        catch (PlatformNotSupportedException ex)
        {
            return (false, ex.Message);
        }
    }
}
