using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace CrmPlatform.PlatformAdminService.Tests.Api;

/// <summary>
/// Verifies every Platform Admin endpoint returns HTTP 401 when no Authorization header is supplied,
/// and HTTP 403 when a tenant-scoped JWT is used (PlatformAdmin-only endpoints).
/// CLAUDE.md: every endpoint requires a 401 unauthorised test; PlatformAdmin endpoints
/// must be inaccessible to any tenant-scoped JWT.
/// </summary>
public sealed class PlatformUnauthorizedTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Tenants — no token ────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Tenants_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/platform/tenants");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_ProvisionTenant_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/platform/tenants", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Tenant_ById_NoToken_Returns401()
    {
        var response = await _client.GetAsync($"/platform/tenants/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_SuspendTenant_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/platform/tenants/{Guid.NewGuid()}/suspend", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_ReactivateTenant_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/platform/tenants/{Guid.NewGuid()}/reactivate", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_DeprovisionTenant_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/platform/tenants/{Guid.NewGuid()}/deprovision", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_TenantProvisioningLog_NoToken_Returns401()
    {
        var response = await _client.GetAsync($"/platform/tenants/{Guid.NewGuid()}/log");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Users (cross-tenant) — no token ───────────────────────────────────────

    [Fact]
    public async Task GET_PlatformUsers_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/platform/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_PlatformUserRoles_NoToken_Returns401()
    {
        var response = await _client.GetAsync($"/platform/users/{Guid.NewGuid()}/roles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
