using System.Diagnostics;
using SelfCertForge.Core.Abstractions;

namespace SelfCertForge.App.Platforms.MacCatalyst;

public sealed class MacDataFolderService : IDataFolderService
{
    public MacDataFolderService(string dataFolderPath)
    {
        DataFolderPath = dataFolderPath;
    }

    public string DataFolderPath { get; }

    public string RevealLabel => "Reveal in Finder";

    public Task RevealAsync()
    {
        // `open <dir>` opens the folder in Finder. With UseShellExecute=false
        // ProcessStartInfo passes ArgumentList items directly to argv — no shell
        // is involved, so quoting must NOT be added (a literal '"' would end up
        // in the path). ArgumentList sidesteps Process.Start's string-splitting.
        var psi = new ProcessStartInfo("open")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(DataFolderPath);
        try { Process.Start(psi)?.Dispose(); } catch { /* ignore */ }
        return Task.CompletedTask;
    }
}
