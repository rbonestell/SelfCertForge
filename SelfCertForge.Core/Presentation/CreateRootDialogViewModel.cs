using System.Windows.Input;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Presentation;

public sealed class CreateRootDialogViewModel : ObservableObject
{
    private readonly IForgeService _forge;

    private string _commonName = string.Empty;
    private string _emailAddress = string.Empty;
    private string _organization = string.Empty;
    private string _organizationalUnit = string.Empty;
    private string _locality = string.Empty;
    private string _stateOrProvince = string.Empty;
    private string _country = string.Empty;
    private int _validityDays = 9125;
    private int _keyBits = 2048;
    private bool _isCreating;
    private string? _errorMessage;

    public CreateRootDialogViewModel(IForgeService forge)
    {
        _forge = forge;
        CreateCommand = new AsyncRelayCommand(SubmitAsync, () => CanSubmit);
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler<StoredCertificate>? Created;
    public event EventHandler? CancelRequested;

    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }

    public string CommonName
    {
        get => _commonName;
        set { if (SetProperty(ref _commonName, value)) Notify(); }
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
        set => SetProperty(ref _validityDays, value);
    }

    public int KeyBits
    {
        get => _keyBits;
        set => SetProperty(ref _keyBits, value);
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
        !string.IsNullOrWhiteSpace(_commonName) &&
        _validityDays > 0;

    public void Reset()
    {
        CommonName = string.Empty;
        EmailAddress = string.Empty;
        Organization = string.Empty;
        OrganizationalUnit = string.Empty;
        Locality = string.Empty;
        StateOrProvince = string.Empty;
        Country = string.Empty;
        ValidityDays = 9125;
        KeyBits = 2048;
        ErrorMessage = null;
        IsCreating = false;
    }

    private async Task SubmitAsync()
    {
        if (!CanSubmit) return;
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
                Country: NullIfBlank(_country)));
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
