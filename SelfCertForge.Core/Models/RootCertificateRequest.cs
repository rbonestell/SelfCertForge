namespace SelfCertForge.Core.Models;

public sealed class RootCertificateRequest
{
    public string OutputDirectory { get; set; } = string.Empty;

    public string RootName { get; set; } = "homeRoot";

    public int KeySizeBits { get; set; } = 2048;

    public int ValidForDays { get; set; } = 9125;

    public string SubjectDn { get; set; } = "/C=US/ST=Colorado/L=Durango/O=Bonestell/OU=Home/CN=homeRoot";
}
