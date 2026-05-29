namespace SelfCertForge.Infrastructure;

/// <summary>
/// Owns where SelfCertForge keeps its data. Historically the JSON stores and
/// the certificates/ folder were written directly into the platform's app-data
/// root (e.g. <c>~/Library</c> on an unsandboxed macCatalyst build), which
/// littered the user's Library. Everything now lives under a dedicated
/// <see cref="FolderName"/> subfolder, and <see cref="Resolve"/> performs a
/// one-time, best-effort migration of any legacy files into it on launch.
/// </summary>
public static class DataFolderLayout
{
    public const string FolderName = "SelfCertForge";

    // Items the app creates directly under its data root. Listed here so the
    // legacy migration knows exactly what to relocate (and nothing else — we
    // never sweep unrelated files out of the platform root).
    private static readonly string[] LegacyFiles =
        { "certificates.json", "activity.json", "preferences.json" };
    private static readonly string[] LegacyDirectories =
        { "certificates" };

    /// <summary>
    /// Returns the dedicated data folder under <paramref name="appDataRoot"/>,
    /// first relocating any legacy data that lived directly in the root, then
    /// ensuring the folder exists. The migration is best-effort: a locked or
    /// partially-moved file is left where it is rather than blocking startup.
    /// </summary>
    public static string Resolve(string appDataRoot)
    {
        var target = Path.Combine(appDataRoot, FolderName);
        MigrateLegacyData(appDataRoot, target);
        Directory.CreateDirectory(target);
        return target;
    }

    /// <summary>
    /// Moves known data items from <paramref name="oldRoot"/> to
    /// <paramref name="newRoot"/>. An item is moved only when it exists in the
    /// old location AND is absent in the new one, so re-runs are no-ops and
    /// existing (newer) data is never clobbered.
    /// </summary>
    public static void MigrateLegacyData(string oldRoot, string newRoot)
    {
        // Defensive: if the roots resolve to the same directory there's nothing
        // to migrate (and a self-move would throw).
        if (string.Equals(Path.GetFullPath(oldRoot), Path.GetFullPath(newRoot), StringComparison.Ordinal))
            return;

        foreach (var name in LegacyFiles)
            TryMoveFile(Path.Combine(oldRoot, name), Path.Combine(newRoot, name));
        foreach (var name in LegacyDirectories)
            TryMoveDirectory(Path.Combine(oldRoot, name), Path.Combine(newRoot, name));
    }

    private static void TryMoveFile(string source, string dest)
    {
        try
        {
            if (!File.Exists(source)) return;
            if (File.Exists(dest)) return; // never overwrite newer data
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Move(source, dest);
        }
        catch { /* best-effort — leave the legacy file in place on failure */ }
    }

    private static void TryMoveDirectory(string source, string dest)
    {
        try
        {
            if (!Directory.Exists(source)) return;
            if (Directory.Exists(dest)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            Directory.Move(source, dest);
        }
        catch { /* best-effort */ }
    }
}
