namespace SelfCertForge.Core.Models;

public sealed record CsrSummary(
    string SubjectDistinguishedName,
    string PublicKeyAlgorithm,
    int PublicKeyBits,
    string PublicKeyFingerprintSha256,
    string RawCsrPem,
    IReadOnlyList<string> RequestedSans,
    CsrRequestedKeyUsages? RequestedKeyUsage,
    CsrRequestedEkus? RequestedEkus);

public sealed record CsrRequestedKeyUsages(
    bool DigitalSignature,
    bool NonRepudiation,
    bool KeyEncipherment,
    bool DataEncipherment,
    bool KeyAgreement,
    bool KeyCertSign,
    bool CrlSign);

public sealed record CsrRequestedEkus(
    bool ServerAuth,
    bool ClientAuth,
    bool CodeSigning,
    bool EmailProtection,
    bool TimeStamping);
