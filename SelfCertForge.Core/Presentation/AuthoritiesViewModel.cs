using System.Collections.ObjectModel;
using System.Windows.Input;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Presentation;

public sealed class AuthoritiesViewModel : ObservableObject
{
    private readonly ICertificateStore _store;
    private readonly ICreateRootDialog _createRootDialog;
    private readonly ICreateSignedCertDialog _createSignedCertDialog;
    private readonly INavigationService _nav;
    private readonly ICertificateExportService? _exportService;
    private readonly IFolderPicker? _folderPicker;
    private readonly IPfxPasswordDialog? _pfxPasswordDialog;
    private readonly IConfirmationDialog? _confirmationDialog;
    private readonly ITrustStoreChecker? _trustChecker;

    private string _searchText = string.Empty;
    private AuthorityRowViewModel? _selectedRow;
    private bool _isExportMenuOpen;

    public AuthoritiesViewModel(
        ICertificateStore store,
        ICreateRootDialog createRootDialog,
        ICreateSignedCertDialog createSignedCertDialog,
        INavigationService nav,
        ICertificateExportService exportService,
        IFolderPicker folderPicker,
        IPfxPasswordDialog pfxPasswordDialog,
        IConfirmationDialog confirmationDialog,
        ITrustStoreChecker? trustChecker = null)
    {
        _store = store;
        _createRootDialog = createRootDialog;
        _createSignedCertDialog = createSignedCertDialog;
        _nav = nav;
        _exportService = exportService;
        _folderPicker = folderPicker;
        _pfxPasswordDialog = pfxPasswordDialog;
        _confirmationDialog = confirmationDialog;
        _trustChecker = trustChecker;
        _store.Changed += (_, _) => Refresh();
        Refresh();

        CreateRootCommand = new RelayCommand(() => _ = _createRootDialog.ShowAsync());

        ToggleExportMenuCommand = new RelayCommand(() => IsExportMenuOpen = !_isExportMenuOpen);
        CloseExportMenuCommand = new RelayCommand(() => IsExportMenuOpen = false);

        ExportKeyPemCommand = new AsyncRelayCommand(
            execute: ExportKeyPemAsync,
            canExecute: () => HasSelection && _exportService is not null && _folderPicker is not null);

        ExportPfxCommand = new AsyncRelayCommand(
            execute: ExportPfxAsync,
            canExecute: () => HasSelection && _exportService is not null && _pfxPasswordDialog is not null);

        ExportDerCommand = new AsyncRelayCommand(
            execute: ExportDerAsync,
            canExecute: () => HasSelection && _exportService is not null && _folderPicker is not null);

        ExportP7bCommand = new AsyncRelayCommand(
            execute: ExportP7bAsync,
            canExecute: () => HasSelection && _exportService is not null && _folderPicker is not null);

        DeleteCommand = new AsyncRelayCommand(
            execute: DeleteAsync,
            canExecute: () => HasSelection && _confirmationDialog is not null);

        AddToTrustStoreCommand = new AsyncRelayCommand(
            execute: AddToTrustStoreAsync,
            canExecute: () => HasSelection && _trustChecker is not null && _selectedRow?.PillKind == "uninstalled");
    }

    public ICommand CreateRootCommand { get; }
    public ICommand ToggleExportMenuCommand { get; }
    public ICommand CloseExportMenuCommand { get; }
    public ICommand ExportKeyPemCommand { get; }
    public ICommand ExportPfxCommand { get; }
    public ICommand ExportDerCommand { get; }
    public ICommand ExportP7bCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand AddToTrustStoreCommand { get; }

    public bool IsExportMenuOpen
    {
        get => _isExportMenuOpen;
        set => SetProperty(ref _isExportMenuOpen, value);
    }

    public ObservableCollection<AuthorityRowViewModel> Rows { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                Refresh();
                OnPropertyChanged(nameof(HasNoMatches));
            }
        }
    }

    public AuthorityRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            var prev = _selectedRow;
            if (SetProperty(ref _selectedRow, value))
            {
                if (prev is not null) prev.IsSelected = false;
                if (_selectedRow is not null) _selectedRow.IsSelected = true;
                IsExportMenuOpen = false;
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(SelectedDetail));
                OnPropertyChanged(nameof(SelectedIsNotTrusted));
                ((AsyncRelayCommand)ExportKeyPemCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)ExportPfxCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)ExportDerCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)ExportP7bCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)DeleteCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)AddToTrustStoreCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsEmpty => Rows.Count == 0 && string.IsNullOrEmpty(_searchText);
    public bool HasContent => _store.All.Any(c => c.Kind == StoredCertificateKind.Root);
    public bool HasSelection => _selectedRow is not null;
    public bool HasNoMatches => Rows.Count == 0 && !string.IsNullOrEmpty(_searchText);
    public bool SelectedIsNotTrusted => _selectedRow?.PillKind == "uninstalled";

    public AuthorityDetailViewModel? SelectedDetail =>
        _selectedRow is null ? null
            : new AuthorityDetailViewModel(_selectedRow.Source, _selectedRow.PillKind, _selectedRow.CreateSignedCertCommand);

    public Task LoadAsync(CancellationToken ct = default) => _store.LoadAsync(ct);

    private async Task DeleteAsync()
    {
        IsExportMenuOpen = false;
        if (_selectedRow is null || _confirmationDialog is null) return;
        var confirmed = await _confirmationDialog.ShowAsync(
            "Delete Root Authority",
            $"Are you sure you want to delete \"{_selectedRow.CommonName}\"? This cannot be undone.",
            confirmLabel: "Delete",
            cancelLabel: "Cancel");
        if (!confirmed) return;
        await _store.RemoveAsync(_selectedRow.Id);
    }

    private async Task AddToTrustStoreAsync()
    {
        IsExportMenuOpen = false;
        if (_selectedRow is null || _trustChecker is null || _confirmationDialog is null) return;

        var certPath = _selectedRow.Source.CertificatePath;
        if (string.IsNullOrEmpty(certPath))
        {
            await _confirmationDialog.ShowAsync(
                "Cannot Add to Trusted Roots",
                "No certificate file path is recorded for this authority. Re-create the root to generate a usable file.",
                confirmLabel: "OK",
                cancelLabel: "");
            return;
        }

        var (success, errorMessage) = await _trustChecker.InstallAsync(certPath);
        if (!success)
        {
            await _confirmationDialog.ShowAsync(
                "Failed to Add to Trusted Roots",
                errorMessage ?? "An unknown error occurred.",
                confirmLabel: "OK",
                cancelLabel: "");
            return;
        }

        Refresh();
    }

    private async Task ExportKeyPemAsync()
    {
        IsExportMenuOpen = false;
        if (_selectedRow is null || _folderPicker is null || _exportService is null) return;
        var folder = await _folderPicker.PickAsync();
        if (folder is null) return;
        await _exportService.ExportKeyPemAsync(_selectedRow.Source, folder);
    }

    private async Task ExportPfxAsync()
    {
        IsExportMenuOpen = false;
        if (_selectedRow is null || _pfxPasswordDialog is null || _folderPicker is null || _exportService is null) return;
        var (confirmed, password) = await _pfxPasswordDialog.ShowAsync();
        if (!confirmed) return;
        var folder = await _folderPicker.PickAsync();
        if (folder is null) return;
        await _exportService.ExportPfxAsync(_selectedRow.Source, folder, password);
    }

    private async Task ExportDerAsync()
    {
        IsExportMenuOpen = false;
        if (_selectedRow is null || _folderPicker is null || _exportService is null) return;
        var folder = await _folderPicker.PickAsync();
        if (folder is null) return;
        await _exportService.ExportDerAsync(_selectedRow.Source, folder);
    }

    private async Task ExportP7bAsync()
    {
        IsExportMenuOpen = false;
        if (_selectedRow is null || _folderPicker is null || _exportService is null) return;
        var folder = await _folderPicker.PickAsync();
        if (folder is null) return;
        await _exportService.ExportP7bAsync(_selectedRow.Source, folder);
    }

    private void Refresh()
    {
        var query = _searchText.Trim();
        var roots = _store.All.Where(c => c.Kind == StoredCertificateKind.Root);

        if (query.Length > 0)
            roots = roots.Where(c => c.CommonName.Contains(query, StringComparison.OrdinalIgnoreCase));

        var rows = roots
            .OrderBy(c => c.CommonName, StringComparer.OrdinalIgnoreCase)
            .Select(c => new AuthorityRowViewModel(c, _trustChecker?.IsTrusted(c.Sha1) ?? false, _createSignedCertDialog, _nav))
            .ToList();

        Rows.Clear();
        foreach (var r in rows) Rows.Add(r);

        if (_selectedRow is not null)
        {
            var match = Rows.FirstOrDefault(r => r.Id == _selectedRow.Id);
            if (!ReferenceEquals(match, _selectedRow))
                SelectedRow = match;
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasContent));
        OnPropertyChanged(nameof(HasNoMatches));
        OnPropertyChanged(nameof(SelectedIsNotTrusted));
        (AddToTrustStoreCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }
}

public sealed class AuthorityRowViewModel : ObservableObject
{
    private bool _isSelected;

    public AuthorityRowViewModel(StoredCertificate source, bool isTrusted, ICreateSignedCertDialog createSignedCertDialog, INavigationService nav)
    {
        Source = source;
        Id = source.Id;
        CommonName = source.CommonName;
        PillKind = CertificateStatus.DeriveRootKind(source, isTrusted);
        PillLabel = PillKind == "installed" ? "Trusted" : "Not Trusted";
        CreateSignedCertCommand = new RelayCommand(async () =>
        {
            var cert = await createSignedCertDialog.ShowAsync(source.Id, source.CommonName);
            if (cert is not null)
                nav.NavigateToCertificate(cert.Id);
        });
    }

    internal StoredCertificate Source { get; }

    public string Id { get; }
    public string CommonName { get; }
    public string PillKind { get; }
    public string ExpirationLabel => $"Expires {Source.ExpiresAt.ToLocalTime():yyyy-MM-dd}";
    public string PillLabel { get; }
    public ICommand CreateSignedCertCommand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string ExpiresLabel
    {
        get
        {
            var days = (int)Math.Round((Source.ExpiresAt - DateTimeOffset.UtcNow).TotalDays);
            return days < 0 ? $"{-days}d ago" : $"{days}d";
        }
    }
}

public sealed class AuthorityDetailViewModel
{
    internal AuthorityDetailViewModel(StoredCertificate source, string pillKind, ICommand createSignedCertCommand)
    {
        CommonName = source.CommonName;
        Subject = source.Subject;
        Algorithm = source.Algorithm;
        Serial = source.Serial;
        Sha256 = source.Sha256;
        Sha1 = source.Sha1;
        IssuedAtLabel = source.IssuedAt.ToLocalTime().ToString("yyyy-MM-dd");
        ExpiresAtLabel = source.ExpiresAt.ToLocalTime().ToString("yyyy-MM-dd");
        PillKind = pillKind;
        PillLabel = pillKind switch
        {
            "installed"   => "Trusted",
            "uninstalled" => "Not Trusted",
            _             => pillKind,
        };
        CreateSignedCertCommand = createSignedCertCommand;
    }

    public string CommonName { get; }
    public string Subject { get; }
    public string Algorithm { get; }
    public string Serial { get; }
    public string Sha256 { get; }
    public string Sha1 { get; }
    public string IssuedAtLabel { get; }
    public string ExpiresAtLabel { get; }
    public string PillKind { get; }
    public string PillLabel { get; }
    public ICommand CreateSignedCertCommand { get; }
}
