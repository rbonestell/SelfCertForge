using System.Windows.Input;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Presentation;

public sealed class CreateRootDialogViewModel : ObservableObject
{
    private readonly IForgeService _forge;
    private readonly IUserPreferencesStore? _preferences;

    private string _commonName = string.Empty;
    private string _emailAddress = string.Empty;
    private string _organization = string.Empty;
    private string _organizationalUnit = string.Empty;
    private string _locality = string.Empty;
    private string _stateOrProvince = string.Empty;
    private string _country = string.Empty;
    private int _validityDays = 9125;
    private int _keyBits = 2048;
    private HashAlgorithmKind _hashAlgorithm = HashAlgorithmKind.Sha256;
    private bool _isCreating;
    private string? _errorMessage;
    private bool _validityHasError;
    private bool _commonNameHasError;

    public CreateRootDialogViewModel(IForgeService forge)
        : this(forge, null) { }

    public CreateRootDialogViewModel(IForgeService forge, IUserPreferencesStore? preferences)
    {
        _forge = forge;
        _preferences = preferences;
        CreateCommand = new AsyncRelayCommand(SubmitAsync, () => CanSubmit);
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));
        // Seed defaults from prefs so a fresh dialog starts with the user's saved values.
        ApplyDefaultsFromPreferences();
    }

    public event EventHandler<StoredCertificate>? Created;
    public event EventHandler? CancelRequested;

    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }

    public string CommonName
    {
        get => _commonName;
        set
        {
            if (SetProperty(ref _commonName, value))
            {
                // Editing clears the "required" indicator immediately so the
                // user sees a clean field again (same pattern as SAN/validity).
                if (_commonNameHasError) CommonNameHasError = false;
                Notify();
            }
        }
    }

    public bool CommonNameHasError
    {
        get => _commonNameHasError;
        private set => SetProperty(ref _commonNameHasError, value);
    }

    public string EmailAddress
    {
        get => _emailAddress;
        set => SetProperty(ref _emailAddress, value);
    }

    public string Organization
    {
        get => _organization;
        set => SetProperty(ref _organization, value);
    }

    public string OrganizationalUnit
    {
        get => _organizationalUnit;
        set => SetProperty(ref _organizationalUnit, value);
    }

    public string Locality
    {
        get => _locality;
        set => SetProperty(ref _locality, value);
    }

    public string StateOrProvince
    {
        get => _stateOrProvince;
        set => SetProperty(ref _stateOrProvince, value);
    }

    public string Country
    {
        get => _country;
        set => SetProperty(ref _country, value);
    }

    public int ValidityDays
    {
        get => _validityDays;
        set
        {
            if (SetProperty(ref _validityDays, value))
            {
                ValidityHasError = value <= 0;
                Notify();
            }
        }
    }

    public bool ValidityHasError
    {
        get => _validityHasError;
        private set => SetProperty(ref _validityHasError, value);
    }

    public int KeyBits
    {
        get => _keyBits;
        set => SetProperty(ref _keyBits, value);
    }

    public HashAlgorithmKind HashAlgorithm
    {
        get => _hashAlgorithm;
        set => SetProperty(ref _hashAlgorithm, value);
    }

    public bool IsCreating
    {
        get => _isCreating;
        private set { if (SetProperty(ref _isCreating, value)) { OnPropertyChanged(nameof(IsNotCreating)); Notify(); } }
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
        _validityDays > 0;

    public void Reset()
    {
        CommonName = string.Empty;
        ErrorMessage = null;
        ValidityHasError = false;
        CommonNameHasError = false;
        IsCreating = false;
        ApplyDefaultsFromPreferences();
    }

    private void ApplyDefaultsFromPreferences()
    {
        var p = _preferences?.Current ?? UserPreferences.Default;
        EmailAddress       = p.DefaultEmail              ?? string.Empty;
        Organization       = p.DefaultOrganization       ?? string.Empty;
        OrganizationalUnit = p.DefaultOrganizationalUnit ?? string.Empty;
        Locality           = p.DefaultLocality           ?? string.Empty;
        StateOrProvince    = p.DefaultStateOrProvince    ?? string.Empty;
        Country            = p.DefaultCountry            ?? string.Empty;
        ValidityDays       = p.RootValidityDays;
        KeyBits            = p.KeyBits;
        HashAlgorithm      = p.HashAlgorithm;
    }

    private async Task SubmitAsync()
    {
        if (!CanSubmit) return;

        // Required-field check matches the SAN-on-Add pattern: error surfaces
        // at action time, the field's border flips red, and the next edit
        // clears it.
        if (string.IsNullOrWhiteSpace(_commonName))
        {
            CommonNameHasError = true;
            return;
        }

        IsCreating = true;
        ErrorMessage = null;
        try
        {
            var cn = _commonName.Trim();
            var stored = await _forge.ForgeAsync(new ForgeRequest(
                Mode: ForgeMode.Root,
                CommonName: cn,
                ValidityDays: _validityDays,
                KeyBits: _keyBits,
                IssuerId: null,
                Sans: [],
                InstallInTrustStore: false,
                EmailAddress: NullIfBlank(_emailAddress),
                Organization: NullIfBlank(_organization),
                OrganizationalUnit: NullIfBlank(_organizationalUnit),
                Locality: NullIfBlank(_locality),
                StateOrProvince: NullIfBlank(_stateOrProvince),
                Country: NullIfBlank(_country),
                HashAlgorithm: _hashAlgorithm));
            Created?.Invoke(this, stored);
            IsCreating = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            IsCreating = false;
        }
    }

    private static string? NullIfBlank(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private void Notify()
    {
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(IsCreating));
        ((AsyncRelayCommand)CreateCommand).RaiseCanExecuteChanged();
    }
}
