// SelfCertForge.Core/Validation/CsrValidationErrorMessages.cs
using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Validation;

public static class CsrValidationErrorMessages
{
    public static string Format(IReadOnlyCollection<CsrValidationError> errors)
    {
        if (errors.Count == 0)
            return "The certificate signing request could not be validated.";

        var lines = errors.Select(e => e switch
        {
            CsrValidationError.Malformed =>
                "The file could not be parsed as a PKCS#10 certificate signing request.",
            CsrValidationError.InvalidProofOfPossession =>
                "The CSR's proof-of-possession signature is invalid — the request may have been tampered with.",
            CsrValidationError.UnsupportedKeyAlgorithm =>
                "Only RSA public keys are supported. The CSR uses a different algorithm.",
            CsrValidationError.KeyTooSmall =>
                "The CSR's RSA key is smaller than the 2048-bit minimum.",
            CsrValidationError.SubjectDnEmptyOrMalformed =>
                "The CSR's Subject Distinguished Name is empty or malformed.",
            _ => "An unrecognized validation error occurred.",
        });

        return string.Join("\n", lines);
    }
}
