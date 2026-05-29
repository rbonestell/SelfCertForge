using CommunityToolkit.Maui.Views;

namespace SelfCertForge.App.Controls;

public partial class LoadingOverlayContent : Popup
{
    private const string PulseHandle = "LoadingPulse";

    public LoadingOverlayContent()
    {
        InitializeComponent();
        Mark.Loaded += (_, _) => StartPulse();
        Mark.Unloaded += (_, _) => Mark.AbortAnimation(PulseHandle);
    }

    // Gentle scale pulse so the brand mark reads as "working" without implying a
    // spinner. Loops until the view leaves the tree (popup dismissed/closed).
    private void StartPulse()
    {
        var pulse = new Animation();
        pulse.Add(0.0, 0.5, new Animation(v => Mark.Scale = v, 0.92, 1.06, Easing.SinInOut));
        pulse.Add(0.5, 1.0, new Animation(v => Mark.Scale = v, 1.06, 0.92, Easing.SinInOut));
        pulse.Commit(Mark, PulseHandle, length: 1200, repeat: () => true);
    }

    private string _message = string.Empty;

    public string Message
    {
        get => _message;
        set
        {
            _message = value;
            if (MessageLabel is not null)
                MessageLabel.Text = value;
        }
    }
}
