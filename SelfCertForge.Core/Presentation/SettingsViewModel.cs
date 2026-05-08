using System.Reflection;
using System.Windows.Input;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Presentation;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly IUpdateService _updateService;

    private bool _isCheckingForUpdate;
    private bool _isUpdateAvailable;
    private bool _isDownloading;
    private int _downloadProgress;
    private UpdateInfo? _availableUpdate;
    private string? _updateStatusMessage;

    public SettingsViewModel(IUpdateService updateService)
    {
        _updateService = updateService;

        var raw = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3);
        CurrentVersion = raw?.Split('+')[0] ?? "1.0.0";

        CheckForUpdateCommand = new AsyncCommand(CheckForUpdateAsync,
            () => !_isCheckingForUpdate && !_isDownloading);

        DownloadAndInstallCommand = new AsyncCommand(DownloadAndInstallAsync,
            () => _isUpdateAvailable && !_isDownloading && !_isCheckingForUpdate);
    }

    public string CurrentVersion { get; }

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
}
