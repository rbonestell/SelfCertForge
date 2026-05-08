using SelfCertForge.Core.Presentation;

namespace SelfCertForge.App.Dialogs;

public partial class CreateRootDialog : ContentPage
{
    private readonly CreateRootDialogViewModel _viewModel;

    public CreateRootDialog(CreateRootDialogViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        InitializeComponent();

        _viewModel.Created += OnCreatedOrCancelled;
        _viewModel.CancelRequested += OnCreatedOrCancelled;
    }

    private void OnCreatedOrCancelled(object? sender, object? e) =>
        MainThread.BeginInvokeOnMainThread(() => _ = Navigation.PopModalAsync(animated: false));

    public void PrepareForOpen() => _viewModel.Reset();

    private void OnKeyBits2048(object? sender, TappedEventArgs e) => _viewModel.KeyBits = 2048;
    private void OnKeyBits3072(object? sender, TappedEventArgs e) => _viewModel.KeyBits = 3072;
    private void OnKeyBits4096(object? sender, TappedEventArgs e) => _viewModel.KeyBits = 4096;
}
