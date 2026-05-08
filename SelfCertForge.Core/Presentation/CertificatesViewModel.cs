using System.Collections.ObjectModel;
using System.Windows.Input;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Presentation;

public sealed class CertificatesViewModel : ObservableObject
{
    private readonly ICertificateStore _store;
    private readonly Func<DateTimeOffset> _now;
    private readonly ICertificateExportService? _exportService;
    private readonly IFolderPicker? _folderPicker;
    private readonly IPfxPasswordDialog? _pfxPasswordDialog;
    private readonly IConfirmationDialog? _confirmationDialog;
    private readonly ITrustStoreChecker? _trustChecker;

    private string _searchText = string.Empty;
    private CertificateRowViewModel? _selectedRow;
    private bool _isExportMenuOpen;

    public CertificatesViewModel(
        ICertificateStore store,
        ICertificateExportService exportService,
        IFolderPicker folderPicker,
        IPfxPasswordDialog pfxPasswordDialog,
        IConfirmationDialog confirmationDialog,
        ITrustStoreChecker? trustChecker = null)
        : this(store, () => DateTimeOffset.UtcNow, exportService, folderPicker, pfxPasswordDialog, confirmationDialog, trustChecker) { }

    internal CertificatesViewModel(ICertificateStore store, Func<DateTimeOffset> now,
        ICertificateExportService? exportService = null,
        IFolderPicker? folderPicker = null,
        IPfxPasswordDialog? pfxPasswordDialog = null,
        IConfirmationDialog? confirmationDialog = null,
        ITrustStoreChecker? trustChecker = null)
    {
        _store = store;
        _now = now;
        _exportService = exportService;
        _folderPicker = folderPicker;
        _pfxPasswordDialog = pfxPasswordDialog;
        _confirmationDialog = confirmationDialog;
        _trustChecker = trustChecker;
        _store.Changed += (_, _) => Refresh();
        if (_trustChecker is not null)
            _trustChecker.Changed += (_, _) => Refresh();
        Refresh();

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
    }

    public ICommand ToggleExportMenuCommand { get; }
    public ICommand CloseExportMenuCommand { get; }
    public ICommand ExportKeyPemCommand { get; }
    public ICommand ExportPfxCommand { get; }
    public ICommand ExportDerCommand { get; }
    public ICommand ExportP7bCommand { get; }
    public ICommand DeleteCommand { get; }

    public ObservableCollection<CertificateRowViewModel> Rows { get; } = new();

    public bool IsExportMenuOpen
    {
        get => _isExportMenuOpen;
        set => SetProperty(ref _isExportMenuOpen, value);
    }

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

    public CertificateRowViewModel? SelectedRow
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
                ((AsyncRelayCommand)ExportKeyPemCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)ExportPfxCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)ExportDerCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)ExportP7bCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)DeleteCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelection => _selectedRow is not null;

    public CertificateDetailViewModel? SelectedDetail =>
        _selectedRow is null ? null
            : new CertificateDetailViewModel(_selectedRow.Source, _selectedRow.PillKind, _selectedRow.TrustPillKind);

    public bool IsEmpty => Rows.Count == 0 && string.IsNullOrEmpty(_searchText);
    public bool HasNoMatches => Rows.Count == 0 && !string.IsNullOrEmpty(_searchText);

    public Task LoadAsync(CancellationToken ct = default) => _store.LoadAsync(ct);

    public void SelectById(string id) =>
        SelectedRow = Rows.FirstOrDefault(r => r.Id == id);

    private void Refresh()
    {
        var now = _now();
        var all = _store.All;
        var children = all.Where(c => c.Kind == StoredCertificateKind.Child);
        var query = _searchText?.Trim() ?? string.Empty;
        if (query.Length > 0)
        {
            children = children.Where(c =>
                c.CommonName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.Sans.Any(s => s.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        var rows = children
            .OrderBy(c => c.CommonName, StringComparer.OrdinalIgnoreCase)
            .Select(c =>
            {
                var issuerRoot = c.IssuerId is not null
                    ? all.FirstOrDefault(r => r.Id == c.IssuerId && r.Kind == StoredCertificateKind.Root)
                    : null;
                var isTrusted = issuerRoot is not null && (_trustChecker?.IsTrusted(issuerRoot.Sha1) ?? false);
                return new CertificateRowViewModel(c, CertificateStatus.DeriveChildKind(c, all, now), isTrusted);
            })
            .ToList();

        Rows.Clear();
        foreach (var row in rows) Rows.Add(row);

        if (_selectedRow is not null)
        {
            var match = Rows.FirstOrDefault(r => r.Id == _selectedRow.Id);
            if (!ReferenceEquals(match, _selectedRow))
                SelectedRow = match;
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasNoMatches));
    }

    private async Task DeleteAsync()
    {
        IsExportMenuOpen = false;
        if (_selectedRow is null || _confirmationDialog is null) return;
        var confirmed = await _confirmationDialog.ShowAsync(
            "Delete Certificate",
            $"Are you sure you want to delete \"{_selectedRow.CommonName}\"? This cannot be undone.",
            confirmLabel: "Delete",
            cancelLabel: "Cancel");
        if (!confirmed) return;
        await _store.RemoveAsync(_selectedRow.Id);
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
}

public sealed class CertificateRowViewModel : ObservableObject
{
    private bool _isSelected;

    internal CertificateRowViewModel(StoredCertificate source, string pillKind, bool isTrusted)
    {
        Source = source;
        PillKind = pillKind;
        TrustPillKind = isTrusted ? "installed" : "uninstalled";
    }

    internal StoredCertificate Source { get; }

    public string Id => Source.Id;
    public string CommonName => Source.CommonName;
    public string ExpirationLabel => $"Expires {Source.ExpiresAt.ToLocalTime():yyyy-MM-dd}";
    public string PillKind { get; }
    public string TrustPillKind { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string PillLabel => PillKind switch
    {
        "valid"    => "Valid",
        "expiring" => $"Expires {DaysUntil()}d",
        "expired"  => "Expired",
        "orphaned" => "Orphaned",
        _          => PillKind,
    };

    public string ExpiresLabel
    {
        get
        {
            var days = (int)Math.Round((Source.ExpiresAt - DateTimeOffset.UtcNow).TotalDays);
            return days < 0 ? $"{-days}d ago" : $"{days}d";
        }
    }

    private int DaysUntil() =>
        Math.Max(0, (int)Math.Round((Source.ExpiresAt - DateTimeOffset.UtcNow).TotalDays));
}

public sealed class CertificateDetailViewModel
{
    internal CertificateDetailViewModel(StoredCertificate source, string pillKind, string trustPillKind)
    {
        TrustPillKind = trustPillKind;
        TrustPillLabel = trustPillKind == "installed" ? "Trusted" : "Not Trusted";
        CommonName = source.CommonName;
        Subject = source.Subject;
        IssuerName = source.IssuerName ?? "—";
        Algorithm = source.Algorithm;
        Serial = source.Serial;
        Sha256 = source.Sha256;
        Sha1 = source.Sha1;
        Sans = string.Join(", ", source.Sans);
        IssuedAtLabel = source.IssuedAt.ToLocalTime().ToString("yyyy-MM-dd");
        ExpiresAtLabel = source.ExpiresAt.ToLocalTime().ToString("yyyy-MM-dd");
        PillKind = pillKind;
        PillLabel = pillKind switch
        {
            "valid"    => "Valid",
            "expiring" => "Expiring soon",
            "expired"  => "Expired",
            "orphaned" => "Orphaned",
            _          => pillKind,
        };
        KeyUsages = source.KeyUsages is { Count: > 0 }
            ? string.Join(", ", source.KeyUsages)
            : "—";
        ExtendedKeyUsages = source.ExtendedKeyUsages is { Count: > 0 }
            ? string.Join(", ", source.ExtendedKeyUsages)
            : "—";
        HasKeyUsage = source.KeyUsages is { Count: > 0 } || source.ExtendedKeyUsages is { Count: > 0 };
    }

    public string CommonName { get; }
    public string Subject { get; }
    public string IssuerName { get; }
    public string Algorithm { get; }
    public string Serial { get; }
    public string Sha256 { get; }
    public string Sha1 { get; }
    public string Sans { get; }
    public string IssuedAtLabel { get; }
    public string ExpiresAtLabel { get; }
    public string PillKind { get; }
    public string PillLabel { get; }
    public string TrustPillKind { get; }
    public string TrustPillLabel { get; }
    public string KeyUsages { get; }
    public string ExtendedKeyUsages { get; }
    public bool HasKeyUsage { get; }
}
