using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace CrmPlatform.IdentityService.Tests.Api;

/// <summary>
/// Verifies every Identity endpoint returns HTTP 401 when no Authorization header is supplied.
/// CLAUDE.md: every endpoint requires a 401 unauthorised test (no token).
/// PlatformAdmin endpoints must additionally return 403 for tenant-scoped JWTs (covered separately).
/// </summary>
public sealed class IdentityUnauthorizedTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Users ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Users_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/identity/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_ProvisionUser_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/identity/users/provision", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_DeprovisionUser_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/identity/users/{Guid.NewGuid()}/deprovision", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Roles ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_UserRoles_NoToken_Returns401()
    {
        var response = await _client.GetAsync($"/identity/users/{Guid.NewGuid()}/roles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_GrantRole_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/identity/users/{Guid.NewGuid()}/roles/grant", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_RevokeRole_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/identity/users/{Guid.NewGuid()}/roles/revoke", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Tenant Registry ───────────────────────────────────────────────────────

    [Fact]
    public async Task GET_TenantRegistry_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/identity/tenant-registry");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Consent ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task POST_RecordConsent_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/identity/consent", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
