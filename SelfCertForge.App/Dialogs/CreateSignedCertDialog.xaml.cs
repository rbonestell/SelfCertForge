using SelfCertForge.Core.Presentation;

namespace SelfCertForge.App.Dialogs;

public partial class CreateSignedCertDialog : ContentPage
{
    private readonly CreateSignedCertDialogViewModel _viewModel;

    internal CreateSignedCertDialogViewModel ViewModel => _viewModel;

    public CreateSignedCertDialog(CreateSignedCertDialogViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        InitializeComponent();

        _viewModel.Created += OnCreatedOrCancelled;
        _viewModel.CancelRequested += OnCreatedOrCancelled;
    }

    private void OnCreatedOrCancelled(object? sender, object? e) =>
        MainThread.BeginInvokeOnMainThread(() => _ = Navigation.PopModalAsync(animated: false));

    public void PrepareForOpen(string issuerId, string issuerName) =>
        _viewModel.Initialize(issuerId, issuerName);

    private void OnKeyBits2048(object? sender, TappedEventArgs e) => _viewModel.KeyBits = 2048;
    private void OnKeyBits3072(object? sender, TappedEventArgs e) => _viewModel.KeyBits = 3072;
    private void OnKeyBits4096(object? sender, TappedEventArgs e) => _viewModel.KeyBits = 4096;

    private void OnSanTypeDns(object? sender, TappedEventArgs e) => _viewModel.NewSanType = "DNS";
    private void OnSanTypeIp(object? sender, TappedEventArgs e) => _viewModel.NewSanType = "IP";

    private void OnKeyUsageDigitalSignatureTapped(object? sender, TappedEventArgs e) =>
        _viewModel.KeyUsageDigitalSignature = !_viewModel.KeyUsageDigitalSignature;
    private void OnKeyUsageNonRepudiationTapped(object? sender, TappedEventArgs e) =>
        _viewModel.KeyUsageNonRepudiation = !_viewModel.KeyUsageNonRepudiation;
    private void OnKeyUsageKeyEnciphermentTapped(object? sender, TappedEventArgs e) =>
        _viewModel.KeyUsageKeyEncipherment = !_viewModel.KeyUsageKeyEncipherment;
    private void OnKeyUsageDataEnciphermentTapped(object? sender, TappedEventArgs e) =>
        _viewModel.KeyUsageDataEncipherment = !_viewModel.KeyUsageDataEncipherment;
    private void OnKeyUsageKeyAgreementTapped(object? sender, TappedEventArgs e) =>
        _viewModel.KeyUsageKeyAgreement = !_viewModel.KeyUsageKeyAgreement;
    private void OnKeyUsageEncipherOnlyTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel.CanSetEncipherDecipher)
            _viewModel.KeyUsageEncipherOnly = !_viewModel.KeyUsageEncipherOnly;
    }

    private void OnKeyUsageDecipherOnlyTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel.CanSetEncipherDecipher)
            _viewModel.KeyUsageDecipherOnly = !_viewModel.KeyUsageDecipherOnly;
    }
    private void OnEkuServerAuthTapped(object? sender, TappedEventArgs e) =>
        _viewModel.EkuServerAuth = !_viewModel.EkuServerAuth;
    private void OnEkuClientAuthTapped(object? sender, TappedEventArgs e) =>
        _viewModel.EkuClientAuth = !_viewModel.EkuClientAuth;
}
