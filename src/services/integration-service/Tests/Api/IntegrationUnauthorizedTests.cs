using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace CrmPlatform.IntegrationService.Tests.Api;

/// <summary>
/// Verifies every Integration endpoint returns HTTP 401 when no Authorization header is supplied.
/// CLAUDE.md: every endpoint requires a 401 unauthorised test (no token).
/// </summary>
public sealed class IntegrationUnauthorizedTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Connectors ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Connectors_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/integrations/connectors");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_CreateConnector_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/integrations/connectors", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DELETE_Connector_NoToken_Returns401()
    {
        var response = await _client.DeleteAsync($"/integrations/connectors/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_DisconnectConnector_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/integrations/connectors/{Guid.NewGuid()}/disconnect", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_OAuthAuthorize_NoToken_Returns401()
    {
        var response = await _client.GetAsync($"/integrations/connectors/{Guid.NewGuid()}/oauth/authorize");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Outbound Jobs ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_OutboundJobs_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/integrations/jobs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_ReplayJob_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/integrations/jobs/{Guid.NewGuid()}/replay", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
