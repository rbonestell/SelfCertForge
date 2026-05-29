using SelfCertForge.Core.Abstractions;

namespace SelfCertForge.Core.Tests;

public sealed class FakeLoadingOverlay : ILoadingOverlay
{
    public List<string> Messages { get; } = new();

    public Task RunAsync(string message, Func<Task> operation)
    {
        Messages.Add(message);
        return operation();
    }

    public Task<T> RunAsync<T>(string message, Func<Task<T>> operation)
    {
        Messages.Add(message);
        return operation();
    }
}
