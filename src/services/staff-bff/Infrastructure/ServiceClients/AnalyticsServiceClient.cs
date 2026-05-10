using System.Net.Http.Json;
using CrmPlatform.StaffBff.Api.Dtos;

namespace CrmPlatform.StaffBff.Infrastructure.ServiceClients;

public sealed class AnalyticsServiceClient(HttpClient http)
{
    public async Task<AnalyticsSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var from = DateTimeOffset.UtcNow.AddDays(-30).ToString("o");
        var to   = DateTimeOffset.UtcNow.ToString("o");

        var result = await http.GetFromJsonAsync<AnalyticsSummaryDto>(
            $"/analytics/dashboard?from={from}&to={to}", ct)
            ?? throw new InvalidOperationException("analytics-service returned null dashboard");

        return new AnalyticsSummary(
            result.ConversionRatePct,
            result.RevenueThisMonth,
            result.NewCustomers);
    }

    private sealed record AnalyticsSummaryDto(
        double ConversionRatePct,
        double RevenueThisMonth,
        int    NewCustomers);
}
