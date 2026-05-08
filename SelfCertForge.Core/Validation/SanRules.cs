using System.Net;
using System.Net.Sockets;

namespace SelfCertForge.Core.Validation;

/// <summary>
/// Validates Subject Alternative Name (SAN) entry values for the
/// Create Signed Certificate dialog.
///
/// Rules:
/// - DNS: only ASCII letters (a-z, A-Z), digits (0-9), and hyphen (-),
///   organized as one or more dot-separated labels. No leading/trailing
///   hyphens within a label. No empty labels. Length 1..253.
/// - IP: must parse as a valid IPv4 or IPv6 address.
///
/// The intent is to block obvious garbage at "Add" time without trying to
/// be a full RFC 1035 / 5891 implementation. Anything that survives this
/// pass is also accepted by the underlying X509 SAN extension.
/// </summary>
public static class SanRules
{
    public static SanValidationResult ValidateDns(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length == 0) return SanValidationResult.Fail("DNS name cannot be empty.");
        if (v.Length > 253) return SanValidationResult.Fail("DNS name is too long (max 253 characters).");

        // Split on '.', each label must be non-empty, 1..63 chars, no leading/trailing hyphen,
        // and contain only [A-Za-z0-9-].
        var labels = v.Split('.');
        foreach (var label in labels)
        {
            if (label.Length == 0)
                return SanValidationResult.Fail("DNS name has an empty label (check for double dots).");
            if (label.Length > 63)
                return SanValidationResult.Fail("DNS label is too long (max 63 characters).");
            if (label[0] == '-' || label[^1] == '-')
                return SanValidationResult.Fail("DNS labels cannot start or end with a hyphen.");
            for (int i = 0; i < label.Length; i++)
            {
                var c = label[i];
                bool ok = (c >= 'a' && c <= 'z')
                       || (c >= 'A' && c <= 'Z')
                       || (c >= '0' && c <= '9')
                       || c == '-';
                if (!ok)
                    return SanValidationResult.Fail("DNS name may contain only letters, digits, hyphens, and dots.");
            }
        }
        return SanValidationResult.Ok();
    }

    public static SanValidationResult ValidateIp(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length == 0) return SanValidationResult.Fail("IP address cannot be empty.");

        if (!IPAddress.TryParse(v, out var addr))
            return SanValidationResult.Fail("Enter a valid IPv4 or IPv6 address.");

        // TryParse accepts a few exotic forms — keep only v4/v6.
        if (addr.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
            return SanValidationResult.Fail("Enter a valid IPv4 or IPv6 address.");

        // .NET's IPAddress.TryParse accepts legacy IPv4 forms like "1.2.3"
        // (interpreted as 1.2.0.3) and "010.0.0.1" (octal). Reject anything
        // that isn't canonical 4-octet decimal "a.b.c.d" so SAN entries match
        // what users actually typed.
        if (addr.AddressFamily == AddressFamily.InterNetwork && !IsCanonicalIPv4(v))
            return SanValidationResult.Fail("Enter a valid IPv4 or IPv6 address.");

        return SanValidationResult.Ok();
    }

    private static bool IsCanonicalIPv4(string s)
    {
        var parts = s.Split('.');
        if (parts.Length != 4) return false;
        foreach (var p in parts)
        {
            if (p.Length is 0 or > 3) return false;
            // No leading zeros except for "0" itself.
            if (p.Length > 1 && p[0] == '0') return false;
            for (int i = 0; i < p.Length; i++)
                if (p[i] < '0' || p[i] > '9') return false;
            if (!int.TryParse(p, out var n) || n < 0 || n > 255) return false;
        }
        return true;
    }

    public static SanValidationResult Validate(string type, string? value) =>
        string.Equals(type, "IP", StringComparison.OrdinalIgnoreCase)
            ? ValidateIp(value)
            : ValidateDns(value);
}

public readonly record struct SanValidationResult(bool IsValid, string? Error)
{
    public static SanValidationResult Ok() => new(true, null);
    public static SanValidationResult Fail(string error) => new(false, error);
}
