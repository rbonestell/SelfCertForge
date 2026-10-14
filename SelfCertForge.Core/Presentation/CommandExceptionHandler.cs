namespace SelfCertForge.Core.Presentation;

/// <summary>
/// Sink for exceptions thrown by async commands. ICommand.Execute is async void, so
/// an exception escaping it terminates the app; commands report here instead.
/// The App sets <see cref="Handler"/> at startup.
/// </summary>
public static class CommandExceptionHandler
{
    public static Action<Exception>? Handler { get; set; }

    internal static void Report(Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[Command] Unhandled exception: {ex}");
        Handler?.Invoke(ex);
    }
}
