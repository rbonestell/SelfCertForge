using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.App.Dialogs;

public partial class CreateFromCsrDialog : ContentPage
{
    private readonly CreateFromCsrDialogViewModel _viewModel;

    internal CreateFromCsrDialogViewModel ViewModel => _viewModel;

    public CreateFromCsrDialog(CreateFromCsrDialogViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        InitializeComponent();

        _viewModel.Created += OnCreatedOrCancelled;
        _viewModel.CancelRequested += OnCreatedOrCancelled;
    }

    private void OnCreatedOrCancelled(object? sender, object? e) =>
        MainThread.BeginInvokeOnMainThread(() => _ = Navigation.PopModalAsync(animated: false));

    public void PrepareForOpen(string signingAuthorityId, string signingAuthorityName,
        CsrSummary csrSummary, string sourceCsrFilename) =>
        _viewModel.Initialize(signingAuthorityId, signingAuthorityName, csrSummary, sourceCsrFilename);

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
    private void OnKeyUsageKeyCertSignTapped(object? sender, TappedEventArgs e) =>
        _viewModel.KeyUsageKeyCertSign = !_viewModel.KeyUsageKeyCertSign;
    private void OnKeyUsageCrlSignTapped(object? sender, TappedEventArgs e) =>
        _viewModel.KeyUsageCrlSign = !_viewModel.KeyUsageCrlSign;

    private void OnEkuServerAuthTapped(object? sender, TappedEventArgs e) =>
        _viewModel.EkuServerAuth = !_viewModel.EkuServerAuth;
    private void OnEkuClientAuthTapped(object? sender, TappedEventArgs e) =>
        _viewModel.EkuClientAuth = !_viewModel.EkuClientAuth;
    private void OnEkuCodeSigningTapped(object? sender, TappedEventArgs e) =>
        _viewModel.EkuCodeSigning = !_viewModel.EkuCodeSigning;
    private void OnEkuTimeStampingTapped(object? sender, TappedEventArgs e) =>
        _viewModel.EkuTimeStamping = !_viewModel.EkuTimeStamping;
    private void OnEkuEmailProtectionTapped(object? sender, TappedEventArgs e) =>
        _viewModel.EkuEmailProtection = !_viewModel.EkuEmailProtection;

    private void OnHashSha256(object? sender, EventArgs e) =>
        _viewModel.HashAlgorithm = HashAlgorithmKind.Sha256;
    private void OnHashSha384(object? sender, EventArgs e) =>
        _viewModel.HashAlgorithm = HashAlgorithmKind.Sha384;
    private void OnHashSha512(object? sender, EventArgs e) =>
        _viewModel.HashAlgorithm = HashAlgorithmKind.Sha512;
}
