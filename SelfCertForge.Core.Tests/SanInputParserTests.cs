using SelfCertForge.Core.Parsing;

namespace SelfCertForge.Core.Tests;

public sealed class SanInputParserTests
{
    [Fact]
    public void Parse_NormalizesMixedEntries_AndAddsDnsPrefixWhenMissing()
    {
        const string input = "helicarrier.local\nDNS:api.local,IP:192.168.1.20;gateway.home";

        var result = SanInputParser.Parse(input);

        result.Should().Equal(
            "DNS:helicarrier.local",
            "DNS:api.local",
            "IP:192.168.1.20",
            "DNS:gateway.home");
    }

    [Fact]
    public void Parse_Throws_WhenInputEmpty()
    {
        var act = () => SanInputParser.Parse(" \n ");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SAN entry is required*");
    }
}
