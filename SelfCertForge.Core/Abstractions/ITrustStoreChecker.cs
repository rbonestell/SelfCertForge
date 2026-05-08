namespace SelfCertForge.Core.Abstractions;

public interface ITrustStoreChecker
{
    event EventHandler? Changed;
    bool IsTrusted(string sha1Thumbprint);
    Task<(bool Success, string? ErrorMessage)> InstallAsync(string certificatePemPath);
}
