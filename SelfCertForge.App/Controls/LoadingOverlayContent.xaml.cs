using CommunityToolkit.Maui.Views;

namespace SelfCertForge.App.Controls;

public partial class LoadingOverlayContent : Popup
{
    // Built once from the bundled SVG and reused for every popup (the SVG is ~2.4 MB).
    private static string? _cachedHtml;

    public LoadingOverlayContent()
    {
        InitializeComponent();
        _ = LoadAnimationAsync();
    }

    private async Task LoadAnimationAsync()
    {
        var html = await GetHtmlAsync();
        await MainThread.InvokeOnMainThreadAsync(() =>
            Anim.Source = new HtmlWebViewSource { Html = html });
    }

    private static async Task<string> GetHtmlAsync()
    {
        if (_cachedHtml is not null)
            return _cachedHtml;

        using var stream = await FileSystem.OpenAppPackageFileAsync("loading.svg");
        using var reader = new StreamReader(stream);
        var svg = await reader.ReadToEndAsync();

        // #232631 == ColorPanelElevated (the card surface) so the WebView blends in.
        _cachedHtml =
            "<!DOCTYPE html><html><head>" +
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
            "<style>html,body{margin:0;padding:0;height:100%;background:#232631;overflow:hidden}" +
            "svg{width:100%;height:100%;display:block}</style></head><body>" +
            svg +
            "</body></html>";

        return _cachedHtml;
    }

    private string _message = string.Empty;

    public string Message
    {
        get => _message;
        set
        {
            _message = value;
            if (MessageLabel is not null)
                MessageLabel.Text = value;
        }
    }
}
