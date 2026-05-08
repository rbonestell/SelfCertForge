using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.Core.Tests;

public sealed class CreateRootDialogPreferencesTests
{
    [Fact]
    public void Constructor_SeedsDefaultsFromPreferences()
    {
        var prefs = new FakePrefs(new UserPreferences
        {
            RootValidityDays = 1825,
            KeyBits = 4096,
            HashAlgorithm = HashAlgorithmKind.Sha384,
            DefaultOrganization = "Acme",
            DefaultEmail = "ops@acme.test",
            DefaultCountry = "US",
        });

        var vm = new CreateRootDialogViewModel(new FakeForge(), prefs);

        vm.ValidityDays.Should().Be(1825);
        vm.KeyBits.Should().Be(4096);
        vm.HashAlgorithm.Should().Be(HashAlgorithmKind.Sha384);
        vm.Organization.Should().Be("Acme");
        vm.EmailAddress.Should().Be("ops@acme.test");
        vm.Country.Should().Be("US");
    }

    [Fact]
    public void Reset_ReSeedsFromPreferences()
    {
        var prefs = new FakePrefs(new UserPreferences { DefaultOrganization = "Acme", RootValidityDays = 1000 });
        var vm = new CreateRootDialogViewModel(new FakeForge(), prefs);

        vm.CommonName = "manual";
        vm.Organization = "manual override";
        vm.ValidityDays = 5;

        vm.Reset();

        vm.CommonName.Should().BeEmpty();
        vm.Organization.Should().Be("Acme");
        vm.ValidityDays.Should().Be(1000);
    }

    [Fact]
    public void Submit_PassesHashAlgorithmThroughToForgeRequest()
    {
        ForgeRequest? captured = null;
        var forge = new FakeForge(req => { captured = req; return Stored(); });
        var prefs = new FakePrefs(new UserPreferences { HashAlgorithm = HashAlgorithmKind.Sha512 });
        var vm = new CreateRootDialogViewModel(forge, prefs);
        vm.CommonName = "root.local";

        ((System.Windows.Input.ICommand)vm.CreateCommand).Execute(null);

        captured.Should().NotBeNull();
        captured!.HashAlgorithm.Should().Be(HashAlgorithmKind.Sha512);
    }

    private static StoredCertificate Stored() => new(
        Id: "c1", Kind: StoredCertificateKind.Root, CommonName: "Test",
        Subject: "CN=Test", IssuerId: null, IssuerName: null,
        Sans: Array.Empty<string>(), Algorithm: "RSA",
        Serial: "0", Sha256: "", Sha1: "",
        IssuedAt: DateTimeOffset.UtcNow, ExpiresAt: DateTimeOffset.UtcNow.AddYears(1),
        InstalledInTrustStore: false);

    private sealed class FakePrefs : IUserPreferencesStore
    {
        public FakePrefs(UserPreferences current) => Current = current;
        public UserPreferences Current { get; private set; }
        public event EventHandler<UserPreferences>? Changed;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAsync(UserPreferences prefs, CancellationToken ct = default)
        {
            Current = prefs;
            Changed?.Invoke(this, prefs);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeForge : IForgeService
    {
        private readonly Func<ForgeRequest, StoredCertificate> _f;
        public FakeForge() : this(_ => Stored()) { }
        public FakeForge(Func<ForgeRequest, StoredCertificate> f) => _f = f;
        public Task<StoredCertificate> ForgeAsync(ForgeRequest request, CancellationToken ct = default) =>
            Task.FromResult(_f(request));
    }
}
