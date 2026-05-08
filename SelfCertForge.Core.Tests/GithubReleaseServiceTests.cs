using System.Net;
using System.Text;
using SelfCertForge.Infrastructure;

namespace SelfCertForge.Core.Tests;

public sealed class GithubReleaseServiceTests
{
    [Fact]
    public async Task RefreshAsync_WhenApiReturnsTag_StripsLeadingV()
    {
        var http = new HttpClient(new StubHandler(HttpStatusCode.OK,
            """{"tag_name":"v0.0.2","name":"0.0.2"}"""));
        var svc = new GithubReleaseService(http, "rbonestell", "SelfCertForge");

        await svc.RefreshAsync();

        svc.LatestPublishedVersion.Should().Be("0.0.2");
    }

    [Fact]
    public async Task RefreshAsync_WhenTagHasNoLeadingV_StoresVerbatim()
    {
        var http = new HttpClient(new StubHandler(HttpStatusCode.OK,
            """{"tag_name":"1.2.3"}"""));
        var svc = new GithubReleaseService(http, "rbonestell", "SelfCertForge");

        await svc.RefreshAsync();

        svc.LatestPublishedVersion.Should().Be("1.2.3");
    }

    [Fact]
    public async Task RefreshAsync_RaisesChanged_OnlyWhenValueDiffers()
    {
        var http = new HttpClient(new StubHandler(HttpStatusCode.OK,
            """{"tag_name":"v1.0.0"}"""));
        var svc = new GithubReleaseService(http, "rbonestell", "SelfCertForge");
        var fired = 0;
        svc.Changed += (_, _) => fired++;

        await svc.RefreshAsync();
        await svc.RefreshAsync(); // same value — should not fire again

        fired.Should().Be(1);
        svc.LatestPublishedVersion.Should().Be("1.0.0");
    }

    [Fact]
    public async Task RefreshAsync_OnNon200_LeavesValueUnchanged()
    {
        var http = new HttpClient(new StubHandler(HttpStatusCode.TooManyRequests, "rate limited"));
        var svc = new GithubReleaseService(http, "rbonestell", "SelfCertForge");

        await svc.RefreshAsync();

        svc.LatestPublishedVersion.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_OnNetworkException_LeavesValueUnchanged()
    {
        var http = new HttpClient(new ThrowingHandler(new HttpRequestException("offline")));
        var svc = new GithubReleaseService(http, "rbonestell", "SelfCertForge");

        await svc.RefreshAsync();

        svc.LatestPublishedVersion.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_OnMalformedJson_LeavesValueUnchanged()
    {
        var http = new HttpClient(new StubHandler(HttpStatusCode.OK, "{ not json"));
        var svc = new GithubReleaseService(http, "rbonestell", "SelfCertForge");

        await svc.RefreshAsync();

        svc.LatestPublishedVersion.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_OnMissingTagName_LeavesValueUnchanged()
    {
        var http = new HttpClient(new StubHandler(HttpStatusCode.OK,
            """{"name":"unreleased"}"""));
        var svc = new GithubReleaseService(http, "rbonestell", "SelfCertForge");

        await svc.RefreshAsync();

        svc.LatestPublishedVersion.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_HitsCorrectEndpoint_WithUserAgent()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """{"tag_name":"v1"}""");
        var http = new HttpClient(handler);
        var svc = new GithubReleaseService(http, "alice", "widgets");

        await svc.RefreshAsync();

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri.Should().Be(
            new Uri("https://api.github.com/repos/alice/widgets/releases/latest"));
        handler.LastRequest.Headers.UserAgent.ToString().Should().Contain("SelfCertForge");
    }

    [Fact]
    public void Constructor_RejectsBlankOwnerOrRepo()
    {
        var http = new HttpClient();
        Action a = () => new GithubReleaseService(http, "", "repo");
        Action b = () => new GithubReleaseService(http, "owner", "  ");
        a.Should().Throw<ArgumentException>();
        b.Should().Throw<ArgumentException>();
    }

    // -- Test handlers -------------------------------------------------------

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public StubHandler(HttpStatusCode status, string body) { _status = status; _body = body; }
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public HttpRequestMessage? LastRequest { get; private set; }
        public RecordingHandler(HttpStatusCode status, string body) { _status = status; _body = body; }
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _ex;
        public ThrowingHandler(Exception ex) => _ex = ex;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => throw _ex;
    }
}
