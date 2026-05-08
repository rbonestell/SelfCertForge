#if WINDOWS

using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using WinUIWindow = Microsoft.UI.Xaml.Window;

namespace SelfCertForge.App.Platforms.Windows;

// Paints the WinUI title bar with the SelfCertForge dark surface so it
// blends into the app chrome. Colors mirror the brand tokens declared in
// App.xaml (ColorBackground / ColorTextPrimary / ColorPanel / ColorPanelElevated).
internal static class WindowCustomizations
{
    private static readonly global::Windows.UI.Color Surface
        = global::Windows.UI.Color.FromArgb(0xFF, 0x0B, 0x0C, 0x10);
    private static readonly global::Windows.UI.Color OnSurface
        = global::Windows.UI.Color.FromArgb(0xFF, 0xF2, 0xF3, 0xF6);
    private static readonly global::Windows.UI.Color ButtonHover
        = global::Windows.UI.Color.FromArgb(0xFF, 0x1B, 0x1D, 0x25);
    private static readonly global::Windows.UI.Color ButtonPressed
        = global::Windows.UI.Color.FromArgb(0xFF, 0x23, 0x26, 0x31);

    public static void ApplyTitleBar(WinUIWindow window)
    {
        ApplyColors(window);

        // Window.Content can still be null at OnWindowCreated time; reapply once
        // on first Activated so RequestedTheme=Dark gets onto the root element
        // (the OS-rendered drag area honors that, not just AppWindowTitleBar.BackgroundColor).
        global::Windows.Foundation.TypedEventHandler<object, Microsoft.UI.Xaml.WindowActivatedEventArgs>? handler = null;
        handler = (s, _) =>
        {
            ApplyColors(window);
            if (handler is not null) window.Activated -= handler;
        };
        window.Activated += handler;
    }

    private const uint DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const uint DWMWA_CAPTION_COLOR = 35;
    private const uint DWMWA_TEXT_COLOR = 36;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint attr, ref int value, int size);

    // DWM caption/text colors are COLORREF (0x00BBGGRR, opposite byte order from
    // typical RGB hex). These are the BGR forms of Surface (#0B0C10) and
    // OnSurface (#F2F3F6).
    private const int CaptionColorBGR = 0x00100C0B;
    private const int CaptionTextColorBGR = 0x00F6F3F2;

    private static void ApplyColors(WinUIWindow window)
    {
        if (window.Content is Microsoft.UI.Xaml.FrameworkElement root)
        {
            root.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Dark;
        }

        var hwnd = WindowNative.GetWindowHandle(window);

        // Set caption color, text color, and immersive dark mode directly via DWM —
        // AppWindowTitleBar.ForegroundColor is unreliable when the OS is in light
        // theme. These attributes are honored on Windows 11 (build 22000+) and
        // ignored on older builds (no-op via PreserveSig).
        int useDark = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        int captionColor = CaptionColorBGR;
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
        int textColor = CaptionTextColorBGR;
        DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));

        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        if (appWindow?.TitleBar is null) return;

        var t = appWindow.TitleBar;
        t.BackgroundColor = Surface;
        t.InactiveBackgroundColor = Surface;
        t.ForegroundColor = OnSurface;
        t.InactiveForegroundColor = OnSurface;
        t.ButtonBackgroundColor = Surface;
        t.ButtonInactiveBackgroundColor = Surface;
        t.ButtonForegroundColor = OnSurface;
        t.ButtonInactiveForegroundColor = OnSurface;
        t.ButtonHoverBackgroundColor = ButtonHover;
        t.ButtonHoverForegroundColor = OnSurface;
        t.ButtonPressedBackgroundColor = ButtonPressed;
        t.ButtonPressedForegroundColor = OnSurface;
    }
}

#endif
