namespace SelfCertForge.Core.Parsing;

public static class SanInputParser
{
    public static List<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("At least one SAN entry is required.");
        }

        var tokens = value
            .Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var results = new List<string>(tokens.Length);
        foreach (var token in tokens)
        {
            if (token.StartsWith("DNS:", StringComparison.OrdinalIgnoreCase) ||
                token.StartsWith("IP:", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(token.ToUpperInvariant().StartsWith("DNS:") ? $"DNS:{token[4..]}" : $"IP:{token[3..]}");
                continue;
            }

            results.Add($"DNS:{token}");
        }

        return results;
    }
}
