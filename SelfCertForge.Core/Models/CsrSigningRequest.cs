namespace SelfCertForge.Core.Models;

public enum CsrSignedSanOrigin
{
    FromCsr,
    AddedByOperator,
}

public sealed record CsrSignedSanEntry(string Value, CsrSignedSanOrigin Origin);

public sealed record CsrSigningRequest(
    string SigningAuthorityId,
    string RawCsrPem,
    string SourceCsrFilename,
    int ValidityDays,
    IReadOnlyList<CsrSignedSanEntry> Sans,
    bool KeyUsageDigitalSignature,
    bool KeyUsageNonRepudiation,
    bool KeyUsageKeyEncipherment,
    bool KeyUsageDataEncipherment,
    bool KeyUsageKeyAgreement,
    bool KeyUsageKeyCertSign,
    bool KeyUsageCrlSign,
    bool EkuServerAuth,
    bool EkuClientAuth,
    bool EkuCodeSigning,
    bool EkuTimeStamping,
    bool EkuEmailProtection,
    HashAlgorithmKind SignatureHashAlgorithm);
