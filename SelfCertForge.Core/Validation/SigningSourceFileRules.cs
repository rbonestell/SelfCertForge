namespace SelfCertForge.Core.Validation;

public static class SigningSourceFileRules
{
    public static IReadOnlyCollection<string> AllowedSeparateModeRootKeyExtensions { get; } = [".key", ".pem"];

    public static IReadOnlyCollection<string> AllowedSeparateModeRootCertificateExtensions { get; } = [".pem", ".crt", ".cer"];

    public static IReadOnlyCollection<string> AllowedPfxBundleExtensions { get; } = [".pfx", ".p12"];

    public static bool HasAllowedSeparateModeRootKeyExtension(string? path)
        => HasAllowedExtension(path, AllowedSeparateModeRootKeyExtensions);

    public static bool HasAllowedSeparateModeRootCertificateExtension(string? path)
        => HasAllowedExtension(path, AllowedSeparateModeRootCertificateExtensions);

    public static bool HasAllowedPfxBundleExtension(string? path)
        => HasAllowedExtension(path, AllowedPfxBundleExtensions);

    public static string DescribeAllowedSeparateModeRootKeyExtensions()
        => string.Join(", ", AllowedSeparateModeRootKeyExtensions);

    public static string DescribeAllowedSeparateModeRootCertificateExtensions()
        => string.Join(", ", AllowedSeparateModeRootCertificateExtensions);

    public static string DescribeAllowedPfxBundleExtensions()
        => string.Join(", ", AllowedPfxBundleExtensions);

    private static bool HasAllowedExtension(string? path, IReadOnlyCollection<string> allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path.Trim());
        return !string.IsNullOrWhiteSpace(extension)
            && allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
