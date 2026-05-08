namespace SelfCertForge.Core.Abstractions;

public interface IPfxPasswordDialog
{
    /// <summary>Returns (Confirmed: true, Password: value) or (Confirmed: false) when cancelled.</summary>
    Task<(bool Confirmed, string? Password)> ShowAsync();
}
