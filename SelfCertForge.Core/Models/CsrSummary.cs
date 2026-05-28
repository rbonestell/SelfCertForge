// SelfCertForge.Core/Models/CsrSummary.cs (will be expanded in Task 2)
namespace SelfCertForge.Core.Models;

public sealed record CsrSummary(
    string SubjectDistinguishedName,
    string PublicKeyAlgorithm,
    int PublicKeyBits,
    string PublicKeyFingerprintSha256,
    string RawCsrPem);
