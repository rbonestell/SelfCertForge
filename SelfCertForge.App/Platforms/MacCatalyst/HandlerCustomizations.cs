using Microsoft.Maui.Handlers;
using UIKit;

namespace SelfCertForge.App.Platforms.MacCatalyst;

internal static class HandlerCustomizations
{
    public static void Apply()
    {
        EntryHandler.Mapper.AppendToMapping("SelfCertForge.NoBorder", (handler, _) =>
        {
            var tf = handler.PlatformView;
            tf.BorderStyle = UITextBorderStyle.None;
            tf.BackgroundColor = UIColor.Clear;
            tf.Layer.BorderWidth = 0;
            tf.Layer.MasksToBounds = true;
            if (OperatingSystem.IsMacCatalystVersionAtLeast(17))
            {
                tf.FocusEffect = null;
            }
        });

        EditorHandler.Mapper.AppendToMapping("SelfCertForge.NoBorder", (handler, _) =>
        {
            var tv = handler.PlatformView;
            tv.BackgroundColor = UIColor.Clear;
            tv.Layer.BorderWidth = 0;
            tv.TextContainerInset = new UIKit.UIEdgeInsets(8, 0, 8, 0);
            if (OperatingSystem.IsMacCatalystVersionAtLeast(17))
            {
                tv.FocusEffect = null;
            }
        });

        PickerHandler.Mapper.AppendToMapping("SelfCertForge.NoBorder", (handler, _) =>
        {
            var tf = handler.PlatformView;
            tf.BorderStyle = UITextBorderStyle.None;
            tf.BackgroundColor = UIColor.Clear;
            tf.Layer.BorderWidth = 0;
            if (OperatingSystem.IsMacCatalystVersionAtLeast(17))
            {
                tf.FocusEffect = null;
            }
        });

    }
}
