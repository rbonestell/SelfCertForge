// SelfCertForge.Core/Models/CsrInspectionResult.cs
namespace SelfCertForge.Core.Models;

public sealed record CsrInspectionResult(
    bool IsValid,
    CsrSummary? Summary,
    IReadOnlyList<CsrValidationError> Errors);
