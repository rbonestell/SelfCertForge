using System.Windows.Input;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.Core.Tests;

public sealed class CreateSignedCertDialogViewModelTests
{
    [Fact]
    public void Initialize_ResetsAllFields()
    {
        var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.Initialize("issuer0", "Old Root");
        vm.CommonName = "Something";
        vm.NewSanValue = "api.local";
        ((ICommand)vm.AddSanCommand).Execute(null);
        vm.SanEntries.Should().ContainSingle();

        vm.Initialize("issuer1", "New Root");

        vm.CommonName.Should().BeEmpty();
        vm.SanEntries.Should().BeEmpty();
        vm.HasSanEntries.Should().BeFalse();
        vm.IssuerName.Should().Be("New Root");
        vm.ValidityDays.Should().Be(397);
    }

    [Fact]
    public void NewSanPlaceholder_DNS_IsApiLocal()
    {
        var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.NewSanType = "DNS";
        vm.NewSanPlaceholder.Should().Be("api.local");
    }

    [Fact]
    public void NewSanPlaceholder_IP_IsLocalhostIp()
    {
        var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.NewSanType = "IP";
        vm.NewSanPlaceholder.Should().Be("127.0.0.1");
    }

    [Fact]
    public void AddSan_AddsToCollection()
    {
        var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.Initialize("issuer1", "Root");
        vm.NewSanValue = "api.local";
        ((ICommand)vm.AddSanCommand).Execute(null);
        vm.SanEntries.Should().ContainSingle();
        vm.HasSanEntries.Should().BeTrue();
        vm.NewSanValue.Should().BeEmpty();
    }

    [Fact]
    public void AddSanCommand_Disabled_WhenNewSanValueEmpty()
    {
        var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.Initialize("issuer1", "Root");
        vm.AddSanCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void RemoveSan_ViaSanEntry()
    {
        var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.Initialize("issuer1", "Root");
        vm.NewSanValue = "api.local";
        ((ICommand)vm.AddSanCommand).Execute(null);
        vm.SanEntries.Should().ContainSingle();

        vm.SanEntries[0].DeleteCommand.Execute(null);

        vm.SanEntries.Should().BeEmpty();
        vm.HasSanEntries.Should().BeFalse();
    }

    [Fact]
    public void KeyAgreement_WhenSetFalse_ClearsEncipherDecipher()
    {
        var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.KeyUsageKeyAgreement = true;
        vm.KeyUsageEncipherOnly = true;
        vm.KeyUsageKeyAgreement = false;
        vm.KeyUsageEncipherOnly.Should().BeFalse();
        vm.KeyUsageDecipherOnly.Should().BeFalse();
        vm.CanSetEncipherDecipher.Should().BeFalse();
    }

    [Fact]
    public void EncipherOnly_WhenSetTrue_ClearsDecipherOnly()
    {
        var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.KeyUsageKeyAgreement = true;
        vm.KeyUsageDecipherOnly = true;
        vm.KeyUsageEncipherOnly = true;
        vm.KeyUsageDecipherOnly.Should().BeFalse();
    }

    [Fact]
    public void CanSubmit_RequiresIssuerId()
    {
        // CommonName is no longer part of CanSubmit gating — required-field
        // check fires at submit time. Issuer + validity > 0 still gate.
        var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.CanSubmit.Should().BeFalse(); // no issuer yet

        vm.Initialize("issuer1", "Root");
        vm.CanSubmit.Should().BeTrue();
    }

    [Fact]
    public void Submit_WithBlankCommonName_FlagsErrorAndDoesNotForge()
    {
        var forge = new FakeForgeService(_ => FakeCert());
        var vm = new CreateSignedCertDialogViewModel(forge);
        vm.Initialize("issuer1", "Root");

        ((ICommand)vm.CreateCommand).Execute(null);

        vm.CommonNameHasError.Should().BeTrue();
        vm.IsCreating.Should().BeFalse();
        forge.CallCount.Should().Be(0);
    }

    [Fact]
    public void EditingCommonName_ClearsCommonNameError()
    {
        var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.Initialize("issuer1", "Root");
        ((ICommand)vm.CreateCommand).Execute(null);
        vm.CommonNameHasError.Should().BeTrue();

        vm.CommonName = "leaf.local";
        vm.CommonNameHasError.Should().BeFalse();
    }

    [Fact]
    public void AddSan_RejectsInvalidDns_SetsValidationError()
    {
        var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.Initialize("issuer1", "Root");
        vm.NewSanType = "DNS";
        vm.NewSanValue = "foo bar"; // space → invalid
        ((ICommand)vm.AddSanCommand).Execute(null);

        vm.SanEntries.Should().BeEmpty();
        vm.HasSanValidationError.Should().BeTrue();
        vm.SanValidationError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void AddSan_RejectsInvalidIp_SetsValidationError()
    {
        var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.Initialize("issuer1", "Root");
        vm.NewSanType = "IP";
        vm.NewSanValue = "256.256.256.256";
        ((ICommand)vm.AddSanCommand).Execute(null);

        vm.SanEntries.Should().BeEmpty();
        vm.HasSanValidationError.Should().BeTrue();
    }

    [Fact]
    public void AddSan_AcceptsValidIpv6()
    {
        var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.Initialize("issuer1", "Root");
        vm.NewSanType = "IP";
        vm.NewSanValue = "::1";
        ((ICommand)vm.AddSanCommand).Execute(null);

        vm.SanEntries.Should().ContainSingle();
        vm.HasSanValidationError.Should().BeFalse();
    }

    [Fact]
    public void EditingSanValue_ClearsValidationError()
    {
        var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.Initialize("issuer1", "Root");
        vm.NewSanValue = "foo bar";
        ((ICommand)vm.AddSanCommand).Execute(null);
        vm.HasSanValidationError.Should().BeTrue();

        vm.NewSanValue = "good.example";
        vm.HasSanValidationError.Should().BeFalse();
    }

    [Fact]
    public void SwitchingSanType_ClearsValidationError()
    {
        var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.Initialize("issuer1", "Root");
        vm.NewSanType = "DNS";
        vm.NewSanValue = "not an ip";
        ((ICommand)vm.AddSanCommand).Execute(null); // valid as DNS? no, has space → invalid
        vm.HasSanValidationError.Should().BeTrue();

        vm.NewSanType = "IP";
        vm.HasSanValidationError.Should().BeFalse();
    }

    [Fact]
    public void ValidityDays_SetToZero_FlagsErrorAndBlocksSubmit()
    {
        var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.Initialize("issuer1", "Root");
        vm.CommonName = "leaf.local";
        vm.CanSubmit.Should().BeTrue();

        vm.ValidityDays = 0;

        vm.ValidityHasError.Should().BeTrue();
        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void ValidityDays_SetToPositive_ClearsError()
    {
        var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()));
        vm.Initialize("issuer1", "Root");
        vm.ValidityDays = 0;
        vm.ValidityHasError.Should().BeTrue();

        vm.ValidityDays = 30;
        vm.ValidityHasError.Should().BeFalse();
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
}
