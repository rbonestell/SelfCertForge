namespace SelfCertForge.Core.Models;

public sealed record ActivityEntry(
    string Id,
    DateTimeOffset At,
    string Kind,
    string Message,
    string? CertificateId);
