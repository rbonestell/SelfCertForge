using System.Windows.Input;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.App.Shell;

public sealed class ShellViewModel : ObservableObject
{
    private const string LastUpdateCheckKey = "LastUpdateCheckUtc";
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(6);

    private readonly SettingsViewModel _settings;
    private readonly IGithubReleaseService? _githubRelease;
    private AppRoute _currentRoute = AppRoute.Dashboard;
    private bool _isUpdateAvailable;

    public ShellViewModel(SettingsViewModel settings, IGithubReleaseService? githubRelease = null)
    {
        _settings = settings;
        _githubRelease = githubRelease;
        _settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.IsUpdateAvailable))
                IsUpdateAvailable = _settings.IsUpdateAvailable;
        };

        NavigateCommand = new RelayCommand<string>(name =>
        {
            if (Enum.TryParse<AppRoute>(name, ignoreCase: false, out var route))
            {
                CurrentRoute = route;
                // Clear the badge once the user navigates to Settings.
                if (route == AppRoute.Settings)
                    IsUpdateAvailable = false;
            }
        });

        // Velopack check is gated by the 6h stale interval (it has install-side
        // effects and we shouldn't churn the network); the GitHub releases poll
        // is a single cheap REST call so we always run it on launch to keep the
        // "Latest" line in Settings fresh after every restart.
        _ = CheckForUpdateIfStaleAsync();
        if (_githubRelease is not null)
        {
            // If the GitHub poll surfaces a published version that doesn't match the
            // installed one, override the 6h Velopack stale-gate. Without this, an
            // app launched shortly after the last gated check would silently see
            // "Latest v0.1.0" alongside "Installed v0.0.2" with no update CTA — the
            // exact dead-end users reported.
            _githubRelease.Changed += (_, latest) => MaybeForceVelopackCheck(latest);
            // Also handle the case where the GH refresh already completed in a prior
            // VM lifetime (service is a singleton) — the Changed event won't replay.
            MaybeForceVelopackCheck(_githubRelease.LatestPublishedVersion);
            _ = _githubRelease.RefreshAsync();
        }
    }

    private void MaybeForceVelopackCheck(string? latest)
    {
        if (string.IsNullOrEmpty(latest)) return;
        if (string.Equals(latest, _settings.CurrentVersion, StringComparison.OrdinalIgnoreCase)) return;
        _ = _settings.CheckForUpdateAsync();
    }

    public AppRoute CurrentRoute
    {
        get => _currentRoute;
        set
        {
            if (_currentRoute == value) return;
            _currentRoute = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDashboard));
            OnPropertyChanged(nameof(IsAuthorities));
            OnPropertyChanged(nameof(IsCertificates));
            OnPropertyChanged(nameof(IsSettings));
        }
    }

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set => SetProperty(ref _isUpdateAvailable, value);
    }

    public bool IsDashboard => _currentRoute == AppRoute.Dashboard;
    public bool IsAuthorities => _currentRoute == AppRoute.Authorities;
    public bool IsCertificates => _currentRoute == AppRoute.Certificates;
    public bool IsSettings => _currentRoute == AppRoute.Settings;

    public ICommand NavigateCommand { get; }

    public SettingsViewModel Settings => _settings;

    private async Task CheckForUpdateIfStaleAsync()
    {
        try
        {
            var lastCheckStr = Preferences.Get(LastUpdateCheckKey, string.Empty);
            if (DateTimeOffset.TryParse(lastCheckStr, out var lastCheck)
                && DateTimeOffset.UtcNow - lastCheck < UpdateCheckInterval)
            {
                return;
            }

            await _settings.CheckForUpdateAsync();
            Preferences.Set(LastUpdateCheckKey, DateTimeOffset.UtcNow.ToString("O"));
        }
        catch
        {
            // Background check — never crash the app on failure.
        }
    }
}

internal sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    public RelayCommand(Action<T> execute) => _execute = execute;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter)
    {
        if (parameter is T typed) _execute(typed);
    }
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
