using System.Net;
using System.Net.Http.Json;
using CrmPlatform.StaffBff.Api.Dtos;
using CrmPlatform.StaffBff.Application;
using CrmPlatform.StaffBff.Infrastructure.ServiceClients;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrmPlatform.StaffBff.Tests.Application;

public sealed class DashboardAggregatorTests
{
    [Fact]
    public async Task AggregateAsync_AllServicesRespond_ReturnsCombinedPayload()
    {
        // Arrange — stub all four downstream services with in-memory handlers
        var sfaHandler       = new StubbedHttpHandler(new SfaSummaryPayload());
        var cssHandler       = new StubbedHttpHandler(new CssSummaryPayload());
        var marketingHandler = new StubbedHttpHandler(new MarketingSummaryPayload());
        var analyticsHandler = new StubbedHttpHandler(new AnalyticsSummaryPayload());

        var sfaClient       = new SfaServiceClient(MakeClient(sfaHandler, "http://sfa/"));
        var cssClient       = new CssServiceClient(MakeClient(cssHandler, "http://css/"));
        var marketingClient = new MarketingServiceClient(MakeClient(marketingHandler, "http://mkt/"));
        var analyticsClient = new AnalyticsServiceClient(MakeClient(analyticsHandler, "http://ana/"));

        var aggregator = new DashboardAggregator(sfaClient, cssClient, marketingClient, analyticsClient);

        // Act
        var result = await aggregator.AggregateAsync();

        // Assert structure is populated
        Assert.NotNull(result);
        Assert.True(result.GeneratedAt > DateTimeOffset.UtcNow.AddSeconds(-5));
        Assert.NotNull(result.Sfa);
        Assert.NotNull(result.Css);
        Assert.NotNull(result.Marketing);
        Assert.NotNull(result.Analytics);
    }

    private static HttpClient MakeClient(HttpMessageHandler handler, string baseAddress)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri(baseAddress) };
        return client;
    }

    // ── Stub payloads matching the internal DTO shapes ────────────────────────

    private sealed record SfaSummaryPayload(
        int OpenLeads = 5, int OpenOpportunities = 3,
        decimal PipelineValue = 50000m, int ActivitiesThisWeek = 12);

    private sealed record CssSummaryPayload(
        int Open = 4, int BreachedSla = 1,
        int ResolvedThisWeek = 8, double AvgResolutionHours = 24.5);

    private sealed record MarketingSummaryPayload(
        int ActiveCampaigns = 2, int LeadsGeneratedThisMonth = 30,
        double EmailOpenRatePct = 28.5);

    private sealed record AnalyticsSummaryPayload(
        double ConversionRatePct = 3.2, double RevenueThisMonth = 120000,
        int NewCustomers = 7);

    /// <summary>Returns 200 OK with JSON-serialised T for any request.</summary>
    private sealed class StubbedHttpHandler(object payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload)
            };
            return Task.FromResult(response);
        }
    }
}
