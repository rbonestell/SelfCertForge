using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;
using Xunit;

namespace SelfCertForge.Core.Tests;

public sealed class AuthoritiesViewModelCsrTests
{
    [Fact]
    public async Task CreateFromCsr_pick_cancelled_returns_silently()
    {
        var picker = new FakePicker(result: null);
        var workflow = new FakeWorkflow();
        var dialog = new FakeDialog();
        var confirm = new FakeConfirmation();
        var row = new AuthorityRowViewModel(
            BuildRoot(), isTrusted: false,
            createSignedCertDialog: new NoopSignedDialog(),
            createFromCsrDialog: dialog, csrFilePicker: picker,
            workflow: workflow, confirmation: confirm,
            nav: new NoopNav());

        await ((AsyncRelayCommand)row.CreateFromCsrCommand).ExecuteAsync(null);

        dialog.WasShown.Should().BeFalse();
        confirm.WasShown.Should().BeFalse();
    }

    [Fact]
    public async Task CreateFromCsr_validation_failure_shows_confirmation_alert_and_does_not_open_dialog()
    {
        var picker = new FakePicker(new CsrFilePickResult("/tmp/bad.csr", "garbage"));
        var workflow = new FakeWorkflow(result: new CsrInspectionResult(
            false, null, new[] { CsrValidationError.Malformed }));
        var dialog = new FakeDialog();
        var confirm = new FakeConfirmation();
        var row = new AuthorityRowViewModel(
            BuildRoot(), false, new NoopSignedDialog(),
            dialog, picker, workflow, confirm, new NoopNav());

        await ((AsyncRelayCommand)row.CreateFromCsrCommand).ExecuteAsync(null);

        confirm.WasShown.Should().BeTrue();
        dialog.WasShown.Should().BeFalse();
    }

    [Fact]
    public async Task CreateFromCsr_valid_csr_opens_dialog_with_summary()
    {
        var summary = new CsrSummary("CN=x", "RSA", 2048, "FP", "PEM",
            Array.Empty<string>(), null, null);
        var picker = new FakePicker(new CsrFilePickResult("/tmp/x.csr", "PEM"));
        var workflow = new FakeWorkflow(new CsrInspectionResult(true, summary, Array.Empty<CsrValidationError>()));
        var dialog = new FakeDialog();
        var confirm = new FakeConfirmation();
        var ca = BuildRoot();
        var row = new AuthorityRowViewModel(
            ca, false, new NoopSignedDialog(),
            dialog, picker, workflow, confirm, new NoopNav());

        await ((AsyncRelayCommand)row.CreateFromCsrCommand).ExecuteAsync(null);

        dialog.WasShown.Should().BeTrue();
        dialog.LastSigningAuthorityId.Should().Be(ca.Id);
        dialog.LastSourceCsrFilename.Should().Be("x.csr");
        dialog.LastSummary.Should().BeSameAs(summary);
    }

    private static StoredCertificate BuildRoot() => new(
        "ca-id", StoredCertificateKind.Root, "Test CA", "CN=Test CA",
        null, null, Array.Empty<string>(), "RSA", "01", "AA", "BB",
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(5), false);

    private sealed class FakePicker : ICsrFilePicker
    {
        private readonly CsrFilePickResult? _r;
        public FakePicker(CsrFilePickResult? result) { _r = result; }
        public Task<CsrFilePickResult?> PickCsrFileAsync(CancellationToken ct = default) => Task.FromResult(_r);
    }

    private sealed class FakeWorkflow : ICertificateWorkflowService
    {
        private readonly CsrInspectionResult? _r;
        public FakeWorkflow(CsrInspectionResult? result = null) { _r = result; }
        public Task<CsrInspectionResult> InspectCsrAsync(string csrPem, CancellationToken ct = default)
            => Task.FromResult(_r ?? new CsrInspectionResult(false, null, new[] { CsrValidationError.Malformed }));
        public Task<CertificateGenerationResult> GenerateRootCertificateAsync(RootCertificateRequest r, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<CertificateGenerationResult> GenerateSignedCertificateAsync(SignedCertificateRequest r, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<CertificateGenerationResult> GenerateCertificateFromCsrAsync(
            CsrSigningRequest r, string a, string b, string c, string d, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeDialog : ICreateFromCsrDialog
    {
        public bool WasShown;
        public string? LastSigningAuthorityId;
        public CsrSummary? LastSummary;
        public string? LastSourceCsrFilename;
        public Task<StoredCertificate?> ShowAsync(string signingAuthorityId, string signingAuthorityName,
            CsrSummary csrSummary, string sourceCsrFilename, CancellationToken ct = default)
        {
            WasShown = true;
            LastSigningAuthorityId = signingAuthorityId;
            LastSummary = csrSummary;
            LastSourceCsrFilename = sourceCsrFilename;
            return Task.FromResult<StoredCertificate?>(null);
        }
    }

    private sealed class FakeConfirmation : IConfirmationDialog
    {
        public bool WasShown;
        public Task<bool> ShowAsync(string t, string m, string c = "Confirm", string x = "Cancel")
        {
            WasShown = true;
            return Task.FromResult(false);
        }
    }

    private sealed class NoopSignedDialog : ICreateSignedCertDialog
    {
        public Task<StoredCertificate?> ShowAsync(string a, string b) => Task.FromResult<StoredCertificate?>(null);
    }

    private sealed class NoopNav : INavigationService
    {
        public void NavigateToCertificate(string certId) { }
        public Task NavigateToAsync(AppRoute route, CancellationToken ct = default) => Task.CompletedTask;
    }
}
