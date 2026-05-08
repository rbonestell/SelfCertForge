namespace SelfCertForge.Core.Presentation;

/// <summary>
/// Identifies which card on the Settings page initiated the most recent
/// preference change. Used to scope the "Saved" toast to a single card.
/// </summary>
public enum SettingsCardSection
{
    CertificateDefaults,
    Application,
}
