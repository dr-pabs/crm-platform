using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CrmPlatform.StaffBff.Tests.Api;

/// <summary>
/// CLAUDE.md §4: every endpoint must have a 401 test.
/// No token → 401 Unauthorized from auth middleware before handlers run.
/// </summary>
public sealed class BffUnauthorizedTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GET_BffDashboard_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/bff/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_BffLeads_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/bff/leads");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
