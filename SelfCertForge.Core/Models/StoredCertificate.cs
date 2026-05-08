namespace SelfCertForge.Core.Models;

/// <summary>
/// A certificate that has been forged through SelfCertForge and persisted in the local store.
/// Trust-store state is meaningful only when <see cref="Kind"/> is <see cref="StoredCertificateKind.Root"/>;
/// per the design system, child certificates inherit trust by chain, never directly.
/// </summary>
public sealed record StoredCertificate(
    string Id,
    StoredCertificateKind Kind,
    string CommonName,
    string Subject,
    string? IssuerId,
    string? IssuerName,
    IReadOnlyList<string> Sans,
    string Algorithm,
    string Serial,
    string Sha256,
    string Sha1,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    bool InstalledInTrustStore,
    string? CertificatePath = null,
    string? PrivateKeyPath = null,
    string? OutputDirectory = null,
    IReadOnlyList<string>? KeyUsages = null,
    IReadOnlyList<string>? ExtendedKeyUsages = null);
