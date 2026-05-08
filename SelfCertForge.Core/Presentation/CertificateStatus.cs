using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Presentation;

/// <summary>
/// Derives <see cref="StatusPill"/> kind strings per the design-system trust model.
/// Roots: <c>installed</c> / <c>uninstalled</c>. Children: <c>valid</c> / <c>expiring</c> /
/// <c>expired</c> / <c>orphaned</c>. Trust never applies to a child directly.
/// </summary>
public static class CertificateStatus
{
    public static readonly TimeSpan ExpiringWindow = TimeSpan.FromDays(30);

    public static string DeriveRootKind(StoredCertificate root, bool isTrusted)
    {
        if (root.Kind != StoredCertificateKind.Root)
            throw new ArgumentException("Expected a root certificate.", nameof(root));
        return isTrusted ? "installed" : "uninstalled";
    }

    public static string DeriveChildKind(
        StoredCertificate child,
        IReadOnlyList<StoredCertificate> allCertificates,
        DateTimeOffset now)
    {
        if (child.Kind != StoredCertificateKind.Child)
            throw new ArgumentException("Expected a child certificate.", nameof(child));

        var issuerExists = child.IssuerId is not null
            && allCertificates.Any(c => c.Id == child.IssuerId && c.Kind == StoredCertificateKind.Root);

        if (!issuerExists) return "orphaned";
        if (child.ExpiresAt <= now) return "expired";
        if (child.ExpiresAt - now <= ExpiringWindow) return "expiring";
        return "valid";
    }
}
