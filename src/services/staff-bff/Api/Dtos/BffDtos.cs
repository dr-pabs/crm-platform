namespace CrmPlatform.StaffBff.Api.Dtos;

// ── Dashboard aggregation response ───────────────────────────────────────────

public sealed record StaffDashboardResponse(
    SfaSummary         Sfa,
    CssSummary         Css,
    MarketingSummary   Marketing,
    AnalyticsSummary   Analytics,
    DateTimeOffset     GeneratedAt);

// ── Per-domain summaries (populated from internal service responses) ──────────

public sealed record SfaSummary(
    int     OpenLeads,
    int     OpenOpportunities,
    decimal PipelineValue,
    int     ActivitiesThisWeek);

public sealed record CssSummary(
    int Open,
    int BreachedSla,
    int ResolvedThisWeek,
    double AvgResolutionHours);

public sealed record MarketingSummary(
    int  ActiveCampaigns,
    int  LeadsGeneratedThisMonth,
    double EmailOpenRatePct);

public sealed record AnalyticsSummary(
    double ConversionRatePct,
    double RevenueThisMonth,
    int    NewCustomers);

// ── Paginated lead list (forwarded from sfa-service) ─────────────────────────

public sealed record BffLeadsResponse(
    IReadOnlyList<BffLeadItem> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record BffLeadItem(
    Guid   Id,
    string Name,
    string Company,
    string Status,
    int    Score,
    string? AssignedTo,
    DateTimeOffset CreatedAt);
