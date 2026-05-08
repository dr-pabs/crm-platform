using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace CrmPlatform.SfaService.Tests.Api;

/// <summary>
/// Verifies every SFA endpoint returns HTTP 401 when no Authorization header is supplied.
/// CLAUDE.md: every endpoint requires a 401 unauthorised test (no token).
/// </summary>
public sealed class SfaUnauthorizedTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Leads ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Leads_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/leads");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Lead_ById_NoToken_Returns401()
    {
        var response = await _client.GetAsync($"/leads/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_Lead_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/leads", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PUT_Lead_NoToken_Returns401()
    {
        var response = await _client.PutAsync($"/leads/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_Lead_Assign_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/leads/{Guid.NewGuid()}/assign", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_Lead_Convert_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/leads/{Guid.NewGuid()}/convert", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DELETE_Lead_NoToken_Returns401()
    {
        var response = await _client.DeleteAsync($"/leads/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Opportunities ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Opportunities_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/opportunities");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Opportunity_ById_NoToken_Returns401()
    {
        var response = await _client.GetAsync($"/opportunities/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_Opportunity_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/opportunities", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_Opportunity_Stage_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/opportunities/{Guid.NewGuid()}/stage", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Contacts ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task POST_Contact_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/contacts", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Contact_ById_NoToken_Returns401()
    {
        var response = await _client.GetAsync($"/contacts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Accounts ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task POST_Account_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/accounts", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Account_ById_NoToken_Returns401()
    {
        var response = await _client.GetAsync($"/accounts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Activities ────────────────────────────────────────────────────────────

    [Fact]
    public async Task POST_Activity_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/activities", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Activities_NoToken_Returns401()
    {
        var response = await _client.GetAsync($"/activities?relatedEntityId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
