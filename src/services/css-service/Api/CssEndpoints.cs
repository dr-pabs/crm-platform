using CrmPlatform.CssService.Api.Dtos;
using CrmPlatform.CssService.Application.Cases;
using CrmPlatform.CssService.Application.Sla;
using CrmPlatform.CssService.Domain.Entities;
using CrmPlatform.CssService.Domain.Enums;
using CrmPlatform.CssService.Infrastructure.Data;
using CrmPlatform.ServiceTemplate.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CrmPlatform.CssService.Api;

public static class CssEndpoints
{
    public static IEndpointRouteBuilder MapCssEndpoints(this IEndpointRouteBuilder app)
    {
        var cases    = app.MapGroup("/cases").RequireAuthorization();
        var policies = app.MapGroup("/sla-policies").RequireAuthorization();

        // ─── Cases ────────────────────────────────────────────────────────────

        cases.MapGet("/", async (
            CssDbContext db,
            ITenantContext ctx,
            string? status   = null,
            string? priority = null,
            Guid?   assignee = null,
            int     page     = 1,
            int     pageSize = 25) =>
        {
            pageSize = Math.Min(pageSize, 100);

            var query = db.Cases.AsQueryable();

            // Customer portal: restrict to own AccountId via companyId claim
            if (ctx.Role == "CustomerPortal" && ctx is ICompanyContext companyCtx)
                query = query.Where(c => c.AccountId == companyCtx.CompanyId);

            if (Enum.TryParse<CaseStatus>(status, ignoreCase: true, out var parsedStatus))
                query = query.Where(c => c.Status == parsedStatus);

            if (Enum.TryParse<CasePriority>(priority, ignoreCase: true, out var parsedPriority))
                query = query.Where(c => c.Priority == parsedPriority);

            if (assignee.HasValue)
                query = query.Where(c => c.AssignedToUserId == assignee);

            query = query.OrderByDescending(c => c.Priority).ThenBy(c => c.CreatedAt);

            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return Results.Ok(new PagedCasesResponse(
                items.Select(ToCaseResponse).ToList(), total, page, pageSize));
        });

        cases.MapGet("/{id:guid}", async (Guid id, CssDbContext db, ITenantContext ctx) =>
        {
            var c = await db.Cases.FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();

            // Company isolation check
            if (ctx.Role == "CustomerPortal" && ctx is ICompanyContext companyCtx
                && c.AccountId != companyCtx.CompanyId)
                return Results.Forbid();

            return Results.Ok(ToCaseResponse(c));
        });

        cases.MapPost("/", async (
            CreateCaseRequest req,
            CreateCaseHandler handler) =>
        {
            var result = await handler.HandleAsync(new CreateCaseCommand(
                req.Subject, req.Description, req.Priority,
                req.Channel, req.ContactId, req.AccountId));

            return result.IsSuccess
                ? Results.Created($"/cases/{result.Value}", new CreatedResponse(result.Value))
                : result.ToHttpResult();
        });

        cases.MapPatch("/{id:guid}/status", async (
            Guid id,
            TransitionStatusRequest req,
            TransitionStatusHandler handler) =>
        {
            var result = await handler.HandleAsync(new TransitionStatusCommand(id, req.NewStatus));
            return result.IsSuccess ? Results.NoContent() : result.ToHttpResult();
        });

        cases.MapPost("/{id:guid}/assign", async (
            Guid id,
            AssignCaseRequest req,
            AssignCaseHandler handler) =>
        {
            var result = await handler.HandleAsync(new AssignCaseCommand(id, req.AssignedToUserId));
            return result.IsSuccess ? Results.NoContent() : result.ToHttpResult();
        });

        cases.MapPost("/{id:guid}/escalate", async (
            Guid id,
            EscalateCaseRequest req,
            EscalateCaseHandler handler) =>
        {
            var result = await handler.HandleAsync(
                new EscalateCaseCommand(id, req.Reason, req.NewAssigneeId));
            return result.IsSuccess ? Results.NoContent() : result.ToHttpResult();
        });

        cases.MapPost("/{id:guid}/comments", async (
            Guid id,
            AddCommentRequest req,
            AddCommentHandler handler) =>
        {
            var result = await handler.HandleAsync(
                new AddCommentCommand(id, req.Body, req.IsInternal, req.AuthorType));
            return result.IsSuccess
                ? Results.Created($"/cases/{id}/comments/{result.Value}", new CreatedResponse(result.Value))
                : result.ToHttpResult();
        });

        cases.MapGet("/{id:guid}/comments", async (
            Guid id,
            CssDbContext db,
            ITenantContext ctx) =>
        {
            var isStaff = ctx.Role is not "CustomerPortal";

            var comments = await db.CaseComments
                .Where(c => c.CaseId == id)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            // Filter internal comments for non-staff callers
            var filtered = isStaff
                ? comments
                : comments.Where(c => !c.IsInternal).ToList();

            return Results.Ok(filtered.Select(ToCommentResponse));
        });

        // ─── SLA Policies ─────────────────────────────────────────────────────

        policies.MapGet("/", async (CssDbContext db) =>
        {
            var items = await db.SlaPolicies
                .OrderBy(p => p.Priority)
                .ToListAsync();
            return Results.Ok(items.Select(ToSlaPolicyResponse));
        });

        policies.MapPost("/", async (
            CreateSlaPolicyRequest req,
            CreateSlaPolicyHandler handler) =>
        {
            var result = await handler.HandleAsync(new CreateSlaPolicyCommand(
                req.Name, req.Priority, req.FirstResponseMinutes,
                req.ResolutionMinutes, req.BusinessHoursOnly));

            return result.IsSuccess
                ? Results.Created($"/sla-policies/{result.Value}", new CreatedResponse(result.Value))
                : result.ToHttpResult();
        });

        return app;
    }

    // ─── Projection helpers ───────────────────────────────────────────────────

    private static CaseResponse ToCaseResponse(Case c) => new(
        c.Id, c.Subject, c.Description,
        c.Status.ToString(), c.Priority.ToString(), c.Channel.ToString(),
        c.ContactId, c.AccountId, c.AssignedToUserId,
        c.SlaDeadline, c.SlaBreached,
        c.CreatedAt, c.UpdatedAt);

    private static CommentResponse ToCommentResponse(CaseComment c) => new(
        c.Id, c.CaseId, c.AuthorId,
        c.AuthorType.ToString(), c.Body, c.IsInternal, c.CreatedAt);

    private static SlaPolicyResponse ToSlaPolicyResponse(SlaPolicy p) => new(
        p.Id, p.Name, p.Priority.ToString(),
        p.FirstResponseMinutes, p.ResolutionMinutes, p.BusinessHoursOnly, p.CreatedAt);
}

/// <summary>
/// Optional second isolation context for customer portal users.
/// Resolved from ITenantContext when the caller role is CustomerPortal.
/// </summary>
public interface ICompanyContext
{
    Guid CompanyId { get; }
}
