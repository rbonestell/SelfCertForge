namespace SelfCertForge.Core.Abstractions;

public interface ILoadingOverlay
{
    Task RunAsync(string message, Func<Task> operation);
    Task<T> RunAsync<T>(string message, Func<Task<T>> operation);
}
