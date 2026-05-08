namespace SelfCertForge.App.Controls;

/// <summary>
/// Trust-aware status pill. Per the design system trust model:
/// `installed` / `uninstalled` describe a root authority's trust-store state;
/// `valid` / `expiring` / `expired` / `orphaned` describe a child certificate.
/// Trust never applies to a child cert directly.
/// </summary>
public partial class StatusPill : ContentView
{
    public static readonly BindableProperty KindProperty = BindableProperty.Create(
        nameof(Kind), typeof(string), typeof(StatusPill), defaultValue: "valid",
        propertyChanged: OnKindChanged);

    public static readonly BindableProperty LabelProperty = BindableProperty.Create(
        nameof(Label), typeof(string), typeof(StatusPill), string.Empty);

    public string Kind
    {
        get => (string)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public Color BgColor { get; private set; } = Colors.Transparent;
    public Color BorderColor { get; private set; } = Colors.Transparent;
    public Color DotColor { get; private set; } = Colors.Transparent;
    public Color TextColor { get; private set; } = Colors.Transparent;

    public StatusPill()
    {
        InitializeComponent();
        ApplyKind(Kind);
    }

    private static void OnKindChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is StatusPill pill && newValue is string kind)
            pill.ApplyKind(kind);
    }

    private void ApplyKind(string kind)
    {
        // Resolve from App resources so styling stays in the design tokens.
        var success = (Color)Application.Current!.Resources["ColorSuccess"];
        var warning = (Color)Application.Current.Resources["ColorWarning"];
        var danger  = (Color)Application.Current.Resources["ColorDanger"];
        var muted   = (Color)Application.Current.Resources["ColorTextMuted"];

        var solid = kind switch
        {
            "installed" => success,
            "valid"     => success,
            "expiring"  => warning,
            "expired"   => danger,
            "orphaned"  => danger,
            _           => muted,
        };

        DotColor = solid;
        BgColor = solid.WithAlpha(0.12f);
        BorderColor = solid.WithAlpha(0.28f);
        TextColor = solid;

        OnPropertyChanged(nameof(DotColor));
        OnPropertyChanged(nameof(BgColor));
        OnPropertyChanged(nameof(BorderColor));
        OnPropertyChanged(nameof(TextColor));
    }
}
