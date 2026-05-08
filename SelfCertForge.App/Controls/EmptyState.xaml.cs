using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace SelfCertForge.App.Controls;

public partial class EmptyState : ContentView
{
    public static readonly BindableProperty IconDataProperty =
        BindableProperty.Create(nameof(IconData), typeof(Geometry), typeof(EmptyState),
            propertyChanged: (b, _, _) => ((EmptyState)b).OnPropertyChanged(nameof(HasIcon)));

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(EmptyState), string.Empty);

    public static readonly BindableProperty BodyProperty =
        BindableProperty.Create(nameof(Body), typeof(string), typeof(EmptyState), string.Empty,
            propertyChanged: (b, _, _) => ((EmptyState)b).OnPropertyChanged(nameof(HasBody)));

    public static readonly BindableProperty CtaLabelProperty =
        BindableProperty.Create(nameof(CtaLabel), typeof(string), typeof(EmptyState), string.Empty,
            propertyChanged: (b, _, _) => ((EmptyState)b).OnPropertyChanged(nameof(HasCta)));

    public static readonly BindableProperty CtaCommandProperty =
        BindableProperty.Create(nameof(CtaCommand), typeof(ICommand), typeof(EmptyState),
            propertyChanged: (b, _, _) => ((EmptyState)b).OnPropertyChanged(nameof(HasCta)));

    public Geometry? IconData
    {
        get => (Geometry?)GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Body
    {
        get => (string)GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public string CtaLabel
    {
        get => (string)GetValue(CtaLabelProperty);
        set => SetValue(CtaLabelProperty, value);
    }

    public ICommand? CtaCommand
    {
        get => (ICommand?)GetValue(CtaCommandProperty);
        set => SetValue(CtaCommandProperty, value);
    }

    public bool HasIcon => IconData is not null;
    public bool HasBody => !string.IsNullOrWhiteSpace(Body);
    public bool HasCta => !string.IsNullOrWhiteSpace(CtaLabel) && CtaCommand is not null;

    public EmptyState()
    {
        InitializeComponent();
    }
}
