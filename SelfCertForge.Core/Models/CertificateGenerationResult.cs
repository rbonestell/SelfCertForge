namespace SelfCertForge.Core.Models;

public sealed class CertificateGenerationResult
{
    public required string OutputDirectory { get; init; }

    public required IReadOnlyList<string> GeneratedFiles { get; init; }
    public required string CertPemPath { get; init; }
    public required string KeyPath { get; init; }
}
