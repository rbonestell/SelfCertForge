namespace SelfCertForge.Core.Models;

public sealed record UpdateInfo(
    string Version,
    string? ReleaseNotes,
    string? DownloadUrl,
    DateTimeOffset? PublishedAt);
