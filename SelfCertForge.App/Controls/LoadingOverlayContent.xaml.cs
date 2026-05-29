namespace SelfCertForge.App.Controls;

public partial class LoadingOverlayContent : ContentView
{
    public LoadingOverlayContent()
    {
        InitializeComponent();
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
