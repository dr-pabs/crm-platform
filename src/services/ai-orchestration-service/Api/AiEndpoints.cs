using Microsoft.AspNetCore.Mvc;
using CrmPlatform.AiOrchestrationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using CrmPlatform.AiOrchestrationService.Api.Dtos;
using CrmPlatform.AiOrchestrationService.Application;
using CrmPlatform.AiOrchestrationService.Domain.Enums;
using CrmPlatform.ServiceTemplate.Infrastructure.MultiTenancy;

namespace CrmPlatform.AiOrchestrationService.Api;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Static Copilot plugin files (no auth) ────────────────────────────
        // Served by UseStaticFiles() from wwwroot — no routes needed for .well-known

        // ── Sync AI endpoints ─────────────────────────────────────────────────
        var sync = app.MapGroup("/ai").RequireAuthorization();

        sync.MapPost("/email-draft", async (
            [FromBody] EmailDraftRequest req,
            SyncAiHandler                handler,
            CancellationToken            ct) =>
        {
            var result = await handler.DraftEmailAsync(req, ct);
            return Results.Ok(result);
        })
        .WithName("DraftEmail")
        .WithSummary("Generate an AI email draft")
        .Produces<EmailDraftResponse>();

        sync.MapPost("/teams-notification", async (
            [FromBody] TeamsNotificationRequest req,
            SyncAiHandler                       handler,
            CancellationToken                   ct) =>
        {
            var result = await handler.SendTeamsCardAsync(req, ct);
            return Results.Ok(result);
        })
        .WithName("SendTeamsNotification")
        .WithSummary("Send an AI-composed Teams Adaptive Card")
        .Produces<TeamsNotificationResponse>();

        sync.MapPost("/teams-call", async (
            [FromBody] TeamsCallRequest req,
            SyncAiHandler               handler,
            CancellationToken           ct) =>
        {
            var result = await handler.InitiateCallAsync(req, ct);
            return Results.Ok(result);
        })
        .WithName("InitiateTeamsCall")
        .WithSummary("Initiate an outbound Teams call via ACS")
        .Produces<TeamsCallResponse>();

        // ── Async job dispatch endpoints ──────────────────────────────────────
        sync.MapPost("/sms", async (
            [FromBody] SmsComposeRequest req,
            AsyncAiJobHandler            handler,
            ITenantContext               ctx,
            CancellationToken            ct) =>
        {
            var job = await handler.EnqueueSmsAsync(req, ctx.UserId, ct);
            return Results.Accepted($"/ai/jobs/{job.Id}", job);
        })
        .WithName("EnqueueSmsComposition")
        .WithSummary("Enqueue an AI-composed SMS for async delivery")
        .Produces<AiJobResponse>(StatusCodes.Status202Accepted);

        sync.MapPost("/case-summarise", async (
            [FromBody] CaseSummarisationRequest req,
            AsyncAiJobHandler                   handler,
            ITenantContext                       ctx,
            CancellationToken                   ct) =>
        {
            var job = await handler.EnqueueCaseSummarisationAsync(req, ctx.UserId, ct);
            return Results.Accepted($"/ai/jobs/{job.Id}", job);
        })
        .WithName("EnqueueCaseSummarisation")
        .WithSummary("Enqueue an on-demand AI case summarisation")
        .Produces<AiJobResponse>(StatusCodes.Status202Accepted);

        // ── Job read endpoints ────────────────────────────────────────────────
        sync.MapGet("/jobs", async (
            AiReadHandler   reader,
            AiJobStatus?    status,
            CapabilityType? capability,
            int page     = 1,
            int pageSize = 25,
            CancellationToken ct = default) =>
        {
            pageSize = Math.Min(pageSize, 100);
            var result = await reader.ListJobsAsync(status, capability, page, pageSize, ct);
            return Results.Ok(result);
        })
        .WithName("ListAiJobs")
        .Produces<PagedResult<AiJobResponse>>();

        sync.MapGet("/jobs/{id:guid}", async (
            Guid          id,
            AiReadHandler reader,
            CancellationToken ct) =>
        {
            var job = await reader.GetJobAsync(id, ct);
            return job is null ? Results.NotFound() : Results.Ok(job);
        })
        .WithName("GetAiJob")
        .Produces<AiJobResponse>()
        .Produces(StatusCodes.Status404NotFound);

        sync.MapGet("/results/{id:guid}", async (
            Guid          id,
            AiReadHandler reader,
            CancellationToken ct) =>
        {
            var result = await reader.GetResultAsync(id, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetAiResult")
        .Produces<AiResultResponse>()
        .Produces(StatusCodes.Status404NotFound);

        // ── SMS records ───────────────────────────────────────────────────────
        sync.MapGet("/sms-records", async (
            AiReadHandler reader,
            int page     = 1,
            int pageSize = 25,
            CancellationToken ct = default) =>
        {
            pageSize = Math.Min(pageSize, 100);
            return Results.Ok(await reader.ListSmsRecordsAsync(page, pageSize, ct));
        })
        .WithName("ListSmsRecords")
        .Produces<PagedResult<SmsRecordResponse>>();

        // ── Teams call records ────────────────────────────────────────────────
        sync.MapGet("/teams-calls", async (
            AiReadHandler reader,
            int page     = 1,
            int pageSize = 25,
            CancellationToken ct = default) =>
        {
            pageSize = Math.Min(pageSize, 100);
            return Results.Ok(await reader.ListTeamsCallsAsync(page, pageSize, ct));
        })
        .WithName("ListTeamsCallRecords")
        .Produces<PagedResult<TeamsCallResponse>>();

        // ── Prompt templates ──────────────────────────────────────────────────
        var prompts = app.MapGroup("/ai/prompts")
            .RequireAuthorization("TenantAdminOrAiPromptEditor");

        prompts.MapGet("/", async (
            PromptManagementHandler handler,
            CancellationToken       ct) =>
            Results.Ok(await handler.ListAsync(ct)))
        .WithName("ListPromptTemplates")
        .Produces<List<PromptTemplateResponse>>();

        prompts.MapPost("/", async (
            [FromBody] UpsertPromptTemplateRequest req,
            PromptManagementHandler                handler,
            CancellationToken                      ct) =>
        {
            var result = await handler.UpsertAsync(req, ct);
            return Results.Ok(result);
        })
        .WithName("UpsertPromptTemplate")
        .Produces<PromptTemplateResponse>();

        prompts.MapDelete("/{id:guid}", async (
            Guid                    id,
            PromptManagementHandler handler,
            CancellationToken       ct) =>
        {
            await handler.DeleteAsync(id, ct);
            return Results.NoContent();
        })
        .WithName("DeletePromptTemplate")
        .Produces(StatusCodes.Status204NoContent);

        
        // ── Eval endpoints ──────────────────────────────────────────────────
        sync.MapPost("/eval/feedback/{resultId:guid}", async (
            Guid resultId,
            [FromBody] AiFeedbackRequest req,
            AiDbContext db,
            CancellationToken ct) =>
        {
            var result = await db.AiResults.FirstOrDefaultAsync(r => r.Id == resultId, ct);
            if (result is null) return Results.NotFound();
            result.RecordFeedback(req.Feedback, req.EditedOutput, req.IsAccepted);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        })
        .WithName("SubmitAiFeedback")
        .WithSummary("Submit feedback on an AI result")
        .Produces(StatusCodes.Status204NoContent);

        sync.MapGet("/eval/metrics", async (
            AiDbContext db,
            CapabilityType? capability,
            CancellationToken ct) =>
        {
            var tenantId = db.Entry(new { }).Property<Guid>("TenantId").CurrentValue;
            var q = db.AiResults.AsQueryable();
            if (capability.HasValue) q = q.Where(r => r.CapabilityType == capability.Value);
            var total = await q.CountAsync(ct);
            var accepted = await q.CountAsync(r => r.IsAccepted, ct);
            return Results.Ok(new { total, accepted, acceptanceRate = total > 0 ? (double)accepted / total : 0 });
        })
        .WithName("GetAiEvalMetrics")
        .WithSummary("Get AI evaluation metrics");

        return app;
    }
}
