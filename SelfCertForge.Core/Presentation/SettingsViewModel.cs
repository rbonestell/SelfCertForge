using System.Reflection;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Presentation;

public sealed class SettingsViewModel : ObservableObject
{
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan SavedToastDuration = TimeSpan.FromMilliseconds(1500);

    private readonly IUpdateService _updateService;
    private readonly IUserPreferencesStore? _preferencesStore;
    private readonly IActivityLog? _activityLog;
    private readonly IDataFolderService? _dataFolderService;
    private readonly IConfirmationDialog? _confirmationDialog;
    private readonly IGithubReleaseService? _githubRelease;

    // Update fields ----------------------------------------------------------
    private bool _isCheckingForUpdate;
    private bool _isUpdateAvailable;
    private bool _isDownloading;
    private int _downloadProgress;
    private UpdateInfo? _availableUpdate;
    private string? _updateStatusMessage;

    // Preferences fields (local mirror of the store) -------------------------
    private int _rootValidityDays;
    private int _signedValidityDays;
    private int _keyBits;
    private HashAlgorithmKind _hashAlgorithm;
    private string _defaultOrganization = string.Empty;
    private string _defaultOrganizationalUnit = string.Empty;
    private string _defaultLocality = string.Empty;
    private string _defaultStateOrProvince = string.Empty;
    private string _defaultCountry = string.Empty;
    private string _defaultEmail = string.Empty;
    private ActivityRetention _activityRetention;

    // Auto-save bookkeeping --------------------------------------------------
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private bool _showSavedDefaults;
    private bool _showSavedApp;
    private bool _rootValidityHasError;
    private bool _signedValidityHasError;

    /// <summary>
    /// Backwards-compatible single-arg constructor used by existing tests / call
    /// sites that only need update functionality. Preferences are not persisted
    /// in this mode (defaults are used and Save is a no-op).
    /// </summary>
    public SettingsViewModel(IUpdateService updateService)
        : this(updateService, null, null, null, null, null) { }

    public SettingsViewModel(
        IUpdateService updateService,
        IUserPreferencesStore? preferencesStore,
        IActivityLog? activityLog,
        IDataFolderService? dataFolderService,
        IConfirmationDialog? confirmationDialog,
        IGithubReleaseService? githubRelease = null)
    {
        _updateService = updateService;
        _preferencesStore = preferencesStore;
        _activityLog = activityLog;
        _dataFolderService = dataFolderService;
        _confirmationDialog = confirmationDialog;
        _githubRelease = githubRelease;

        var raw = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3);
        CurrentVersion = raw?.Split('+')[0] ?? "1.0.0";

        CheckForUpdateCommand = new AsyncCommand(CheckForUpdateAsync,
            () => !_isCheckingForUpdate && !_isDownloading);

        DownloadAndInstallCommand = new AsyncCommand(DownloadAndInstallAsync,
            () => _isUpdateAvailable && !_isDownloading && !_isCheckingForUpdate);

        RevealDataFolderCommand = new AsyncCommand(RevealDataFolderAsync,
            () => _dataFolderService is not null);

        ClearActivityLogCommand = new AsyncCommand(ClearActivityLogAsync,
            () => _activityLog is not null);

        // Hydrate from store. We suppress save while populating so the initial
        // load doesn't re-persist the same values.
        var prefs = _preferencesStore?.Current ?? UserPreferences.Default;
        _suppressSave = true;
        ApplyPreferences(prefs);
        _suppressSave = false;

        if (_preferencesStore is not null)
            _preferencesStore.Changed += OnPreferencesChangedExternally;

        if (_githubRelease is not null)
        {
            _githubRelease.Changed += OnLatestPublishedVersionChanged;
            // Capture whatever the service may already have (e.g., if a previous
            // refresh completed before this VM was constructed in the same session).
            _latestPublishedVersion = _githubRelease.LatestPublishedVersion;
        }
    }

    public string CurrentVersion { get; }

    private string? _latestPublishedVersion;

    /// <summary>
    /// Latest version published to the GitHub releases feed. Null until the
    /// first successful fetch. Purely informational — see
    /// <see cref="IGithubReleaseService"/> for why this is not used to drive
    /// install decisions.
    /// </summary>
    public string? LatestPublishedVersion
    {
        get => _latestPublishedVersion;
        private set
        {
            if (SetProperty(ref _latestPublishedVersion, value))
            {
                OnPropertyChanged(nameof(HasLatestPublishedVersion));
                OnPropertyChanged(nameof(IsOnLatestPublishedVersion));
            }
        }
    }

    public bool HasLatestPublishedVersion => !string.IsNullOrEmpty(_latestPublishedVersion);

    /// <summary>True when the installed version matches the latest published version (used for dim styling).</summary>
    public bool IsOnLatestPublishedVersion =>
        HasLatestPublishedVersion &&
        string.Equals(CurrentVersion, _latestPublishedVersion, StringComparison.OrdinalIgnoreCase);

    // -- Updates -------------------------------------------------------------

    public bool IsCheckingForUpdate
    {
        get => _isCheckingForUpdate;
        private set
        {
            if (SetProperty(ref _isCheckingForUpdate, value))
            {
                CheckForUpdateCommand.RaiseCanExecuteChanged();
                DownloadAndInstallCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set
        {
            if (SetProperty(ref _isUpdateAvailable, value))
                DownloadAndInstallCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        private set
        {
            if (SetProperty(ref _isDownloading, value))
            {
                CheckForUpdateCommand.RaiseCanExecuteChanged();
                DownloadAndInstallCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int DownloadProgress
    {
        get => _downloadProgress;
        private set
        {
            if (SetProperty(ref _downloadProgress, value))
                OnPropertyChanged(nameof(DownloadProgressNormalized));
        }
    }

    public UpdateInfo? AvailableUpdate
    {
        get => _availableUpdate;
        private set => SetProperty(ref _availableUpdate, value);
    }

    public string? UpdateStatusMessage
    {
        get => _updateStatusMessage;
        private set
        {
            if (SetProperty(ref _updateStatusMessage, value))
                OnPropertyChanged(nameof(HasUpdateStatusMessage));
        }
    }

    public bool HasUpdateStatusMessage => !string.IsNullOrEmpty(_updateStatusMessage);

    public double DownloadProgressNormalized => _downloadProgress / 100.0;

    public AsyncCommand CheckForUpdateCommand { get; }
    public AsyncCommand DownloadAndInstallCommand { get; }
    public AsyncCommand RevealDataFolderCommand { get; }
    public AsyncCommand ClearActivityLogCommand { get; }

    // -- Certificate defaults ------------------------------------------------

    public int RootValidityDays
    {
        get => _rootValidityDays;
        set
        {
            if (SetProperty(ref _rootValidityDays, value))
            {
                RootValidityHasError = value <= 0;
                if (!RootValidityHasError) ScheduleSave(SettingsCardSection.CertificateDefaults);
            }
        }
    }

    public bool RootValidityHasError
    {
        get => _rootValidityHasError;
        private set => SetProperty(ref _rootValidityHasError, value);
    }

    public int SignedValidityDays
    {
        get => _signedValidityDays;
        set
        {
            if (SetProperty(ref _signedValidityDays, value))
            {
                SignedValidityHasError = value <= 0;
                if (!SignedValidityHasError) ScheduleSave(SettingsCardSection.CertificateDefaults);
            }
        }
    }

    public bool SignedValidityHasError
    {
        get => _signedValidityHasError;
        private set => SetProperty(ref _signedValidityHasError, value);
    }

    public int KeyBits
    {
        get => _keyBits;
        set
        {
            if (SetProperty(ref _keyBits, value))
            {
                OnPropertyChanged(nameof(IsKeyBits2048));
                OnPropertyChanged(nameof(IsKeyBits3072));
                OnPropertyChanged(nameof(IsKeyBits4096));
                ScheduleSave(SettingsCardSection.CertificateDefaults);
            }
        }
    }

    public bool IsKeyBits2048 => _keyBits == 2048;
    public bool IsKeyBits3072 => _keyBits == 3072;
    public bool IsKeyBits4096 => _keyBits == 4096;

    public HashAlgorithmKind HashAlgorithm
    {
        get => _hashAlgorithm;
        set
        {
            if (SetProperty(ref _hashAlgorithm, value))
            {
                OnPropertyChanged(nameof(IsHashSha256));
                OnPropertyChanged(nameof(IsHashSha384));
                OnPropertyChanged(nameof(IsHashSha512));
                ScheduleSave(SettingsCardSection.CertificateDefaults);
            }
        }
    }

    public bool IsHashSha256 => _hashAlgorithm == HashAlgorithmKind.Sha256;
    public bool IsHashSha384 => _hashAlgorithm == HashAlgorithmKind.Sha384;
    public bool IsHashSha512 => _hashAlgorithm == HashAlgorithmKind.Sha512;

    public string DefaultOrganization
    {
        get => _defaultOrganization;
        set { if (SetProperty(ref _defaultOrganization, value ?? string.Empty)) ScheduleSave(SettingsCardSection.CertificateDefaults); }
    }

    public string DefaultOrganizationalUnit
    {
        get => _defaultOrganizationalUnit;
        set { if (SetProperty(ref _defaultOrganizationalUnit, value ?? string.Empty)) ScheduleSave(SettingsCardSection.CertificateDefaults); }
    }

    public string DefaultLocality
    {
        get => _defaultLocality;
        set { if (SetProperty(ref _defaultLocality, value ?? string.Empty)) ScheduleSave(SettingsCardSection.CertificateDefaults); }
    }

    public string DefaultStateOrProvince
    {
        get => _defaultStateOrProvince;
        set { if (SetProperty(ref _defaultStateOrProvince, value ?? string.Empty)) ScheduleSave(SettingsCardSection.CertificateDefaults); }
    }

    public string DefaultCountry
    {
        get => _defaultCountry;
        set { if (SetProperty(ref _defaultCountry, value ?? string.Empty)) ScheduleSave(SettingsCardSection.CertificateDefaults); }
    }

    public string DefaultEmail
    {
        get => _defaultEmail;
        set { if (SetProperty(ref _defaultEmail, value ?? string.Empty)) ScheduleSave(SettingsCardSection.CertificateDefaults); }
    }

    // -- Application ---------------------------------------------------------

    public ActivityRetention ActivityRetention
    {
        get => _activityRetention;
        set
        {
            if (SetProperty(ref _activityRetention, value))
            {
                OnPropertyChanged(nameof(IsRetention100));
                OnPropertyChanged(nameof(IsRetention500));
                OnPropertyChanged(nameof(IsRetention1000));
                OnPropertyChanged(nameof(IsRetentionUnlimited));
                ScheduleSave(SettingsCardSection.Application);
            }
        }
    }

    public bool IsRetention100       => _activityRetention == ActivityRetention.OneHundred;
    public bool IsRetention500       => _activityRetention == ActivityRetention.FiveHundred;
    public bool IsRetention1000      => _activityRetention == ActivityRetention.OneThousand;
    public bool IsRetentionUnlimited => _activityRetention == ActivityRetention.Unlimited;

    public string DataFolderPath => _dataFolderService?.DataFolderPath ?? string.Empty;
    public string RevealLabel    => _dataFolderService?.RevealLabel ?? "Reveal";
    public bool   HasDataFolder  => _dataFolderService is not null;
    public bool   CanClearLog    => _activityLog is not null;

    /// <summary>True briefly after a Certificate Defaults change is persisted.</summary>
    public bool ShowSavedDefaults
    {
        get => _showSavedDefaults;
        private set => SetProperty(ref _showSavedDefaults, value);
    }

    /// <summary>True briefly after an Application-card change is persisted.</summary>
    public bool ShowSavedApp
    {
        get => _showSavedApp;
        private set => SetProperty(ref _showSavedApp, value);
    }

    // -- Update commands -----------------------------------------------------

    public async Task CheckForUpdateAsync()
    {
        if (IsCheckingForUpdate) return;

        IsCheckingForUpdate = true;
        UpdateStatusMessage = null;

        try
        {
            var update = await _updateService.CheckForUpdateAsync();
            if (update is not null)
            {
                AvailableUpdate = update;
                IsUpdateAvailable = true;
                UpdateStatusMessage = $"Version {update.Version} is available.";
            }
            else
            {
                IsUpdateAvailable = false;
                AvailableUpdate = null;
                UpdateStatusMessage = "You're on the latest version.";
            }
        }
        catch
        {
            UpdateStatusMessage = "Update check failed. Check your internet connection.";
        }
        finally
        {
            IsCheckingForUpdate = false;
        }
    }

    private async Task DownloadAndInstallAsync()
    {
        if (AvailableUpdate is null || IsDownloading) return;

        IsDownloading = true;
        DownloadProgress = 0;
        UpdateStatusMessage = "Downloading update…";

        try
        {
            var progress = new Progress<int>(p =>
            {
                DownloadProgress = p;
                UpdateStatusMessage = $"Downloading update… {p}%";
            });

            await _updateService.DownloadUpdateAsync(AvailableUpdate, progress);
            UpdateStatusMessage = "Applying update and restarting…";
            await _updateService.ApplyUpdateAndRestartAsync(AvailableUpdate);
        }
        catch
        {
            UpdateStatusMessage = "Download failed. Please try again.";
            IsDownloading = false;
        }
    }

    // -- App-section commands ------------------------------------------------

    private async Task RevealDataFolderAsync()
    {
        if (_dataFolderService is null) return;
        try { await _dataFolderService.RevealAsync(); }
        catch { /* swallow — best-effort UX action */ }
    }

    private async Task ClearActivityLogAsync()
    {
        if (_activityLog is null) return;

        var confirmed = _confirmationDialog is null ||
            await _confirmationDialog.ShowAsync(
                title: "Clear activity log?",
                message: "This permanently removes all entries from the activity log on this device. Certificates are not affected.",
                confirmLabel: "Clear",
                cancelLabel: "Cancel");

        if (!confirmed) return;

        await _activityLog.ClearAsync();
    }

    // -- Auto-save plumbing --------------------------------------------------

    private void ApplyPreferences(UserPreferences prefs)
    {
        RootValidityDays         = prefs.RootValidityDays;
        SignedValidityDays       = prefs.SignedValidityDays;
        KeyBits                  = prefs.KeyBits;
        HashAlgorithm            = prefs.HashAlgorithm;
        DefaultOrganization      = prefs.DefaultOrganization      ?? string.Empty;
        DefaultOrganizationalUnit = prefs.DefaultOrganizationalUnit ?? string.Empty;
        DefaultLocality          = prefs.DefaultLocality          ?? string.Empty;
        DefaultStateOrProvince   = prefs.DefaultStateOrProvince   ?? string.Empty;
        DefaultCountry           = prefs.DefaultCountry           ?? string.Empty;
        DefaultEmail             = prefs.DefaultEmail             ?? string.Empty;
        ActivityRetention        = prefs.ActivityRetention;
    }

    private UserPreferences BuildPreferences() => new()
    {
        RootValidityDays           = _rootValidityDays,
        SignedValidityDays         = _signedValidityDays,
        KeyBits                    = _keyBits,
        HashAlgorithm              = _hashAlgorithm,
        DefaultOrganization        = NullIfBlank(_defaultOrganization),
        DefaultOrganizationalUnit  = NullIfBlank(_defaultOrganizationalUnit),
        DefaultLocality            = NullIfBlank(_defaultLocality),
        DefaultStateOrProvince     = NullIfBlank(_defaultStateOrProvince),
        DefaultCountry             = NullIfBlank(_defaultCountry),
        DefaultEmail               = NullIfBlank(_defaultEmail),
        ActivityRetention          = _activityRetention,
    };

    private void ScheduleSave(SettingsCardSection section)
    {
        if (_suppressSave || _preferencesStore is null) return;

        // Cancel the prior in-flight debounce, but DO NOT dispose the old CTS:
        // the awaited Task.Delay inside SaveAfterDebounceAsync still references
        // its token. Disposing here can race with cancellation and surface as
        // ObjectDisposedException on a thread-pool thread. Old CTS is GC'd once
        // the awaiting task observes cancellation and exits.
        var previous = _saveCts;
        _saveCts = new CancellationTokenSource();
        previous?.Cancel();
        var token = _saveCts.Token;

        // The "Saved" toast is scoped to whichever card the most recent edit
        // came from — if the user toggles defaults then app retention, the
        // toast appears in the Application card only.
        _ = SaveAfterDebounceAsync(token, section);
    }

    private async Task SaveAfterDebounceAsync(CancellationToken token, SettingsCardSection section)
    {
        try
        {
            await Task.Delay(SaveDebounce, token);
            if (token.IsCancellationRequested) return;
            await SaveNowAsync();
            SetSavedFlag(section, true);
            await Task.Delay(SavedToastDuration, token);
            if (!token.IsCancellationRequested) SetSavedFlag(section, false);
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
        {
            /* superseded by another ScheduleSave or shutdown */
        }
    }

    private void SetSavedFlag(SettingsCardSection section, bool value)
    {
        if (section == SettingsCardSection.CertificateDefaults) ShowSavedDefaults = value;
        else                                                   ShowSavedApp = value;
    }

    /// <summary>Force an immediate write (used by tests).</summary>
    public Task SaveNowAsync() =>
        _preferencesStore is null ? Task.CompletedTask : _preferencesStore.SaveAsync(BuildPreferences());

    private void OnLatestPublishedVersionChanged(object? sender, string? version)
    {
        LatestPublishedVersion = version;
    }

    private void OnPreferencesChangedExternally(object? sender, UserPreferences prefs)
    {
        // If the change originated from us (we just saved), the values match — no-op.
        if (prefs.Equals(BuildPreferences())) return;
        _suppressSave = true;
        try { ApplyPreferences(prefs); }
        finally { _suppressSave = false; }
    }

    private static string? NullIfBlank(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
