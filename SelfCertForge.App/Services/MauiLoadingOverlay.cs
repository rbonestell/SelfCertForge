using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using SelfCertForge.App.Controls;
using SelfCertForge.Core.Abstractions;

namespace SelfCertForge.App.Services;

public sealed class MauiLoadingOverlay : ILoadingOverlay
{
    private static readonly TimeSpan ShowDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan MinVisible = TimeSpan.FromMilliseconds(500);

    private readonly object _gate = new();
    private int _depth;
    private string _message = string.Empty;
    private LoadingOverlayContent? _content;
    private Page? _hostPage;
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
            lock (_gate)
            {
                if (_depth == 0 || _content is not null)
                    return;
            }

            var page = Application.Current?.Windows is { Count: > 0 } windows
                ? windows[0].Page
                : null;
            if (page is null)
                return;

            _hostPage = page;
            _content = new LoadingOverlayContent { Message = _message };
            _shownAtUtc = DateTime.UtcNow;

            _ = page.ShowPopupAsync(_content, new PopupOptions
            {
                CanBeDismissedByTappingOutsideOfPopup = false,
                Shape = null,
                PageOverlayColor = Color.FromRgba(0, 0, 0, 0.6),
            });
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
        lock (_gate)
        {
            isShown = _content is not null;
            shownAt = _shownAtUtc;
        }

        if (isShown)
        {
            var elapsed = DateTime.UtcNow - shownAt;
            if (elapsed < MinVisible)
                await Task.Delay(MinVisible - elapsed);
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            lock (_gate)
            {
                if (_depth != 0)
                    return; // re-entered during the delay; keep it visible
            }

            if (_content is not null && _hostPage is not null)
            {
                await _hostPage.ClosePopupAsync();
                _content = null;
                _hostPage = null;
            }
        });
    }
}
