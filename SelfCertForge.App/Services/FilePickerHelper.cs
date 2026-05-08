#if MACCATALYST
using Microsoft.Maui.ApplicationModel;
using Foundation;
using UIKit;
using UniformTypeIdentifiers;
#endif
#if WINDOWS
using Windows.Storage.Pickers;
using WinRT.Interop;
#endif

namespace SelfCertForge.App.Services;

public static class FilePickerHelper
{
    public static async Task<string?> PickFileAsync(IEnumerable<string>? extensions = null, string? initialDirectory = null)
    {
#if MACCATALYST
        var tcs = new TaskCompletionSource<string?>();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                UTType[] utTypes;
                if (extensions is not null)
                {
                    var list = new List<UTType>();
                    foreach (var raw in extensions)
                    {
                        var ext = raw.TrimStart('.').Trim();
                        if (string.IsNullOrEmpty(ext)) continue;
                        var t = UTType.CreateFromExtension(ext);
                        if (t is not null) list.Add(t);
                    }
                    utTypes = list.Count > 0 ? list.ToArray() : new[] { UTTypes.Item };
                }
                else
                {
                    utTypes = new[] { UTTypes.Item };
                }

                var picker = new UIDocumentPickerViewController(utTypes, asCopy: true)
                {
                    AllowsMultipleSelection = false
                };

                if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
                {
                    picker.DirectoryUrl = NSUrl.FromFilename(initialDirectory);
                }

                EventHandler<UIDocumentPickedAtUrlsEventArgs>? pickedHandler = null;
                EventHandler? cancelHandler = null;

                pickedHandler = (_, args) =>
                {
                    picker.DidPickDocumentAtUrls -= pickedHandler;
                    picker.WasCancelled -= cancelHandler;
                    var url = args.Urls?.FirstOrDefault();
                    tcs.TrySetResult(url?.Path);
                };
                cancelHandler = (_, _) =>
                {
                    picker.DidPickDocumentAtUrls -= pickedHandler;
                    picker.WasCancelled -= cancelHandler;
                    tcs.TrySetResult(null);
                };

                picker.DidPickDocumentAtUrls += pickedHandler;
                picker.WasCancelled += cancelHandler;

                var currentVc = Platform.GetCurrentUIViewController();
                if (currentVc is null)
                {
                    tcs.TrySetResult(null);
                    return;
                }
                currentVc.PresentViewController(picker, true, null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return await tcs.Task;
#elif WINDOWS
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List
        };
        if (extensions is not null)
        {
            foreach (var ext in extensions)
            {
                var e = ext.Trim();
                if (string.IsNullOrEmpty(e)) continue;
                if (!e.StartsWith('.')) e = "." + e;
                picker.FileTypeFilter.Add(e);
            }
        }
        else
        {
            picker.FileTypeFilter.Add("*");
        }

        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        var hwnd = window is null ? IntPtr.Zero : WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
#else
        await Task.CompletedTask;
        return null;
#endif
    }
}
