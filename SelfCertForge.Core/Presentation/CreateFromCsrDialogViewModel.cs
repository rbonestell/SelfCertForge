using System.Collections.ObjectModel;
using System.Windows.Input;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

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

    private bool _isKuLocked;
    private bool _isEkuLocked;

    private bool _kuDigitalSignature, _kuNonRepudiation, _kuKeyEncipherment, _kuDataEncipherment,
                 _kuKeyAgreement, _kuKeyCertSign, _kuCrlSign;
    private bool _ekuServerAuth, _ekuClientAuth, _ekuCodeSigning, _ekuTimeStamping;
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
        set { if (SetProperty(ref _newSanValue, value)) (AddSanCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

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

        SanEntries.Clear();
        foreach (var s in summary.RequestedSans)
            SanEntries.Add(new CsrSanOriginRowViewModel(s, CsrSignedSanOrigin.FromCsr, RemoveSan));

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
            _kuKeyEncipherment = true;
        }

        if (summary.RequestedEkus is { } e)
        {
            IsEkuLocked = true;
            _ekuServerAuth = e.ServerAuth;
            _ekuClientAuth = e.ClientAuth;
            _ekuCodeSigning = e.CodeSigning;
            _ekuTimeStamping = e.TimeStamping;
        }
        else
        {
            IsEkuLocked = false;
        }

        Notify();
    }

    private void AddSan()
    {
        var v = _newSanValue.Trim();
        if (string.IsNullOrEmpty(v)) return;
        SanEntries.Add(new CsrSanOriginRowViewModel(v, CsrSignedSanOrigin.AddedByOperator, RemoveSan));
        NewSanValue = string.Empty;
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
                Sans: SanEntries.Select(r => new CsrSignedSanEntry(r.Value, r.Origin)).ToArray(),
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

    private void Notify()
    {
        OnPropertyChanged(nameof(CanSubmit));
        (SubmitCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (AddSanCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
