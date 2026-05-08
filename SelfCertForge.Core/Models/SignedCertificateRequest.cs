namespace SelfCertForge.Core.Models;

public sealed class SignedCertificateRequest
{
    public SignedCertificateSourceMode SourceMode { get; set; } = SignedCertificateSourceMode.Unknown;

    public string CertificateName { get; set; } = string.Empty;

    public string OutputDirectory { get; set; } = string.Empty;

    public string RootPrivateKeyPath { get; set; } = string.Empty;

    public string? RootCertificatePath { get; set; }

    public string? RootPfxBundlePath { get; set; }

    public string? RootPfxPassword { get; set; }

    public string SubjectDn { get; set; } = "/C=US/ST=Colorado/L=Durango/O=Bonestell/OU=Home/CN=Helicarrier";

    public List<string> SubjectAlternativeNames { get; set; } = [];

    public bool ReuseExistingDeviceKey { get; set; } = true;

    public int KeySizeBits { get; set; } = 2048;

    public int ValidForDays { get; set; } = 397;

    public bool KeyUsageDigitalSignature { get; set; } = true;

    public bool KeyUsageNonRepudiation { get; set; } = false;

    public bool KeyUsageKeyEncipherment { get; set; } = true;

    public bool KeyUsageDataEncipherment { get; set; } = false;

    public bool KeyUsageKeyAgreement { get; set; } = false;

    public bool KeyUsageEncipherOnly { get; set; } = false;

    public bool KeyUsageDecipherOnly { get; set; } = false;

    public bool EkuServerAuth { get; set; } = false;

    public bool EkuClientAuth { get; set; } = false;
}
