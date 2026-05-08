using SelfCertForge.Core.Validation;

namespace SelfCertForge.Core.Tests;

public sealed class SanRulesTests
{
    [Theory]
    [InlineData("api.local")]
    [InlineData("a")]
    [InlineData("foo-bar.example.com")]
    [InlineData("ABC.def.ghi")]
    [InlineData("0123.4567")]
    [InlineData("a-b-c.d-e-f")]
    public void ValidateDns_AcceptsWellFormed(string input)
    {
        var r = SanRules.ValidateDns(input);
        r.IsValid.Should().BeTrue($"input was '{input}', error '{r.Error}'");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("foo..bar")]
    [InlineData("-foo.bar")]
    [InlineData("foo-.bar")]
    [InlineData("foo.bar-")]
    [InlineData("foo_bar.example")]
    [InlineData("foo bar")]
    [InlineData("foo!bar")]
    [InlineData("héllo.test")]
    [InlineData("foo.bar/baz")]
    public void ValidateDns_RejectsBad(string input)
    {
        SanRules.ValidateDns(input).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateDns_RejectsLabelLongerThan63()
    {
        var label = new string('a', 64);
        SanRules.ValidateDns(label).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("2001:db8::1")]
    [InlineData("fe80::1")]
    public void ValidateIp_AcceptsValid(string input)
    {
        SanRules.ValidateIp(input).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("256.0.0.1")]
    [InlineData("not-an-ip")]
    [InlineData("1.2.3")]
    [InlineData("1.2.3.4.5")]
    [InlineData("zz::1")]
    public void ValidateIp_RejectsInvalid(string input)
    {
        SanRules.ValidateIp(input).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("DNS", "api.local", true)]
    [InlineData("dns", "api.local", true)]
    [InlineData("IP", "127.0.0.1", true)]
    [InlineData("ip", "::1", true)]
    [InlineData("DNS", "1.2.3.4", true)]   // numeric DNS labels are technically valid syntactically
    [InlineData("IP", "api.local", false)]
    [InlineData("DNS", "foo bar", false)]
    public void Validate_DispatchesByType(string type, string value, bool expected)
    {
        SanRules.Validate(type, value).IsValid.Should().Be(expected);
    }
}
