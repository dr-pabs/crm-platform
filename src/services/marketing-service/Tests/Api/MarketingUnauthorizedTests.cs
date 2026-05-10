using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace CrmPlatform.MarketingService.Tests.Api;

/// <summary>
/// Verifies every Marketing endpoint returns HTTP 401 when no Authorization header is supplied.
/// CLAUDE.md: every endpoint requires a 401 unauthorised test (no token).
/// </summary>
public sealed class MarketingUnauthorizedTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Campaigns ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Campaigns_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/campaigns");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Campaign_ById_NoToken_Returns401()
    {
        var response = await _client.GetAsync($"/campaigns/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_Campaign_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/campaigns", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_Campaign_Status_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/campaigns/{Guid.NewGuid()}/status", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Journeys ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Journeys_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/journeys");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Journey_ById_NoToken_Returns401()
    {
        var response = await _client.GetAsync($"/journeys/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_Journey_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/journeys", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_Journey_Publish_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/journeys/{Guid.NewGuid()}/publish", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_Journey_Enroll_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/journeys/{Guid.NewGuid()}/enroll", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Email Templates ───────────────────────────────────────────────────────

    [Fact]
    public async Task GET_EmailTemplates_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/email-templates");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_EmailTemplate_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/email-templates", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
