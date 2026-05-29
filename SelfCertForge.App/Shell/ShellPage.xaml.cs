using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using SelfCertForge.App.Controls;

namespace SelfCertForge.App.Shell;

public partial class ShellPage : ContentPage
{
    public ShellPage(ShellViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    // Easter egg / UI-layout test aid: tap the brand wordmark to pop the loading
    // lightbox with the caption "SelfCertForge". Unlike the real loading overlay,
    // this instance is dismissable by tapping the dimmed backdrop.
    private async void OnBrandLogoTapped(object? sender, TappedEventArgs e)
    {
        var popup = new LoadingOverlayContent { Message = "SelfCertForge" };
        await this.ShowPopupAsync(popup, new PopupOptions
        {
            CanBeDismissedByTappingOutsideOfPopup = true,
            Shape = null,
            Shadow = null,
            PageOverlayColor = Colors.Black.WithAlpha(0.6f),
        });
    }
}
