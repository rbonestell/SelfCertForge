namespace SelfCertForge.App.Controls;

/// <summary>
/// One segment of a segmented selector. Hosting page wires <see cref="Tapped"/>
/// to a code-behind handler that updates the bound enum/int property on the VM.
/// Avoids Win2D-heavy primitives — pure Border + Label — so it renders safely
/// on Parallels-virtualized Windows ARM64 where shape geometry has been flaky.
/// </summary>
public partial class SegmentedOption : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(SegmentedOption), string.Empty);

    public static readonly BindableProperty IsSelectedProperty =
        BindableProperty.Create(nameof(IsSelected), typeof(bool), typeof(SegmentedOption), false);

    public static readonly BindableProperty UseMonoFontProperty =
        BindableProperty.Create(nameof(UseMonoFont), typeof(bool), typeof(SegmentedOption), false,
            propertyChanged: (b, _, _) => ((SegmentedOption)b).OnPropertyChanged(nameof(ResolvedFontFamily)));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool UseMonoFont
    {
        get => (bool)GetValue(UseMonoFontProperty);
        set => SetValue(UseMonoFontProperty, value);
    }

    public string ResolvedFontFamily => UseMonoFont ? "JetBrainsMono" : "InterRegular";

    public event EventHandler? Tapped;

    public SegmentedOption()
    {
        InitializeComponent();
    }

    private void OnTapped(object? sender, TappedEventArgs e) => Tapped?.Invoke(this, EventArgs.Empty);
}
