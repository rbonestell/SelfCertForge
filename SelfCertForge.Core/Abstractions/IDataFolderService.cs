namespace SelfCertForge.Core.Abstractions;

/// <summary>
/// Abstracts "open the app's data folder in the OS file manager." Lives in
/// Core so the Settings ViewModel stays MAUI-free; implementations live in
/// the platform-specific app project.
/// </summary>
public interface IDataFolderService
{
    /// <summary>Absolute path to the directory holding certificates.json, activity.json, preferences.json.</summary>
    string DataFolderPath { get; }

    /// <summary>Localized label for the reveal action ("Reveal in Finder" / "Reveal in Explorer").</summary>
    string RevealLabel { get; }

    /// <summary>Open the data folder in the platform's native file manager.</summary>
    Task RevealAsync();
}
