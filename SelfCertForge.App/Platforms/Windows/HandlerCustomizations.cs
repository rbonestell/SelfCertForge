using Microsoft.Maui.Handlers;
using WinThickness = Microsoft.UI.Xaml.Thickness;
using WinBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;

namespace SelfCertForge.App.Platforms.Windows;

internal static class HandlerCustomizations
{
    public static void Apply()
    {
        EntryHandler.Mapper.AppendToMapping("SelfCertForge.NoBorder", (handler, _) =>
        {
            var textBox = handler.PlatformView;
            textBox.BorderThickness = new WinThickness(0);
            textBox.Background = new WinBrush(Microsoft.UI.Colors.Transparent);
            textBox.FocusVisualPrimaryThickness = new WinThickness(0);
            textBox.FocusVisualSecondaryThickness = new WinThickness(0);
        });

        EditorHandler.Mapper.AppendToMapping("SelfCertForge.NoBorder", (handler, _) =>
        {
            var textBox = handler.PlatformView;
            textBox.BorderThickness = new WinThickness(0);
            textBox.Background = new WinBrush(Microsoft.UI.Colors.Transparent);
            textBox.FocusVisualPrimaryThickness = new WinThickness(0);
            textBox.FocusVisualSecondaryThickness = new WinThickness(0);
        });

        // Compensate for MAUI's cross-platform FontSize unit inconsistency.
        // FontSize is points on iOS / macCatalyst (with macCatalyst's "Mac idiom"
        // applying an additional ~77% scale) but DIPs on Windows. The same
        // FontSize="32" therefore reads larger on Windows than on Mac. Multiply
        // platform font size by the macCatalyst Mac-idiom factor so visual sizes
        // line up with the macOS reference.
        const double WindowsFontScale = 0.77;

        static bool IsInTitleBar(Microsoft.Maui.Controls.Element? element)
        {
            while (element is not null)
            {
                if (element is Microsoft.Maui.Controls.TitleBar) return true;
                element = element.Parent;
            }
            return false;
        }

        // Chain into the "Font" mapping (rather than register under a unique key)
        // so the scale re-runs whenever ANY font property changes — e.g., a
        // DataTrigger swapping FontFamily on hover/active reapplies the default
        // FontSize, which would otherwise undo our compensation.
        LabelHandler.Mapper.AppendToMapping("Font", (handler, view) =>
        {
            // The MAUI TitleBar renders Title / Subtitle via internal Labels;
            // shrinking those by 0.77 makes the window title unreadable.
            if (IsInTitleBar(view as Microsoft.Maui.Controls.Element)) return;
            if (view is Microsoft.Maui.ITextStyle ts)
                handler.PlatformView.FontSize = ts.Font.Size * WindowsFontScale;
        });

        EntryHandler.Mapper.AppendToMapping("Font", (handler, view) =>
        {
            if (view is Microsoft.Maui.ITextStyle ts)
                handler.PlatformView.FontSize = ts.Font.Size * WindowsFontScale;
        });

        EditorHandler.Mapper.AppendToMapping("Font", (handler, view) =>
        {
            if (view is Microsoft.Maui.ITextStyle ts)
                handler.PlatformView.FontSize = ts.Font.Size * WindowsFontScale;
        });

        ButtonHandler.Mapper.AppendToMapping("Font", (handler, view) =>
        {
            if (view is Microsoft.Maui.ITextStyle ts)
                handler.PlatformView.FontSize = ts.Font.Size * WindowsFontScale;
        });
    }
}
