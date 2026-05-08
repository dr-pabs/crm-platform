using CrmPlatform.StaffBff.Api.Dtos;
using CrmPlatform.StaffBff.Infrastructure.ServiceClients;

namespace CrmPlatform.StaffBff.Application;

/// <summary>
/// Fans out to all four domain services in parallel and assembles a composite
/// dashboard payload. All calls share the inbound Bearer token via BearerTokenHandler.
/// ADR 0005: only this service may fan-out within a single request cycle.
/// </summary>
public sealed class DashboardAggregator(
    SfaServiceClient       sfa,
    CssServiceClient       css,
    MarketingServiceClient marketing,
    AnalyticsServiceClient analytics)
{
    public async Task<StaffDashboardResponse> AggregateAsync(CancellationToken ct = default)
    {
        var sfaTask       = sfa.GetSummaryAsync(ct);
        var cssTask       = css.GetSummaryAsync(ct);
        var marketingTask = marketing.GetSummaryAsync(ct);
        var analyticsTask = analytics.GetSummaryAsync(ct);

        await Task.WhenAll(sfaTask, cssTask, marketingTask, analyticsTask);

        return new StaffDashboardResponse(
            sfaTask.Result,
            cssTask.Result,
            marketingTask.Result,
            analyticsTask.Result,
            DateTimeOffset.UtcNow);
    }
}
