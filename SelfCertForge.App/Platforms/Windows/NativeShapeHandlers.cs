#if WINDOWS

using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using MauiPath = Microsoft.Maui.Controls.Shapes.Path;
using MauiEllipse = Microsoft.Maui.Controls.Shapes.Ellipse;
using MauiBorder = Microsoft.Maui.Controls.Border;
using MauiRoundRectangle = Microsoft.Maui.Controls.Shapes.RoundRectangle;
using WinFoundation = global::Windows.Foundation;
using WinMedia = Microsoft.UI.Xaml.Media;
using WinPath = Microsoft.UI.Xaml.Shapes.Path;
using WinEllipse = Microsoft.UI.Xaml.Shapes.Ellipse;
using WinShape = Microsoft.UI.Xaml.Shapes.Shape;
using WinBorder = Microsoft.UI.Xaml.Controls.Border;
using WinBrush = Microsoft.UI.Xaml.Media.Brush;
using WinSolidColorBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using WinThickness = Microsoft.UI.Xaml.Thickness;
using WinCornerRadius = Microsoft.UI.Xaml.CornerRadius;

namespace SelfCertForge.App.Platforms.Windows;

// MAUI's default ShapeViewHandler on Windows backs Microsoft.Maui.Controls.Shapes.*
// with a Win2D CanvasControl. Win2D's CanvasGeometry.CombineWith fails with HRESULT
// 0x80070490 on virtualized GPUs (WARP fallback in VMs), aborting the compositor.
// These handlers swap the Win2D-backed platform view for native WinUI shapes,
// which render via the compositor without Win2D.

internal sealed class NativeMauiPathHandler : ViewHandler<MauiPath, WinPath>
{
    public static readonly IPropertyMapper<MauiPath, NativeMauiPathHandler> NativeMapper =
        new PropertyMapper<MauiPath, NativeMauiPathHandler>(ViewHandler.ViewMapper)
        {
            [nameof(MauiPath.Data)] = MapData,
            [nameof(MauiPath.Stroke)] = MapStroke,
            [nameof(MauiPath.StrokeThickness)] = MapStrokeThickness,
            [nameof(MauiPath.Fill)] = MapFill,
            ["Aspect"] = MapAspect,
            ["StrokeLineCap"] = MapStrokeLineCap,
            ["StrokeLineJoin"] = MapStrokeLineJoin,
        };

    public NativeMauiPathHandler() : base(NativeMapper) { }

    protected override WinPath CreatePlatformView() => new WinPath();

    private static void MapData(NativeMauiPathHandler handler, MauiPath view)
    {
        handler.PlatformView.Data = view.Data is null ? null : ConvertGeometry(view.Data);
    }

    internal static WinMedia.Geometry? ConvertGeometry(Geometry maui) => maui switch
    {
        PathGeometry pg => ConvertPathGeometry(pg),
        LineGeometry lg => new WinMedia.LineGeometry
        {
            StartPoint = ToWinPoint(lg.StartPoint),
            EndPoint = ToWinPoint(lg.EndPoint),
        },
        RectangleGeometry rg => new WinMedia.RectangleGeometry
        {
            Rect = new WinFoundation.Rect(rg.Rect.X, rg.Rect.Y, rg.Rect.Width, rg.Rect.Height),
        },
        Microsoft.Maui.Controls.Shapes.EllipseGeometry eg => new WinMedia.EllipseGeometry
        {
            Center = ToWinPoint(eg.Center),
            RadiusX = eg.RadiusX,
            RadiusY = eg.RadiusY,
        },
        _ => null,
    };

    private static WinMedia.PathGeometry ConvertPathGeometry(PathGeometry pg)
    {
        var win = new WinMedia.PathGeometry
        {
            FillRule = pg.FillRule == FillRule.Nonzero
                ? WinMedia.FillRule.Nonzero
                : WinMedia.FillRule.EvenOdd,
        };
        foreach (var fig in pg.Figures)
            win.Figures.Add(ConvertFigure(fig));
        return win;
    }

    private static WinMedia.PathFigure ConvertFigure(PathFigure fig)
    {
        var win = new WinMedia.PathFigure
        {
            StartPoint = ToWinPoint(fig.StartPoint),
            IsClosed = fig.IsClosed,
            IsFilled = fig.IsFilled,
        };
        foreach (var seg in fig.Segments)
        {
            var converted = ConvertSegment(seg);
            if (converted is not null) win.Segments.Add(converted);
        }
        return win;
    }

    private static WinMedia.PathSegment? ConvertSegment(PathSegment seg) => seg switch
    {
        LineSegment ls => new WinMedia.LineSegment { Point = ToWinPoint(ls.Point) },
        PolyLineSegment pls => CopyPoints(new WinMedia.PolyLineSegment(), pls.Points, (s, p) => s.Points.Add(p)),
        BezierSegment bs => new WinMedia.BezierSegment
        {
            Point1 = ToWinPoint(bs.Point1),
            Point2 = ToWinPoint(bs.Point2),
            Point3 = ToWinPoint(bs.Point3),
        },
        PolyBezierSegment pbs => CopyPoints(new WinMedia.PolyBezierSegment(), pbs.Points, (s, p) => s.Points.Add(p)),
        QuadraticBezierSegment qb => new WinMedia.QuadraticBezierSegment
        {
            Point1 = ToWinPoint(qb.Point1),
            Point2 = ToWinPoint(qb.Point2),
        },
        PolyQuadraticBezierSegment pqb => CopyPoints(new WinMedia.PolyQuadraticBezierSegment(), pqb.Points, (s, p) => s.Points.Add(p)),
        ArcSegment a => new WinMedia.ArcSegment
        {
            Point = ToWinPoint(a.Point),
            Size = new WinFoundation.Size(a.Size.Width, a.Size.Height),
            RotationAngle = a.RotationAngle,
            IsLargeArc = a.IsLargeArc,
            SweepDirection = a.SweepDirection == SweepDirection.Clockwise
                ? WinMedia.SweepDirection.Clockwise
                : WinMedia.SweepDirection.Counterclockwise,
        },
        _ => null,
    };

    private static T CopyPoints<T>(T target, System.Collections.Generic.IEnumerable<Point> points,
        System.Action<T, WinFoundation.Point> add)
    {
        foreach (var p in points) add(target, ToWinPoint(p));
        return target;
    }

    private static WinFoundation.Point ToWinPoint(Point p) => new(p.X, p.Y);

    private static void MapStroke(NativeMauiPathHandler handler, MauiPath view)
        => SetBrush(handler.PlatformView, isStroke: true, view.Stroke);

    private static void MapFill(NativeMauiPathHandler handler, MauiPath view)
        => SetBrush(handler.PlatformView, isStroke: false, view.Fill);

    private static void MapStrokeThickness(NativeMauiPathHandler handler, MauiPath view)
        => handler.PlatformView.StrokeThickness = view.StrokeThickness;

    private static void MapAspect(NativeMauiPathHandler handler, MauiPath view)
        => handler.PlatformView.Stretch = ToWinStretch(view.Aspect);

    private static void MapStrokeLineCap(NativeMauiPathHandler handler, MauiPath view)
    {
        var cap = ToWinPenLineCap(view.StrokeLineCap);
        handler.PlatformView.StrokeStartLineCap = cap;
        handler.PlatformView.StrokeEndLineCap = cap;
    }

    private static void MapStrokeLineJoin(NativeMauiPathHandler handler, MauiPath view)
        => handler.PlatformView.StrokeLineJoin = ToWinPenLineJoin(view.StrokeLineJoin);

    internal static Microsoft.UI.Xaml.Media.Stretch ToWinStretch(Microsoft.Maui.Controls.Stretch s)
        => s switch
        {
            // MAUI's Stretch.None is the default for Shape and effectively means
            // "fit the view's layout bounds" (its Win2D-backed renderer draws the
            // shape into the full layout slot). Mapping to WinUI Stretch.None
            // would draw at intrinsic geometry size, which is 0x0 for an Ellipse
            // with no explicit geometry — so badges and dots disappear. Match
            // MAUI's effective behavior with Stretch.Fill.
            Microsoft.Maui.Controls.Stretch.None => Microsoft.UI.Xaml.Media.Stretch.Fill,
            Microsoft.Maui.Controls.Stretch.Fill => Microsoft.UI.Xaml.Media.Stretch.Fill,
            Microsoft.Maui.Controls.Stretch.UniformToFill => Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
            _ => Microsoft.UI.Xaml.Media.Stretch.Uniform,
        };

    internal static Microsoft.UI.Xaml.Media.PenLineCap ToWinPenLineCap(Microsoft.Maui.Controls.Shapes.PenLineCap c)
        => c switch
        {
            Microsoft.Maui.Controls.Shapes.PenLineCap.Round => Microsoft.UI.Xaml.Media.PenLineCap.Round,
            Microsoft.Maui.Controls.Shapes.PenLineCap.Square => Microsoft.UI.Xaml.Media.PenLineCap.Square,
            _ => Microsoft.UI.Xaml.Media.PenLineCap.Flat,
        };

    internal static Microsoft.UI.Xaml.Media.PenLineJoin ToWinPenLineJoin(Microsoft.Maui.Controls.Shapes.PenLineJoin j)
        => j switch
        {
            Microsoft.Maui.Controls.Shapes.PenLineJoin.Round => Microsoft.UI.Xaml.Media.PenLineJoin.Round,
            Microsoft.Maui.Controls.Shapes.PenLineJoin.Bevel => Microsoft.UI.Xaml.Media.PenLineJoin.Bevel,
            _ => Microsoft.UI.Xaml.Media.PenLineJoin.Miter,
        };

    internal static WinBrush? ToWinBrush(Microsoft.Maui.Controls.Brush? mauiBrush)
    {
        if (mauiBrush is Microsoft.Maui.Controls.SolidColorBrush solid && solid.Color is not null)
        {
            var c = solid.Color;
            return new WinSolidColorBrush(global::Windows.UI.Color.FromArgb(
                (byte)(c.Alpha * 255),
                (byte)(c.Red * 255),
                (byte)(c.Green * 255),
                (byte)(c.Blue * 255)));
        }
        return null;
    }

    internal static void SetBrush(WinShape shape, bool isStroke, Microsoft.Maui.Controls.Brush? mauiBrush)
    {
        var brush = ToWinBrush(mauiBrush);
        if (isStroke) shape.Stroke = brush;
        else shape.Fill = brush;
    }
}

internal sealed class NativeMauiEllipseHandler : ViewHandler<MauiEllipse, WinEllipse>
{
    public static readonly IPropertyMapper<MauiEllipse, NativeMauiEllipseHandler> NativeMapper =
        new PropertyMapper<MauiEllipse, NativeMauiEllipseHandler>(ViewHandler.ViewMapper)
        {
            [nameof(MauiEllipse.Stroke)] = MapStroke,
            [nameof(MauiEllipse.StrokeThickness)] = MapStrokeThickness,
            [nameof(MauiEllipse.Fill)] = MapFill,
            ["Aspect"] = (h, v) => h.PlatformView.Stretch = NativeMauiPathHandler.ToWinStretch(v.Aspect),
        };

    public NativeMauiEllipseHandler() : base(NativeMapper) { }

    protected override WinEllipse CreatePlatformView() => new WinEllipse();

    private static void MapStroke(NativeMauiEllipseHandler handler, MauiEllipse view)
        => NativeMauiPathHandler.SetBrush(handler.PlatformView, isStroke: true, view.Stroke);

    private static void MapFill(NativeMauiEllipseHandler handler, MauiEllipse view)
        => NativeMauiPathHandler.SetBrush(handler.PlatformView, isStroke: false, view.Fill);

    private static void MapStrokeThickness(NativeMauiEllipseHandler handler, MauiEllipse view)
        => handler.PlatformView.StrokeThickness = view.StrokeThickness;
}

// MAUI Border on Windows uses Microsoft.Maui.Platform.ContentPanel which routes
// Background and StrokeShape rendering through Win2D's W2DGraphicsView. On
// virtualized GPUs this triggers the same CombineWith crash. Swapping to a
// native WinUI Border means Background, BorderBrush, BorderThickness and
// CornerRadius all render through the WinUI compositor without Win2D.
internal sealed class NativeMauiBorderHandler : ViewHandler<MauiBorder, WinBorder>
{
    public static readonly IPropertyMapper<MauiBorder, NativeMauiBorderHandler> NativeMapper =
        new PropertyMapper<MauiBorder, NativeMauiBorderHandler>(ViewHandler.ViewMapper)
        {
            [nameof(MauiBorder.Background)] = MapBackground,
            [nameof(MauiBorder.BackgroundColor)] = MapBackground,
            [nameof(MauiBorder.Stroke)] = MapStroke,
            [nameof(MauiBorder.StrokeThickness)] = MapStrokeThickness,
            [nameof(MauiBorder.StrokeShape)] = MapStrokeShape,
            [nameof(MauiBorder.Padding)] = MapPadding,
            [nameof(MauiBorder.Content)] = MapContent,
        };

    public NativeMauiBorderHandler() : base(NativeMapper) { }

    protected override WinBorder CreatePlatformView() => new WinBorder();

    private static void MapBackground(NativeMauiBorderHandler handler, MauiBorder view)
    {
        handler.PlatformView.Background = ResolveBackgroundBrush(view);
    }

    private static void MapStroke(NativeMauiBorderHandler handler, MauiBorder view)
    {
        handler.PlatformView.BorderBrush = NativeMauiPathHandler.ToWinBrush(view.Stroke);
    }

    private static void MapStrokeThickness(NativeMauiBorderHandler handler, MauiBorder view)
    {
        handler.PlatformView.BorderThickness = new WinThickness(view.StrokeThickness);
    }

    private static void MapStrokeShape(NativeMauiBorderHandler handler, MauiBorder view)
    {
        var b = handler.PlatformView;
        if (view.StrokeShape is MauiRoundRectangle rr)
        {
            // Stash the requested radius so SizeChanged can re-clamp once we
            // know the actual rendered dimensions. WinUI Border does not auto-
            // clamp CornerRadius to half the shorter side the way CSS does, so
            // a "pill"-style RoundRectangle 999 distorts into an oval at small
            // sizes. Clamp ourselves.
            b.Tag = rr.CornerRadius;
            ApplyClampedCornerRadius(b, rr.CornerRadius);
            b.SizeChanged -= BorderSizeChanged;
            b.SizeChanged += BorderSizeChanged;
        }
        else
        {
            b.Tag = null;
            b.CornerRadius = new WinCornerRadius(0);
            b.SizeChanged -= BorderSizeChanged;
        }
    }

    private static void BorderSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        if (sender is WinBorder b && b.Tag is Microsoft.Maui.CornerRadius requested)
        {
            ApplyClampedCornerRadius(b, requested);
        }
    }

    private static void ApplyClampedCornerRadius(WinBorder b, Microsoft.Maui.CornerRadius requested)
    {
        var maxRadius = System.Math.Min(b.ActualWidth, b.ActualHeight) / 2;
        if (maxRadius <= 0) maxRadius = double.MaxValue; // pre-layout: trust the value
        b.CornerRadius = new WinCornerRadius(
            System.Math.Min(requested.TopLeft, maxRadius),
            System.Math.Min(requested.TopRight, maxRadius),
            System.Math.Min(requested.BottomRight, maxRadius),
            System.Math.Min(requested.BottomLeft, maxRadius));
    }

    private static void MapPadding(NativeMauiBorderHandler handler, MauiBorder view)
    {
        var p = view.Padding;
        handler.PlatformView.Padding = new WinThickness(p.Left, p.Top, p.Right, p.Bottom);
    }

    private static void MapContent(NativeMauiBorderHandler handler, MauiBorder view)
    {
        if (view.Content is null || handler.MauiContext is null)
        {
            handler.PlatformView.Child = null;
            return;
        }

        var platform = view.Content.ToPlatform(handler.MauiContext);
        // MAUI's HorizontalOptions / VerticalOptions don't propagate through
        // ToPlatform when we slot the result directly into a WinUI Border;
        // the FrameworkElement defaults to Stretch and the content lands at
        // top-left rather than where the XAML asked for it.
        if (platform is Microsoft.UI.Xaml.FrameworkElement fe)
        {
            fe.HorizontalAlignment = ToWinHAlign(view.Content.HorizontalOptions.Alignment);
            fe.VerticalAlignment = ToWinVAlign(view.Content.VerticalOptions.Alignment);
        }
        handler.PlatformView.Child = platform;
    }

    private static Microsoft.UI.Xaml.HorizontalAlignment ToWinHAlign(Microsoft.Maui.Controls.LayoutAlignment a) => a switch
    {
        Microsoft.Maui.Controls.LayoutAlignment.Start => Microsoft.UI.Xaml.HorizontalAlignment.Left,
        Microsoft.Maui.Controls.LayoutAlignment.Center => Microsoft.UI.Xaml.HorizontalAlignment.Center,
        Microsoft.Maui.Controls.LayoutAlignment.End => Microsoft.UI.Xaml.HorizontalAlignment.Right,
        _ => Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
    };

    private static Microsoft.UI.Xaml.VerticalAlignment ToWinVAlign(Microsoft.Maui.Controls.LayoutAlignment a) => a switch
    {
        Microsoft.Maui.Controls.LayoutAlignment.Start => Microsoft.UI.Xaml.VerticalAlignment.Top,
        Microsoft.Maui.Controls.LayoutAlignment.Center => Microsoft.UI.Xaml.VerticalAlignment.Center,
        Microsoft.Maui.Controls.LayoutAlignment.End => Microsoft.UI.Xaml.VerticalAlignment.Bottom,
        _ => Microsoft.UI.Xaml.VerticalAlignment.Stretch,
    };

    private static WinBrush? ResolveBackgroundBrush(MauiBorder view)
    {
        // BackgroundColor and Background are independent in MAUI; Background takes
        // precedence when both are set.
        if (view.Background is Microsoft.Maui.Controls.Brush b
            && NativeMauiPathHandler.ToWinBrush(b) is { } fromBrush)
        {
            return fromBrush;
        }

        if (view.BackgroundColor is { } c)
        {
            return new WinSolidColorBrush(global::Windows.UI.Color.FromArgb(
                (byte)(c.Alpha * 255),
                (byte)(c.Red * 255),
                (byte)(c.Green * 255),
                (byte)(c.Blue * 255)));
        }

        return null;
    }
}

#endif
