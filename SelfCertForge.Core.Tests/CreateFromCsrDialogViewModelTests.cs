using FluentAssertions;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;
using Xunit;

namespace SelfCertForge.Core.Tests;

public class CreateFromCsrDialogViewModelTests
{
    private static CsrSummary BasicSummary(
        IReadOnlyList<string>? sans = null,
        CsrRequestedKeyUsages? ku = null,
        CsrRequestedEkus? eku = null) =>
        new("CN=device", "RSA", 2048, "ABCD", "PEM",
            sans ?? Array.Empty<string>(), ku, eku);

    [Fact]
    public void Initialize_locks_subject_and_keysize_seeds_validity()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        vm.Initialize("ca", "Test CA", BasicSummary(), "thing.csr");

        vm.SubjectDistinguishedName.Should().Be("CN=device");
        vm.PublicKeyBits.Should().Be(2048);
        vm.SigningAuthorityName.Should().Be("Test CA");
        vm.SourceCsrFilename.Should().Be("thing.csr");
        vm.ValidityDays.Should().Be(397);
    }

    [Fact]
    public void Initialize_prefills_sans_with_FromCsr_origin()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        vm.Initialize("ca", "Test CA",
            BasicSummary(sans: new[] { "a.example", "b.example" }), "x.csr");

        vm.SanEntries.Should().HaveCount(2);
        vm.SanEntries.Should().OnlyContain(s => s.Origin == CsrSignedSanOrigin.FromCsr);
    }

    [Fact]
    public void AddSan_appends_AddedByOperator()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        vm.Initialize("ca", "Test CA", BasicSummary(), "x.csr");
        vm.NewSanValue = "extra.example";
        vm.AddSanCommand.Execute(null);

        vm.SanEntries.Should().ContainSingle();
        vm.SanEntries[0].Value.Should().Be("extra.example");
        vm.SanEntries[0].Origin.Should().Be(CsrSignedSanOrigin.AddedByOperator);
        vm.NewSanValue.Should().Be(string.Empty);
    }

    [Fact]
    public void RemoveSan_works_for_either_origin()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        vm.Initialize("ca", "Test CA", BasicSummary(sans: new[] { "a" }), "x.csr");
        vm.NewSanValue = "b";
        vm.AddSanCommand.Execute(null);

        vm.SanEntries[0].RemoveCommand.Execute(null);
        vm.SanEntries.Should().ContainSingle();
        vm.SanEntries[0].Value.Should().Be("b");
    }

    [Fact]
    public void KeyUsage_locked_when_csr_supplies_one()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        var ku = new CsrRequestedKeyUsages(true, false, true, false, false, false, false);
        vm.Initialize("ca", "Test CA", BasicSummary(ku: ku), "x.csr");

        vm.IsKeyUsageLocked.Should().BeTrue();
        vm.KeyUsageDigitalSignature.Should().BeTrue();
        vm.KeyUsageKeyEncipherment.Should().BeTrue();
        vm.KeyUsageNonRepudiation.Should().BeFalse();
    }

    [Fact]
    public void KeyUsage_editable_when_csr_omits_it()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        vm.Initialize("ca", "Test CA", BasicSummary(ku: null), "x.csr");

        vm.IsKeyUsageLocked.Should().BeFalse();
    }

    [Fact]
    public void Eku_locked_when_csr_supplies_one()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        var eku = new CsrRequestedEkus(true, true, false, false, false);
        vm.Initialize("ca", "Test CA", BasicSummary(eku: eku), "x.csr");

        vm.IsEkuLocked.Should().BeTrue();
        vm.EkuServerAuth.Should().BeTrue();
        vm.EkuClientAuth.Should().BeTrue();
    }

    [Fact]
    public void CanSubmit_false_when_ValidityDays_zero()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        vm.Initialize("ca", "Test CA", BasicSummary(), "x.csr");
        vm.ValidityDays = 0;
        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void CanSubmit_false_when_SigningAuthorityId_empty()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        vm.Initialize("", "Test CA", BasicSummary(), "x.csr");
        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public async Task Submit_calls_ForgeFromCsr_and_raises_Created()
    {
        var forge = new FakeForge();
        var vm = new CreateFromCsrDialogViewModel(forge, preferences: null);
        vm.Initialize("ca", "Test CA", BasicSummary(), "x.csr");

        StoredCertificate? captured = null;
        vm.Created += (_, c) => captured = c;

        await vm.SubmitAsyncForTest();

        captured.Should().NotBeNull();
        forge.LastRequest!.SigningRequest.SigningAuthorityId.Should().Be("ca");
        forge.LastRequest.SigningRequest.SourceCsrFilename.Should().Be("x.csr");
    }

    private sealed class FakeForge : IForgeService
    {
        public ForgeFromCsrRequest? LastRequest { get; private set; }

        public Task<StoredCertificate> ForgeAsync(ForgeRequest r, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<StoredCertificate> ForgeFromCsrAsync(ForgeFromCsrRequest r, CancellationToken ct = default)
        {
            LastRequest = r;
            return Task.FromResult(new StoredCertificate(
                "id", StoredCertificateKind.Child, "device", "CN=device",
                r.SigningRequest.SigningAuthorityId, "Test CA", Array.Empty<string>(),
                "RSA", "01", "AA", "BB",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1),
                false, null, null, null, null, null, true, r.SigningRequest.SourceCsrFilename));
        }
    }
}
