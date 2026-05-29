using FluentAssertions;
using SelfCertForge.Core.Abstractions;
using Xunit;

namespace SelfCertForge.Core.Tests;

public sealed class LoadingOverlayExtensionsTests
{
    [Fact]
    public async Task RunOrDirectAsync_NullOverlay_RunsOperation()
    {
        var ran = false;
        ILoadingOverlay? overlay = null;

        await overlay.RunOrDirectAsync("msg", () => { ran = true; return Task.CompletedTask; });

        ran.Should().BeTrue();
    }

    [Fact]
    public async Task RunOrDirectAsync_WithOverlay_RecordsMessageAndReturnsValue()
    {
        var overlay = new FakeLoadingOverlay();

        var result = await overlay.RunOrDirectAsync("Working…", () => Task.FromResult(42));

        result.Should().Be(42);
        overlay.Messages.Should().ContainSingle().Which.Should().Be("Working…");
    }
}
