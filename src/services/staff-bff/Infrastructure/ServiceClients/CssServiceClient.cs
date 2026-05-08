using System.Net.Http.Json;
using CrmPlatform.StaffBff.Api.Dtos;

namespace CrmPlatform.StaffBff.Infrastructure.ServiceClients;

public sealed class CssServiceClient(HttpClient http)
{
    public async Task<CssSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var result = await http.GetFromJsonAsync<CssSummaryDto>("/internal/bff/summary", ct)
            ?? throw new InvalidOperationException("css-service returned null summary");

        return new CssSummary(
            result.Open,
            result.BreachedSla,
            result.ResolvedThisWeek,
            result.AvgResolutionHours);
    }

    private sealed record CssSummaryDto(
        int    Open,
        int    BreachedSla,
        int    ResolvedThisWeek,
        double AvgResolutionHours);
}
