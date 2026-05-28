// SelfCertForge.Core/Models/CsrValidationError.cs
namespace SelfCertForge.Core.Models;

public enum CsrValidationError
{
    Malformed,
    InvalidProofOfPossession,
    UnsupportedKeyAlgorithm,
    KeyTooSmall,
    SubjectDnEmptyOrMalformed,
}
