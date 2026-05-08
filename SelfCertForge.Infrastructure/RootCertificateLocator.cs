namespace SelfCertForge.Infrastructure;

public static class RootCertificateLocator
{
    public static string ResolveRootCertPath(string rootPrivateKeyPath, string? rootCertificatePath)
    {
        if (string.IsNullOrWhiteSpace(rootPrivateKeyPath))
        {
            throw new ArgumentException("Root private key path is required.", nameof(rootPrivateKeyPath));
        }

        if (!File.Exists(rootPrivateKeyPath))
        {
            throw new FileNotFoundException($"Root private key file was not found: {rootPrivateKeyPath}", rootPrivateKeyPath);
        }

        if (string.IsNullOrWhiteSpace(rootCertificatePath))
        {
            throw new InvalidOperationException("Root certificate path is required.");
        }

        if (!File.Exists(rootCertificatePath))
        {
            throw new FileNotFoundException($"Root certificate file was not found: {rootCertificatePath}", rootCertificatePath);
        }

        return rootCertificatePath;
    }
}
