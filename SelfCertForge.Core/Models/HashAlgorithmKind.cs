namespace SelfCertForge.Core.Models;

/// <summary>
/// Hash algorithm used to sign generated certificates. Maps 1:1 to
/// <see cref="System.Security.Cryptography.HashAlgorithmName"/> in the
/// workflow service. Defaults to SHA-256 because that's the broadest
/// browser/OS-trust default.
/// </summary>
public enum HashAlgorithmKind
{
    Sha256 = 0,
    Sha384 = 1,
    Sha512 = 2,
}
