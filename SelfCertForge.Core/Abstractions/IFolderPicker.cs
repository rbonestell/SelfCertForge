namespace SelfCertForge.Core.Abstractions;

public interface IFolderPicker
{
    Task<string?> PickAsync(CancellationToken ct = default);
}
