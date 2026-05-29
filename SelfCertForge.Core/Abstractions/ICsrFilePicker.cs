using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Abstractions;

public interface ICsrFilePicker
{
    Task<CsrFilePickResult?> PickCsrFileAsync(CancellationToken ct = default);
}
