using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace CrmPlatform.CssService.Tests.Api;

/// <summary>
/// Verifies every CSS endpoint returns HTTP 401 when no Authorization header is supplied.
/// CLAUDE.md: every endpoint requires a 401 unauthorised test (no token).
/// </summary>
public sealed class CssUnauthorizedTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Cases ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Cases_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/cases");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Case_ById_NoToken_Returns401()
    {
        var response = await _client.GetAsync($"/cases/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_Case_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/cases", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_Case_Status_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/cases/{Guid.NewGuid()}/status", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_Case_Assign_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/cases/{Guid.NewGuid()}/assign", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_Case_Comment_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/cases/{Guid.NewGuid()}/comments", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Case_Comments_NoToken_Returns401()
    {
        var response = await _client.GetAsync($"/cases/{Guid.NewGuid()}/comments");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_Case_Escalate_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/cases/{Guid.NewGuid()}/escalate", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── SLA Policies ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_SlaPolicies_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/sla-policies");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_SlaPolicy_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/sla-policies", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
