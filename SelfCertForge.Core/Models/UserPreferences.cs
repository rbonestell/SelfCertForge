namespace SelfCertForge.Core.Models;

/// <summary>
/// All persisted user-tunable settings. Acts as defaults that pre-fill the
/// Create Root / Create Signed dialogs, plus app-level behavior knobs.
/// </summary>
public sealed record UserPreferences
{
    /// <summary>Default validity (days) used when creating a new root authority. ~25 years.</summary>
    public int RootValidityDays { get; init; } = 9125;

    /// <summary>Default validity (days) used when creating a signed certificate. CA/Browser-Forum max for public TLS.</summary>
    public int SignedValidityDays { get; init; } = 397;

    /// <summary>Default RSA key size (bits). Allowed values: 2048, 3072, 4096.</summary>
    public int KeyBits { get; init; } = 2048;

    /// <summary>Default signing hash algorithm.</summary>
    public HashAlgorithmKind HashAlgorithm { get; init; } = HashAlgorithmKind.Sha256;

    public string? DefaultOrganization { get; init; }
    public string? DefaultOrganizationalUnit { get; init; }
    public string? DefaultLocality { get; init; }
    public string? DefaultStateOrProvince { get; init; }
    /// <summary>2-letter ISO country code (e.g. "US"). Stored as-typed; uppercased at signing time.</summary>
    public string? DefaultCountry { get; init; }
    public string? DefaultEmail { get; init; }

    /// <summary>Cap for the activity log on disk. <see cref="ActivityRetention.Unlimited"/> disables pruning.</summary>
    public ActivityRetention ActivityRetention { get; init; } = ActivityRetention.FiveHundred;

    public static UserPreferences Default { get; } = new();
}
