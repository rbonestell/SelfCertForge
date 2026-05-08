using System.Windows.Input;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.App.Shell;

public sealed class ShellViewModel : ObservableObject
{
    private const string LastUpdateCheckKey = "LastUpdateCheckUtc";

    private readonly SettingsViewModel _settings;
    private AppRoute _currentRoute = AppRoute.Dashboard;
    private bool _isUpdateAvailable;

    public ShellViewModel(SettingsViewModel settings)
    {
        _settings = settings;
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

        _ = CheckForUpdateIfStaleAsync();
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
                && DateTimeOffset.UtcNow - lastCheck < TimeSpan.FromHours(24))
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
