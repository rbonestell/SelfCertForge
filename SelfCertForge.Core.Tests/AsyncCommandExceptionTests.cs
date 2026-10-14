using System.Collections.Concurrent;
using FluentAssertions;
using SelfCertForge.Core.Presentation;
using Xunit;

namespace SelfCertForge.Core.Tests;

// ICommand.Execute is async void: an exception escaping it terminates the app (issue #2).
public sealed class AsyncCommandExceptionTests
{
    [Fact]
    public async Task AsyncRelayCommand_ThrowingExecute_ReportsAndStaysUsable()
    {
        var boom = new InvalidOperationException("boom");
        var reported = new ConcurrentQueue<Exception>();
        CommandExceptionHandler.Handler = reported.Enqueue;
        try
        {
            var command = new AsyncRelayCommand(async () => { await Task.Yield(); throw boom; });

            await RunOnSyncContext(() => command.Execute(null));

            reported.Should().Contain(boom);
            command.CanExecute(null).Should().BeTrue();
        }
        finally
        {
            CommandExceptionHandler.Handler = null;
        }
    }

    [Fact]
    public async Task AsyncCommand_ThrowingExecute_Reports()
    {
        var boom = new InvalidOperationException("boom");
        var reported = new ConcurrentQueue<Exception>();
        CommandExceptionHandler.Handler = reported.Enqueue;
        try
        {
            var command = new AsyncCommand(async () => { await Task.Yield(); throw boom; });

            await RunOnSyncContext(() => command.Execute(null));

            reported.Should().Contain(boom);
        }
        finally
        {
            CommandExceptionHandler.Handler = null;
        }
    }

    // async void rethrows onto the captured SynchronizationContext; capture that so the
    // test sees what the UI dispatcher would see (an unhandled crash) instead of losing it.
    // Post runs inline, so by OperationCompleted any rethrow has already been captured.
    private static async Task RunOnSyncContext(Action action)
    {
        var context = new CapturingContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try { action(); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }

        await context.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        context.Crash.Should().BeNull("an exception escaping async void crashes the app");
    }

    private sealed class CapturingContext : SynchronizationContext
    {
        public readonly TaskCompletionSource Completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Exception? Crash;

        public override void Post(SendOrPostCallback d, object? state)
        {
            try { d(state); }
            catch (Exception ex) { Crash = ex; }
        }

        public override void OperationCompleted() => Completed.TrySetResult();
    }
}
