// SelfCertForge.Core.Tests/CsrValidationErrorMessagesTests.cs
using FluentAssertions;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Validation;
using Xunit;

namespace SelfCertForge.Core.Tests;

public sealed class CsrValidationErrorMessagesTests
{
    [Fact]
    public void Format_Malformed_returns_user_friendly_message()
    {
        var msg = CsrValidationErrorMessages.Format(new[] { CsrValidationError.Malformed });
        msg.Should().Contain("could not be parsed");
    }

    [Fact]
    public void Format_InvalidProofOfPossession_returns_user_friendly_message()
    {
        var msg = CsrValidationErrorMessages.Format(new[] { CsrValidationError.InvalidProofOfPossession });
        msg.Should().Contain("proof-of-possession");
    }

    [Fact]
    public void Format_UnsupportedKeyAlgorithm_returns_user_friendly_message()
    {
        var msg = CsrValidationErrorMessages.Format(new[] { CsrValidationError.UnsupportedKeyAlgorithm });
        msg.Should().Contain("RSA");
    }

    [Fact]
    public void Format_KeyTooSmall_returns_user_friendly_message()
    {
        var msg = CsrValidationErrorMessages.Format(new[] { CsrValidationError.KeyTooSmall });
        msg.Should().Contain("2048");
    }

    [Fact]
    public void Format_SubjectDnEmptyOrMalformed_returns_user_friendly_message()
    {
        var msg = CsrValidationErrorMessages.Format(new[] { CsrValidationError.SubjectDnEmptyOrMalformed });
        msg.Should().Contain("Subject");
    }

    [Fact]
    public void Format_multiple_errors_joins_them_with_newline()
    {
        var msg = CsrValidationErrorMessages.Format(new[]
        {
            CsrValidationError.KeyTooSmall,
            CsrValidationError.SubjectDnEmptyOrMalformed,
        });
        msg.Should().Contain("2048");
        msg.Should().Contain("Subject");
        msg.Should().Contain("\n");
    }

    [Fact]
    public void Format_empty_list_returns_fallback()
    {
        var msg = CsrValidationErrorMessages.Format(Array.Empty<CsrValidationError>());
        msg.Should().NotBeNullOrWhiteSpace();
    }
}
