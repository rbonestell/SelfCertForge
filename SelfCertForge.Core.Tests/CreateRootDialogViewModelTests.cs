using System.Windows.Input;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.Core.Tests;

public sealed class CreateRootDialogViewModelTests
{
    [Fact]
    public void InitialState_CanSubmit_ValidityIsDefault()
    {
        // CommonName is no longer part of CanSubmit; required-field check happens
        // at submit time so the user sees a visible "required" indicator instead
        // of a silently-disabled button. ValidityDays > 0 still gates.
        var vm = new CreateRootDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.CanSubmit.Should().BeTrue();
    }

    [Fact]
    public void CanSubmit_IsFalse_WhenValidityDaysInvalid()
    {
        var vm = new CreateRootDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.ValidityDays = 0;
        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void Submit_WithBlankCommonName_FlagsErrorAndDoesNotForge()
    {
        var forge = new FakeForgeService(_ => FakeCert());
        var vm = new CreateRootDialogViewModel(forge);
        vm.CommonName = "   ";
        StoredCertificate? raised = null;
        vm.Created += (_, c) => raised = c;

        ((ICommand)vm.CreateCommand).Execute(null);

        vm.CommonNameHasError.Should().BeTrue();
        vm.IsCreating.Should().BeFalse();
        forge.CallCount.Should().Be(0);
        raised.Should().BeNull();
    }

    [Fact]
    public void EditingCommonName_ClearsCommonNameError()
    {
        var vm = new CreateRootDialogViewModel(new FakeForgeService(_ => FakeCert()));
        ((ICommand)vm.CreateCommand).Execute(null);
        vm.CommonNameHasError.Should().BeTrue();

        vm.CommonName = "r";
        vm.CommonNameHasError.Should().BeFalse();
    }

[Fact]
    public void Reset_ClearsAllFields()
    {
        var vm = new CreateRootDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.CommonName = "Anything";
        vm.ValidityDays = 10;
        vm.Reset();
        vm.CommonName.Should().BeEmpty();
        vm.ValidityDays.Should().Be(9125);
        vm.KeyBits.Should().Be(2048);
        vm.ErrorMessage.Should().BeNull();
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public void CancelCommand_RaisesCancelRequestedEvent()
    {
        var vm = new CreateRootDialogViewModel(new FakeForgeService(_ => FakeCert()));
        var fired = false;
        vm.CancelRequested += (_, _) => fired = true;
        ((ICommand)vm.CancelCommand).Execute(null);
        fired.Should().BeTrue();
    }

    [Fact]
    public void CreateCommand_WhenForgeFails_SetsErrorMessage()
    {
        var vm = new CreateRootDialogViewModel(new ThrowingForgeService(new InvalidOperationException("bad")));
        vm.CommonName = "My Root";
        ((ICommand)vm.CreateCommand).Execute(null);
        vm.ErrorMessage.Should().Be("bad");
        vm.HasError.Should().BeTrue();
        vm.IsCreating.Should().BeFalse();
    }

    [Fact]
    public void CreateCommand_WhenForgeSucceeds_RaisesCreatedEvent()
    {
        var cert = FakeCert("c1");
        var vm = new CreateRootDialogViewModel(new FakeForgeService(_ => cert));
        StoredCertificate? raised = null;
        vm.Created += (_, c) => raised = c;
        vm.CommonName = "My Root";
        ((ICommand)vm.CreateCommand).Execute(null);
        raised.Should().Be(cert);
    }

    private static StoredCertificate FakeCert(string id = "c1") => new(
        Id: id, Kind: StoredCertificateKind.Root, CommonName: "Test",
        Subject: "CN=Test", IssuerId: null, IssuerName: null,
        Sans: Array.Empty<string>(), Algorithm: "RSA",
        Serial: "0", Sha256: "", Sha1: "",
        IssuedAt: DateTimeOffset.UtcNow, ExpiresAt: DateTimeOffset.UtcNow.AddYears(1),
        InstalledInTrustStore: false);

    private sealed class FakeForgeService : IForgeService
    {
        private readonly Func<ForgeRequest, StoredCertificate> _factory;
        public int CallCount { get; private set; }
        public FakeForgeService(Func<ForgeRequest, StoredCertificate> factory) => _factory = factory;
        public Task<StoredCertificate> ForgeAsync(ForgeRequest request, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(_factory(request));
        }
    }

    private sealed class ThrowingForgeService : IForgeService
    {
        private readonly Exception _ex;
        public ThrowingForgeService(Exception ex) => _ex = ex;
        public Task<StoredCertificate> ForgeAsync(ForgeRequest request, CancellationToken ct = default) =>
            Task.FromException<StoredCertificate>(_ex);
    }
}
