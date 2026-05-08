namespace SelfCertForge.Core.Models;

/// <summary>
/// Maximum activity log entries kept on disk. <see cref="Unlimited"/> disables pruning.
/// Backing values are the actual cap (negative = unlimited sentinel).
/// </summary>
public enum ActivityRetention
{
    OneHundred = 100,
    FiveHundred = 500,
    OneThousand = 1000,
    Unlimited = -1,
}
