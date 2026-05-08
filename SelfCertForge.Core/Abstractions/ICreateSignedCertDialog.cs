using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Abstractions;

public interface ICreateSignedCertDialog
{
    Task<StoredCertificate?> ShowAsync(string issuerId, string issuerName);
}
