using CrmPlatform.StaffBff.Application;
using CrmPlatform.StaffBff.Infrastructure.ServiceClients;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrmPlatform.StaffBff.Api;

public static class BffEndpoints
{
    public static IEndpointRouteBuilder MapBffEndpoints(this IEndpointRouteBuilder app)
    {
        var bff = app.MapGroup("/bff").RequireAuthorization();

        // ── GET /bff/dashboard ────────────────────────────────────────────────
        // Composite payload for the staff portal home screen.
        // Fans out to sfa, css, marketing, analytics in parallel.
        bff.MapGet("/dashboard", async (
            DashboardAggregator aggregator,
            CancellationToken ct) =>
        {
            var result = await aggregator.AggregateAsync(ct);
            return Results.Ok(result);
        });

        // ── GET /bff/leads ────────────────────────────────────────────────────
        // Proxied paginated lead list from sfa-service.
        bff.MapGet("/leads", async (
            SfaServiceClient sfaClient,
            int page     = 1,
            int pageSize = 25,
            CancellationToken ct = default) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            var result = await sfaClient.GetLeadsAsync(page, pageSize, ct);
            return Results.Ok(result);
        });

        return app;
    }
}
