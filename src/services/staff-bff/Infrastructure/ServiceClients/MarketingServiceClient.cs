using System.Net.Http.Json;
using CrmPlatform.StaffBff.Api.Dtos;

namespace CrmPlatform.StaffBff.Infrastructure.ServiceClients;

public sealed class MarketingServiceClient(HttpClient http)
{
    public async Task<MarketingSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var result = await http.GetFromJsonAsync<MarketingSummaryDto>("/internal/bff/summary", ct)
            ?? throw new InvalidOperationException("marketing-service returned null summary");

        return new MarketingSummary(
            result.ActiveCampaigns,
            result.LeadsGeneratedThisMonth,
            result.EmailOpenRatePct);
    }

    private sealed record MarketingSummaryDto(
        int    ActiveCampaigns,
        int    LeadsGeneratedThisMonth,
        double EmailOpenRatePct);
}
