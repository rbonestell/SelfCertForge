using SelfCertForge.Core.Models;
using SelfCertForge.Infrastructure;

namespace SelfCertForge.Core.Tests;

public sealed class UserPreferencesStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(),
        "selfcertforge-prefstests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void NewStore_BeforeLoad_ReturnsDefaults()
    {
        var store = new JsonUserPreferencesStore(_dir);
        store.Current.Should().Be(UserPreferences.Default);
        store.Current.RootValidityDays.Should().Be(9125);
        store.Current.SignedValidityDays.Should().Be(397);
        store.Current.KeyBits.Should().Be(2048);
        store.Current.HashAlgorithm.Should().Be(HashAlgorithmKind.Sha256);
        store.Current.ActivityRetention.Should().Be(ActivityRetention.FiveHundred);
    }

    [Fact]
    public async Task SaveAndReload_RoundTripsAllFields()
    {
        var first = new JsonUserPreferencesStore(_dir);
        var prefs = new UserPreferences
        {
            RootValidityDays = 1825,
            SignedValidityDays = 90,
            KeyBits = 4096,
            HashAlgorithm = HashAlgorithmKind.Sha384,
            DefaultOrganization = "Acme",
            DefaultOrganizationalUnit = "Eng",
            DefaultLocality = "Durango",
            DefaultStateOrProvince = "CO",
            DefaultCountry = "US",
            DefaultEmail = "ops@acme.test",
            ActivityRetention = ActivityRetention.OneThousand,
        };
        await first.SaveAsync(prefs);

        var second = new JsonUserPreferencesStore(_dir);
        await second.LoadAsync();

        second.Current.Should().Be(prefs);
    }

    [Fact]
    public async Task Save_FiresChangedEvent_WithNewValues()
    {
        var store = new JsonUserPreferencesStore(_dir);
        UserPreferences? observed = null;
        store.Changed += (_, p) => observed = p;

        var prefs = UserPreferences.Default with { KeyBits = 4096 };
        await store.SaveAsync(prefs);

        observed.Should().Be(prefs);
    }

    [Fact]
    public async Task LoadAsync_OnCorruptFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(Path.Combine(_dir, "preferences.json"), "{ this is not json }");

        var store = new JsonUserPreferencesStore(_dir);
        await store.LoadAsync();

        store.Current.Should().Be(UserPreferences.Default);
    }

    [Fact]
    public async Task LoadAsync_OnMissingFile_ReturnsDefaults()
    {
        var store = new JsonUserPreferencesStore(_dir);
        await store.LoadAsync();
        store.Current.Should().Be(UserPreferences.Default);
    }
}
