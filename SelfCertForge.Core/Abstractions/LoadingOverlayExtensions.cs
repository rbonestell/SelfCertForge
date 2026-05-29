namespace SelfCertForge.Core.Abstractions;

public static class LoadingOverlayExtensions
{
    public static Task RunOrDirectAsync(this ILoadingOverlay? overlay, string message, Func<Task> operation)
        => overlay is null ? operation() : overlay.RunAsync(message, operation);

    public static Task<T> RunOrDirectAsync<T>(this ILoadingOverlay? overlay, string message, Func<Task<T>> operation)
        => overlay is null ? operation() : overlay.RunAsync(message, operation);
}
