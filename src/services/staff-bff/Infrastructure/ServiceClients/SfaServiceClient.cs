using System.Net.Http.Json;
using CrmPlatform.StaffBff.Api.Dtos;

namespace CrmPlatform.StaffBff.Infrastructure.ServiceClients;

public sealed class SfaServiceClient(HttpClient http)
{
    /// <summary>Returns top-level SFA counts for the dashboard summary.</summary>
    public async Task<SfaSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        // sfa-service exposes /internal/bff/summary — see ADR 0005
        var result = await http.GetFromJsonAsync<SfaSummaryDto>("/internal/bff/summary", ct)
            ?? throw new InvalidOperationException("sfa-service returned null summary");

        return new SfaSummary(
            result.OpenLeads,
            result.OpenOpportunities,
            result.PipelineValue,
            result.ActivitiesThisWeek);
    }

    public async Task<BffLeadsResponse> GetLeadsAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var result = await http.GetFromJsonAsync<BffLeadsResponse>(
            $"/leads?page={page}&pageSize={pageSize}", ct)
            ?? throw new InvalidOperationException("sfa-service /leads returned null");

        return result;
    }

    // Internal DTO matching sfa-service BFF summary response shape
    private sealed record SfaSummaryDto(
        int     OpenLeads,
        int     OpenOpportunities,
        decimal PipelineValue,
        int     ActivitiesThisWeek);
}
