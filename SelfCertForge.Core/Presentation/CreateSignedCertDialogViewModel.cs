using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Presentation;

public sealed class CreateSignedCertDialogViewModel : ObservableObject
{
    private readonly IForgeService _forge;

    private string _issuerId = string.Empty;
    private string _issuerName = string.Empty;
    private string _commonName = string.Empty;
    private string _newSanValue = string.Empty;
    private string _newSanType = "DNS";
    private int _validityDays = 397;
    private int _keyBits = 2048;
    private bool _isCreating;
    private string? _errorMessage;
    private bool _keyUsageDigitalSignature = true;
    private bool _keyUsageNonRepudiation = false;
    private bool _keyUsageKeyEncipherment = true;
    private bool _keyUsageDataEncipherment = false;
    private bool _keyUsageKeyAgreement = false;
    private bool _keyUsageEncipherOnly = false;
    private bool _keyUsageDecipherOnly = false;
    private bool _ekuServerAuth = false;
    private bool _ekuClientAuth = false;

    public CreateSignedCertDialogViewModel(IForgeService forge)
    {
        _forge = forge;
        CreateCommand = new AsyncRelayCommand(SubmitAsync, () => CanSubmit);
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));
        AddSanCommand = new RelayCommand(AddSan, () => !string.IsNullOrWhiteSpace(_newSanValue) && !_isCreating);
    }

    public event EventHandler<StoredCertificate>? Created;
    public event EventHandler? CancelRequested;

    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand AddSanCommand { get; }

    public ObservableCollection<SanEntryViewModel> SanEntries { get; } = new();
    public bool HasSanEntries => SanEntries.Count > 0;

    public string IssuerName
    {
        get => _issuerName;
        private set => SetProperty(ref _issuerName, value);
    }

    public string CommonName
    {
        get => _commonName;
        set { if (SetProperty(ref _commonName, value)) Notify(); }
    }

    public string NewSanValue
    {
        get => _newSanValue;
        set
        {
            if (SetProperty(ref _newSanValue, value?.Trim() ?? string.Empty))
                ((RelayCommand)AddSanCommand).RaiseCanExecuteChanged();
        }
    }

    public string NewSanType
    {
        get => _newSanType;
        set
        {
            if (SetProperty(ref _newSanType, value))
                OnPropertyChanged(nameof(NewSanPlaceholder));
        }
    }

    public string NewSanPlaceholder => _newSanType == "IP" ? "127.0.0.1" : "api.local";

    public int ValidityDays
    {
        get => _validityDays;
        set => SetProperty(ref _validityDays, value);
    }

    public int KeyBits
    {
        get => _keyBits;
        set => SetProperty(ref _keyBits, value);
    }

    public bool KeyUsageDigitalSignature
    {
        get => _keyUsageDigitalSignature;
        set => SetProperty(ref _keyUsageDigitalSignature, value);
    }

    public bool KeyUsageNonRepudiation
    {
        get => _keyUsageNonRepudiation;
        set => SetProperty(ref _keyUsageNonRepudiation, value);
    }

    public bool KeyUsageKeyEncipherment
    {
        get => _keyUsageKeyEncipherment;
        set => SetProperty(ref _keyUsageKeyEncipherment, value);
    }

    public bool KeyUsageDataEncipherment
    {
        get => _keyUsageDataEncipherment;
        set => SetProperty(ref _keyUsageDataEncipherment, value);
    }

    public bool KeyUsageKeyAgreement
    {
        get => _keyUsageKeyAgreement;
        set
        {
            if (!SetProperty(ref _keyUsageKeyAgreement, value)) return;
            if (!value)
            {
                KeyUsageEncipherOnly = false;
                KeyUsageDecipherOnly = false;
            }
            OnPropertyChanged(nameof(CanSetEncipherDecipher));
        }
    }

    public bool KeyUsageEncipherOnly
    {
        get => _keyUsageEncipherOnly;
        set
        {
            if (!SetProperty(ref _keyUsageEncipherOnly, value)) return;
            if (value) _keyUsageDecipherOnly = false;
            OnPropertyChanged(nameof(KeyUsageDecipherOnly));
        }
    }

    public bool KeyUsageDecipherOnly
    {
        get => _keyUsageDecipherOnly;
        set
        {
            if (!SetProperty(ref _keyUsageDecipherOnly, value)) return;
            if (value) _keyUsageEncipherOnly = false;
            OnPropertyChanged(nameof(KeyUsageEncipherOnly));
        }
    }

    public bool CanSetEncipherDecipher => _keyUsageKeyAgreement;

    public bool EkuServerAuth
    {
        get => _ekuServerAuth;
        set => SetProperty(ref _ekuServerAuth, value);
    }

    public bool EkuClientAuth
    {
        get => _ekuClientAuth;
        set => SetProperty(ref _ekuClientAuth, value);
    }

    public bool IsCreating
    {
        get => _isCreating;
        private set
        {
            if (SetProperty(ref _isCreating, value))
            {
                OnPropertyChanged(nameof(IsNotCreating));
                ((RelayCommand)AddSanCommand).RaiseCanExecuteChanged();
                Notify();
            }
        }
    }

    public bool IsNotCreating => !_isCreating;

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => _errorMessage is not null;

    public bool CanSubmit =>
        !_isCreating &&
        !string.IsNullOrWhiteSpace(_commonName) &&
        _validityDays > 0 &&
        !string.IsNullOrEmpty(_issuerId);

    public void Initialize(string issuerId, string issuerName)
    {
        _issuerId = issuerId;
        CommonName = string.Empty;
        NewSanValue = string.Empty;
        NewSanType = "DNS";
        SanEntries.Clear();
        OnPropertyChanged(nameof(HasSanEntries));
        ValidityDays = 397;
        KeyBits = 2048;
        KeyUsageDigitalSignature = true;
        KeyUsageNonRepudiation = false;
        KeyUsageKeyEncipherment = true;
        KeyUsageDataEncipherment = false;
        KeyUsageKeyAgreement = false;
        KeyUsageEncipherOnly = false;
        KeyUsageDecipherOnly = false;
        EkuServerAuth = false;
        EkuClientAuth = false;
        ErrorMessage = null;
        IsCreating = false;
        IssuerName = issuerName;
    }

    private async Task SubmitAsync()
    {
        if (!CanSubmit) return;
        IsCreating = true;
        ErrorMessage = null;
        try
        {
            var stored = await _forge.ForgeAsync(new ForgeRequest(
                Mode: ForgeMode.Child,
                CommonName: _commonName.Trim(),
                ValidityDays: _validityDays,
                KeyBits: _keyBits,
                IssuerId: _issuerId,
                Sans: SanEntries.Select(e => $"{e.Type}:{e.Value}").ToArray(),
                InstallInTrustStore: false,
                KeyUsageDigitalSignature: _keyUsageDigitalSignature,
                KeyUsageKeyEncipherment: _keyUsageKeyEncipherment,
                KeyUsageNonRepudiation: _keyUsageNonRepudiation,
                KeyUsageDataEncipherment: _keyUsageDataEncipherment,
                KeyUsageKeyAgreement: _keyUsageKeyAgreement,
                KeyUsageEncipherOnly: _keyUsageEncipherOnly,
                KeyUsageDecipherOnly: _keyUsageDecipherOnly,
                EkuServerAuth: _ekuServerAuth,
                EkuClientAuth: _ekuClientAuth));
            Created?.Invoke(this, stored);
            IsCreating = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            IsCreating = false;
        }
    }

    private void AddSan()
    {
        var value = _newSanValue.Trim();
        if (string.IsNullOrEmpty(value)) return;
        SanEntries.Add(new SanEntryViewModel(_newSanType, value, RemoveSan));
        NewSanValue = string.Empty;
        OnPropertyChanged(nameof(HasSanEntries));
    }

    private void RemoveSan(SanEntryViewModel entry)
    {
        SanEntries.Remove(entry);
        OnPropertyChanged(nameof(HasSanEntries));
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(IsCreating));
        ((AsyncRelayCommand)CreateCommand).RaiseCanExecuteChanged();
    }
}
