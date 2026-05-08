namespace SelfCertForge.App.Controls;

public partial class FieldRow : ContentView
{
    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(FieldRow), string.Empty);

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(string), typeof(FieldRow), string.Empty);

    public static readonly BindableProperty MonoProperty =
        BindableProperty.Create(nameof(Mono), typeof(bool), typeof(FieldRow), false,
            propertyChanged: (b, _, _) => ((FieldRow)b).OnPropertyChanged(nameof(ResolvedFontFamily)));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool Mono
    {
        get => (bool)GetValue(MonoProperty);
        set => SetValue(MonoProperty, value);
    }

    public string ResolvedFontFamily => Mono ? "JetBrainsMono" : "InterRegular";

    public FieldRow()
    {
        InitializeComponent();
    }
}
