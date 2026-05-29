using SelfCertForge.Core.Abstractions;

namespace SelfCertForge.Core.Tests;

public sealed class FakeLoadingOverlay : ILoadingOverlay
{
    public List<string> Messages { get; } = new();

    /// <summary>
    /// Highest number of overlay scopes open simultaneously. A nested
    /// <see cref="RunAsync(string, Func{Task})"/> — e.g. "Installing" running inside
    /// "Downloading" — drives this to 2, letting tests prove the calls actually nest
    /// rather than merely running back-to-back.
    /// </summary>
    public int MaxConcurrentDepth { get; private set; }

    private int _depth;

    public async Task RunAsync(string message, Func<Task> operation)
    {
        Enter(message);
        try { await operation(); }
        finally { _depth--; }
    }

    public async Task<T> RunAsync<T>(string message, Func<Task<T>> operation)
    {
        Enter(message);
        try { return await operation(); }
        finally { _depth--; }
    }

    private void Enter(string message)
    {
        Messages.Add(message);
        _depth++;
        if (_depth > MaxConcurrentDepth) MaxConcurrentDepth = _depth;
    }
}
