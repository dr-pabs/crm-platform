using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace CrmPlatform.AnalyticsService.Tests.Api;

/// <summary>
/// Verifies every Analytics endpoint returns HTTP 401 when no Authorization header is supplied.
/// CLAUDE.md: every endpoint requires a 401 unauthorised test (no token).
/// </summary>
public sealed class AnalyticsUnauthorizedTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GET_AnalyticsDashboard_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/analytics/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_AnalyticsMetric_ByKey_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/analytics/metrics/new-leads");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_AnalyticsEvents_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/analytics/events");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
