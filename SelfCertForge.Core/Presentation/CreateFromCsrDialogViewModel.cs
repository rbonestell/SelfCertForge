using System.Collections.ObjectModel;
using System.Windows.Input;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Validation;

namespace SelfCertForge.Core.Presentation;

public sealed class CreateFromCsrDialogViewModel : ObservableObject
{
    private readonly IForgeService _forge;
    private readonly IUserPreferencesStore? _preferences;

    private string _signingAuthorityId = string.Empty;
    private string _signingAuthorityName = string.Empty;
    private string _subjectDn = string.Empty;
    private int _publicKeyBits;
    private string _publicKeyAlgorithm = string.Empty;
    private string _publicKeyFingerprint = string.Empty;
    private string _rawCsrPem = string.Empty;
    private string _sourceCsrFilename = string.Empty;
    private int _validityDays = 397;
    private bool _isCreating;
    private string? _errorMessage;
    private string _newSanValue = string.Empty;
    private string _newSanType = "DNS";
    private string? _sanValidationError;

    private bool _isKuLocked;
    private bool _isEkuLocked;

    private bool _kuDigitalSignature, _kuNonRepudiation, _kuKeyEncipherment, _kuDataEncipherment,
                 _kuKeyAgreement, _kuKeyCertSign, _kuCrlSign;
    private bool _ekuServerAuth, _ekuClientAuth, _ekuCodeSigning, _ekuTimeStamping, _ekuEmailProtection;
    private HashAlgorithmKind _hashAlgorithm = HashAlgorithmKind.Sha256;

    public CreateFromCsrDialogViewModel(IForgeService forge, IUserPreferencesStore? preferences = null)
    {
        _forge = forge;
        _preferences = preferences;
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => CanSubmit);
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));
        AddSanCommand = new RelayCommand(AddSan,
            () => !string.IsNullOrWhiteSpace(_newSanValue) && !_isCreating);
    }

    public event EventHandler<StoredCertificate>? Created;
    public event EventHandler? CancelRequested;

    public ICommand SubmitCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand AddSanCommand { get; }

    public ObservableCollection<CsrSanOriginRowViewModel> SanEntries { get; } = new();

    public string SigningAuthorityId => _signingAuthorityId;
    public string SigningAuthorityName { get => _signingAuthorityName; private set => SetProperty(ref _signingAuthorityName, value); }
    public string SubjectDistinguishedName { get => _subjectDn; private set => SetProperty(ref _subjectDn, value); }
    public int PublicKeyBits { get => _publicKeyBits; private set => SetProperty(ref _publicKeyBits, value); }
    public string PublicKeyAlgorithm { get => _publicKeyAlgorithm; private set => SetProperty(ref _publicKeyAlgorithm, value); }
    public string PublicKeyFingerprintSha256 { get => _publicKeyFingerprint; private set => SetProperty(ref _publicKeyFingerprint, value); }
    public string SourceCsrFilename { get => _sourceCsrFilename; private set => SetProperty(ref _sourceCsrFilename, value); }

    public int ValidityDays { get => _validityDays; set { if (SetProperty(ref _validityDays, value)) Notify(); } }

    public string NewSanValue
    {
        get => _newSanValue;
        set
        {
            if (SetProperty(ref _newSanValue, value))
            {
                SanValidationError = null;
                (AddSanCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string NewSanType
    {
        get => _newSanType;
        set
        {
            if (SetProperty(ref _newSanType, value))
            {
                SanValidationError = null;
                OnPropertyChanged(nameof(NewSanPlaceholder));
            }
        }
    }

    public string NewSanPlaceholder =>
        string.Equals(_newSanType, "IP", StringComparison.OrdinalIgnoreCase)
            ? "10.0.0.1"
            : "api.example.local";

    public string? SanValidationError
    {
        get => _sanValidationError;
        private set
        {
            if (SetProperty(ref _sanValidationError, value))
                OnPropertyChanged(nameof(HasSanValidationError));
        }
    }

    public bool HasSanValidationError => !string.IsNullOrEmpty(_sanValidationError);

    public bool IsKeyUsageLocked { get => _isKuLocked; private set => SetProperty(ref _isKuLocked, value); }
    public bool IsEkuLocked { get => _isEkuLocked; private set => SetProperty(ref _isEkuLocked, value); }

    public bool KeyUsageDigitalSignature { get => _kuDigitalSignature; set { if (!IsKeyUsageLocked) SetProperty(ref _kuDigitalSignature, value); } }
    public bool KeyUsageNonRepudiation   { get => _kuNonRepudiation;   set { if (!IsKeyUsageLocked) SetProperty(ref _kuNonRepudiation, value); } }
    public bool KeyUsageKeyEncipherment  { get => _kuKeyEncipherment;  set { if (!IsKeyUsageLocked) SetProperty(ref _kuKeyEncipherment, value); } }
    public bool KeyUsageDataEncipherment { get => _kuDataEncipherment; set { if (!IsKeyUsageLocked) SetProperty(ref _kuDataEncipherment, value); } }
    public bool KeyUsageKeyAgreement     { get => _kuKeyAgreement;     set { if (!IsKeyUsageLocked) SetProperty(ref _kuKeyAgreement, value); } }
    public bool KeyUsageKeyCertSign      { get => _kuKeyCertSign;      set { if (!IsKeyUsageLocked) SetProperty(ref _kuKeyCertSign, value); } }
    public bool KeyUsageCrlSign          { get => _kuCrlSign;          set { if (!IsKeyUsageLocked) SetProperty(ref _kuCrlSign, value); } }

    public bool EkuServerAuth   { get => _ekuServerAuth;   set { if (!IsEkuLocked) SetProperty(ref _ekuServerAuth, value); } }
    public bool EkuClientAuth   { get => _ekuClientAuth;   set { if (!IsEkuLocked) SetProperty(ref _ekuClientAuth, value); } }
    public bool EkuCodeSigning  { get => _ekuCodeSigning;  set { if (!IsEkuLocked) SetProperty(ref _ekuCodeSigning, value); } }
    public bool EkuTimeStamping { get => _ekuTimeStamping; set { if (!IsEkuLocked) SetProperty(ref _ekuTimeStamping, value); } }
    public bool EkuEmailProtection { get => _ekuEmailProtection; set { if (!IsEkuLocked) SetProperty(ref _ekuEmailProtection, value); } }

    public HashAlgorithmKind HashAlgorithm { get => _hashAlgorithm; set => SetProperty(ref _hashAlgorithm, value); }

    public bool IsCreating { get => _isCreating; private set => SetProperty(ref _isCreating, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public bool CanSubmit =>
        !_isCreating && _validityDays > 0 && !string.IsNullOrEmpty(_signingAuthorityId);

    public void Initialize(string signingAuthorityId, string signingAuthorityName,
        CsrSummary summary, string sourceCsrFilename)
    {
        _signingAuthorityId = signingAuthorityId;
        SigningAuthorityName = signingAuthorityName;
        SubjectDistinguishedName = summary.SubjectDistinguishedName;
        PublicKeyAlgorithm = summary.PublicKeyAlgorithm;
        PublicKeyBits = summary.PublicKeyBits;
        PublicKeyFingerprintSha256 = summary.PublicKeyFingerprintSha256;
        _rawCsrPem = summary.RawCsrPem;
        SourceCsrFilename = sourceCsrFilename;
        ValidityDays = _preferences?.Current.SignedValidityDays ?? 397;

        var cn = TryExtractCommonName(summary.SubjectDistinguishedName);

        SanEntries.Clear();
        foreach (var s in summary.RequestedSans)
        {
            var (type, value) = ParseSan(s);
            // Don't surface the Subject CN as a duplicate SAN entry —
            // it lives on the cert as the Subject already and operators
            // expect SANs to be the *additional* names.
            if (cn is not null && string.Equals(value, cn, StringComparison.OrdinalIgnoreCase))
                continue;
            SanEntries.Add(new CsrSanOriginRowViewModel(type, value, CsrSignedSanOrigin.FromCsr, RemoveSan));
        }

        if (summary.RequestedKeyUsage is { } ku)
        {
            IsKeyUsageLocked = true;
            _kuDigitalSignature = ku.DigitalSignature;
            _kuNonRepudiation = ku.NonRepudiation;
            _kuKeyEncipherment = ku.KeyEncipherment;
            _kuDataEncipherment = ku.DataEncipherment;
            _kuKeyAgreement = ku.KeyAgreement;
            _kuKeyCertSign = ku.KeyCertSign;
            _kuCrlSign = ku.CrlSign;
        }
        else
        {
            IsKeyUsageLocked = false;
            _kuDigitalSignature = true;
            _kuNonRepudiation = false;
            _kuKeyEncipherment = true;
            _kuDataEncipherment = false;
            _kuKeyAgreement = false;
            _kuKeyCertSign = false;
            _kuCrlSign = false;
        }

        if (summary.RequestedEkus is { } e)
        {
            IsEkuLocked = true;
            _ekuServerAuth = e.ServerAuth;
            _ekuClientAuth = e.ClientAuth;
            _ekuCodeSigning = e.CodeSigning;
            _ekuTimeStamping = e.TimeStamping;
            _ekuEmailProtection = e.EmailProtection;
        }
        else
        {
            IsEkuLocked = false;
            _ekuServerAuth = false;
            _ekuClientAuth = false;
            _ekuCodeSigning = false;
            _ekuTimeStamping = false;
            _ekuEmailProtection = false;
        }

        NewSanType = "DNS";
        NewSanValue = string.Empty;
        SanValidationError = null;

        Notify();
    }

    private static (string Type, string Value) ParseSan(string raw)
    {
        if (raw.StartsWith("IP:", StringComparison.OrdinalIgnoreCase))
            return ("IP", raw[3..]);
        if (raw.StartsWith("DNS:", StringComparison.OrdinalIgnoreCase))
            return ("DNS", raw[4..]);
        return ("DNS", raw);
    }

    private static string? TryExtractCommonName(string distinguishedName)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName)) return null;
        // Distinguished names are comma-separated RDN=value pairs.
        // Take the first CN= we see; values may be quoted.
        foreach (var part in distinguishedName.Split(','))
        {
            var trimmed = part.TrimStart();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                var v = trimmed[3..].Trim();
                if (v.Length >= 2 && v[0] == '"' && v[^1] == '"') v = v[1..^1];
                return v;
            }
        }
        return null;
    }

    private void AddSan()
    {
        var v = _newSanValue.Trim();
        var result = SanRules.Validate(_newSanType, v);
        if (!result.IsValid)
        {
            SanValidationError = result.Error;
            return;
        }
        SanEntries.Add(new CsrSanOriginRowViewModel(_newSanType, v, CsrSignedSanOrigin.AddedByOperator, RemoveSan));
        NewSanValue = string.Empty;
        SanValidationError = null;
    }

    private void RemoveSan(CsrSanOriginRowViewModel row) => SanEntries.Remove(row);

    internal Task SubmitAsyncForTest() => SubmitAsync();

    private async Task SubmitAsync()
    {
        if (!CanSubmit) return;
        IsCreating = true;
        ErrorMessage = null;
        try
        {
            var request = new CsrSigningRequest(
                SigningAuthorityId: _signingAuthorityId,
                RawCsrPem: _rawCsrPem,
                SourceCsrFilename: _sourceCsrFilename,
                ValidityDays: _validityDays,
                Sans: SanEntries.Select(SerializeSan).ToArray(),
                KeyUsageDigitalSignature: _kuDigitalSignature,
                KeyUsageNonRepudiation: _kuNonRepudiation,
                KeyUsageKeyEncipherment: _kuKeyEncipherment,
                KeyUsageDataEncipherment: _kuDataEncipherment,
                KeyUsageKeyAgreement: _kuKeyAgreement,
                KeyUsageKeyCertSign: _kuKeyCertSign,
                KeyUsageCrlSign: _kuCrlSign,
                EkuServerAuth: _ekuServerAuth,
                EkuClientAuth: _ekuClientAuth,
                EkuCodeSigning: _ekuCodeSigning,
                EkuTimeStamping: _ekuTimeStamping,
                EkuEmailProtection: _ekuEmailProtection,
                SignatureHashAlgorithm: _hashAlgorithm);

            var stored = await _forge.ForgeFromCsrAsync(new ForgeFromCsrRequest(request));
            Created?.Invoke(this, stored);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsCreating = false;
            Notify();
        }
    }

    private static CsrSignedSanEntry SerializeSan(CsrSanOriginRowViewModel row)
    {
        // Workflow service routes SAN values by an "IP:" prefix; bare values are DNS.
        var value = string.Equals(row.Type, "IP", StringComparison.OrdinalIgnoreCase)
            ? "IP:" + row.Value
            : row.Value;
        return new CsrSignedSanEntry(value, row.Origin);
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(IsKeyUsageLocked));
        OnPropertyChanged(nameof(IsEkuLocked));
        OnPropertyChanged(nameof(KeyUsageDigitalSignature));
        OnPropertyChanged(nameof(KeyUsageNonRepudiation));
        OnPropertyChanged(nameof(KeyUsageKeyEncipherment));
        OnPropertyChanged(nameof(KeyUsageDataEncipherment));
        OnPropertyChanged(nameof(KeyUsageKeyAgreement));
        OnPropertyChanged(nameof(KeyUsageKeyCertSign));
        OnPropertyChanged(nameof(KeyUsageCrlSign));
        OnPropertyChanged(nameof(EkuServerAuth));
        OnPropertyChanged(nameof(EkuClientAuth));
        OnPropertyChanged(nameof(EkuCodeSigning));
        OnPropertyChanged(nameof(EkuTimeStamping));
        OnPropertyChanged(nameof(EkuEmailProtection));
        (SubmitCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (AddSanCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
