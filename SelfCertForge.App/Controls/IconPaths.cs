using Microsoft.Maui.Controls.Shapes;

namespace SelfCertForge.App.Controls;

/// <summary>
/// Lucide icon path data, 24x24 viewbox, exposed as parsed <see cref="Geometry"/> instances.
/// Pre-parsing avoids MAUI's binding system not auto-converting string→Geometry on Path.Data.
/// </summary>
public static class IconPaths
{
    private static readonly PathGeometryConverter Converter = new();
    private static Geometry G(string d) => (Geometry)Converter.ConvertFromInvariantString(d)!;

    public static readonly Geometry LayoutDashboard = G("M3 3h7v9H3zM14 3h7v5h-7zM14 12h7v9h-7zM3 16h7v5H3z");
    public static readonly Geometry ShieldCheck     = G("M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1zM9 12l2 2 4-4");
    public static readonly Geometry ScrollText      = G("M15 12h-5M15 8h-5M19 17V5a2 2 0 0 0-2-2H4M8 21h12a2 2 0 0 0 2-2v-1a1 1 0 0 0-1-1H11a1 1 0 0 0-1 1v1a2 2 0 1 1-4 0V5a2 2 0 1 0-4 0v2a1 1 0 0 0 1 1h3");
    public static readonly Geometry Hammer          = G("M15 12l-8.5 8.5a2.12 2.12 0 0 1-3-3L12 9M17.64 15L22 10.64M20.91 11.7l-1.25-1.25c-.6-.6-.93-1.4-.93-2.25v-.86L16.01 4.6a5.56 5.56 0 0 0-3.94-1.64H9l.92.82A6.18 6.18 0 0 1 12 8.4v1.56l2 2h2.47l2.26 1.91");
    public static readonly Geometry Gavel           = G("M14.5 12.5l-8 8a2.119 2.119 0 1 1-3-3l8-8M16 16l6-6M8 8l6-6M9 7l8 8M21 11l-8-8");
    public static readonly Geometry KeyRound        = G("M2.586 17.414A2 2 0 0 0 2 18.828V21a1 1 0 0 0 1 1h3a1 1 0 0 0 1-1v-1a1 1 0 0 1 1-1h1a1 1 0 0 0 1-1v-1a1 1 0 0 1 1-1h.172a2 2 0 0 0 1.414-.586l.814-.814a6.5 6.5 0 1 0-4-4zM16 12h.01");
    public static readonly Geometry Settings2       = G("M14 17H5M19 7h-9M17 17a2 2 0 1 0 4 0a2 2 0 1 0-4 0zM7 7a2 2 0 1 0 4 0a2 2 0 1 0-4 0z");
    public static readonly Geometry Search          = G("M11 11a7 7 0 1 0 0-14 7 7 0 0 0 0 14zM21 21l-4.3-4.3");
    public static readonly Geometry X               = G("M18 6L6 18M6 6l12 12");
    public static readonly Geometry Copy            = G("M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2M9 2h6a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1H9a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1z");
    public static readonly Geometry TriangleAlert   = G("M21.73 18l-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3zM12 9v4M12 17h.01");
    public static readonly Geometry ArrowUpFromLine = G("M12 3v12M6 9l6-6 6 6M5 21h14");
    public static readonly Geometry ChevronDown     = G("M6 9l6 6 6-6");
    public static readonly Geometry Plus            = G("M12 5v14M5 12h14");
    public static readonly Geometry CalendarClock   = G("M16 14v2.2l1.6 1M16 2v4M21 7.5V6a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h3.5M3 10h5M8 2v4M22 16a6 6 0 1 0-12 0a6 6 0 1 0 12 0z");
    public static readonly Geometry FileBadge       = G("M13 22h5a2 2 0 0 0 2-2V8a2.4 2.4 0 0 0-.706-1.706l-3.588-3.588A2.4 2.4 0 0 0 14 2H6a2 2 0 0 0-2 2v3.3M14 2v5a1 1 0 0 0 1 1h5M7.69 16.479l1.29 4.88a.5.5 0 0 1-.698.591l-1.843-.849a1 1 0 0 0-.879.001l-1.846.85a.5.5 0 0 1-.692-.593l1.29-4.88M9 14a3 3 0 1 0-6 0a3 3 0 1 0 6 0z");
    public static readonly Geometry Info            = G("M22 12a10 10 0 1 0-20 0a10 10 0 0 0 20 0zM12 16v-4M12 8h.01");
    public static readonly Geometry Activity        = G("M22 12h-2.48a2 2 0 0 0-1.93 1.46l-2.35 8.36a.25.25 0 0 1-.48 0L9.24 2.18a.25.25 0 0 0-.48 0l-2.35 8.36A2 2 0 0 1 4.49 12H2");
}
