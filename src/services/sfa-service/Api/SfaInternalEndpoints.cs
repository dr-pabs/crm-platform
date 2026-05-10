using CrmPlatform.SfaService.Application.Leads;
using CrmPlatform.SfaService.Domain.Enums;
using CrmPlatform.SfaService.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CrmPlatform.SfaService.Api;

/// <summary>
/// Internal endpoints called only by Durable Functions (lead-score-decay) and staff-bff.
/// Not exposed via API gateway — internal network only.
/// </summary>
public static class SfaInternalEndpoints
{
    public static IEndpointRouteBuilder MapSfaInternalEndpoints(this IEndpointRouteBuilder app)
    {
        var internal_ = app.MapGroup("/internal");

        // ─── GET /internal/bff/summary ────────────────────────────────────────
        // Called by staff-bff to populate the dashboard aggregate.
        internal_.MapGet("/bff/summary", async (SfaDbContext db, CancellationToken ct) =>
        {
            var weekAgo = DateTime.UtcNow.AddDays(-7);

            var openLeads          = await db.Leads.CountAsync(l => l.Status == LeadStatus.New || l.Status == LeadStatus.Contacted, ct);
            var openOpportunities  = await db.Opportunities.CountAsync(o => o.Stage != OpportunityStage.Won && o.Stage != OpportunityStage.Lost, ct);
            var pipelineValue      = await db.Opportunities
                .Where(o => o.Stage != OpportunityStage.Won && o.Stage != OpportunityStage.Lost)
                .SumAsync(o => o.Value, ct);
            var activitiesThisWeek = await db.Activities.CountAsync(a => a.OccurredAt >= weekAgo, ct);

            return Results.Ok(new SfaBffSummaryResponse(openLeads, openOpportunities, pipelineValue, activitiesThisWeek));
        });

        // ─── POST /internal/leads/decay-scores ────────────────────────────────
        // Called daily by the lead-score-decay Durable Function.
        // Delegates to the existing ScoreDecayService logic.
        internal_.MapPost("/leads/decay-scores", async (
            DecayTriggeredRequest req,
            ScoreDecayHandler handler,
            ILogger<SfaInternalEndpoints> logger) =>
        {
            logger.LogInformation(
                "Lead score decay triggered externally at {TriggeredAt:u}", req.TriggeredAt);

            var result = await handler.HandleAsync();

            return result.IsSuccess
                ? Results.Ok(new DecayResultResponse(result.Value))
                : result.ToHttpResult();
        });

        return app;
    }
}

public sealed record DecayTriggeredRequest(DateTime TriggeredAt);
public sealed record DecayResultResponse(int DecayedLeadCount);
public sealed record SfaBffSummaryResponse(
    int OpenLeads,
    int OpenOpportunities,
    decimal PipelineValue,
    int ActivitiesThisWeek);
