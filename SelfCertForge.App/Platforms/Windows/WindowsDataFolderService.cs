using System.Diagnostics;
using SelfCertForge.Core.Abstractions;

namespace SelfCertForge.App.Platforms.Windows;

public sealed class WindowsDataFolderService : IDataFolderService
{
    public WindowsDataFolderService(string dataFolderPath)
    {
        DataFolderPath = dataFolderPath;
    }

    public string DataFolderPath { get; }

    public string RevealLabel => "Reveal in Explorer";

    public Task RevealAsync()
    {
        // `explorer.exe <dir>` opens File Explorer at the path. ArgumentList
        // ensures spaces/special chars in the path are passed safely without
        // manual quoting (which would otherwise be embedded as literal chars).
        var psi = new ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = true,
        };
        psi.ArgumentList.Add(DataFolderPath);
        try { Process.Start(psi)?.Dispose(); } catch { /* ignore */ }
        return Task.CompletedTask;
    }
}
