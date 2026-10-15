using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using SelfCertForge.App.Controls;
using SelfCertForge.Core.Abstractions;

namespace SelfCertForge.App.Services;

public sealed class MauiLoadingOverlay : ILoadingOverlay
{
    private static readonly TimeSpan ShowDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan MinVisible = TimeSpan.FromMilliseconds(3000);
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CloseRetryDelay = TimeSpan.FromMilliseconds(250);

    private readonly object _gate = new();
    private int _depth;
    private string _message = string.Empty;
    private LoadingOverlayContent? _content;
    private Task? _opened; // completes once the popup is on the modal stack
    private Task? _shown;  // ShowPopupAsync: completes once the popup has closed
    private DateTime _shownAtUtc;

    public Task RunAsync(string message, Func<Task> operation)
        => RunAsync(message, async () => { await operation(); return true; });

    public async Task<T> RunAsync<T>(string message, Func<Task<T>> operation)
    {
        await EnterAsync(message);
        try
        {
            return await operation();
        }
        finally
        {
            await ExitAsync();
        }
    }

    private async Task EnterAsync(string message)
    {
        bool firstEntry;
        lock (_gate)
        {
            _depth++;
            _message = message;
            firstEntry = _depth == 1;
        }

        if (!firstEntry)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (_content is not null)
                    _content.Message = message;
            });
            return;
        }

        // Anti-flicker: defer the show; skip it if the work already finished.
        await Task.Delay(ShowDelay);
        lock (_gate)
        {
            if (_depth == 0 || _content is not null)
                return;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var page = Application.Current?.Windows is { Count: > 0 } windows
                ? windows[0].Page
                : null;
            if (page is null)
                return;

            lock (_gate)
            {
                if (_depth == 0 || _content is not null)
                    return;

                _content = new LoadingOverlayContent { Message = _message };
                _shownAtUtc = DateTime.UtcNow;
            }

            var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _content.Opened += (_, _) => opened.TrySetResult();
            _opened = opened.Task;
            _shown = page.ShowPopupAsync(_content, new PopupOptions
            {
                CanBeDismissedByTappingOutsideOfPopup = false,
                Shape = null,
                Shadow = null,
                PageOverlayColor = Colors.Black.WithAlpha(0.6f),
            });
            // A failed push never raises Opened; don't make ExitAsync wait out the timeout.
            _shown.ContinueWith(_ => opened.TrySetResult(), TaskScheduler.Default);
        });
    }

    private async Task ExitAsync()
    {
        bool lastExit;
        lock (_gate)
        {
            _depth--;
            lastExit = _depth == 0;
        }

        if (!lastExit)
            return;

        DateTime shownAt;
        bool isShown;
        Task? opened;
        lock (_gate)
        {
            isShown = _content is not null;
            shownAt = _shownAtUtc;
            opened = _opened;
        }

        if (isShown)
        {
            var elapsed = DateTime.UtcNow - shownAt;
            if (elapsed < MinVisible)
                await Task.Delay(MinVisible - elapsed);

            // The show is fire-and-forget. Closing before the push lands throws PopupNotFound,
            // and the popup would then appear with nothing left to close it.
            if (opened is not null)
            {
                try { await opened.WaitAsync(OpenTimeout); }
                catch (TimeoutException) { /* attempt the close anyway; retry covers a late push */ }
            }
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            lock (_gate)
            {
                if (_depth != 0)
                    return; // re-entered during the delay; keep it visible
            }

            var content = _content;
            var shown = _shown;
            _content = null;
            _opened = null;
            _shown = null;
            if (content is null || shown is null)
                return;

            try
            {
                // content.CloseAsync targets this popup; Page.ClosePopupAsync closes whatever
                // popup is on top, which could be someone else's.
                await content.CloseAsync();
            }
            catch (Exception ex)
            {
                // Toolkit throws PopupNotFound/PopupBlocked if the modal stack isn't what
                // we expect; the operation already succeeded, so don't fail it (issue #2).
                App.Report("MauiLoadingOverlay.ExitAsync", ex);
                if (!shown.IsCompleted)
                    _ = RetryCloseAsync(content, shown);
            }
        });
    }

    // The popup is still up but couldn't be closed (e.g. another modal page is above it).
    // Keep trying in the background so the caller's operation isn't held up, and stop once
    // ShowPopupAsync completes, i.e. the popup is actually gone.
    // Polls every 250ms: the toolkit raises no event when a covering modal goes away.
    private static async Task RetryCloseAsync(LoadingOverlayContent content, Task shown)
    {
        while (!shown.IsCompleted)
        {
            await Task.Delay(CloseRetryDelay);
            try { await MainThread.InvokeOnMainThreadAsync(() => content.CloseAsync()); }
            catch { /* still blocked or already closing; loop re-checks */ }
        }
    }
}
