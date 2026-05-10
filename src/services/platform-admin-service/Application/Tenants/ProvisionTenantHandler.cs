using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CrmPlatform.PlatformAdminService.Domain.Entities;
using CrmPlatform.PlatformAdminService.Domain.Enums;
using CrmPlatform.PlatformAdminService.Infrastructure.Data;
using CrmPlatform.PlatformAdminService.Infrastructure.Provisioning;
using CrmPlatform.ServiceTemplate.Domain;
using CrmPlatform.ServiceTemplate.Infrastructure.Messaging;

namespace CrmPlatform.PlatformAdminService.Application.Tenants;

public sealed record ProvisionTenantCommand(
    string Name,
    string Slug,
    string PlanId,
    string RequestedBy);

public sealed record ProvisionTenantResult(Guid TenantId, string Slug);

/// <summary>
/// Provisioning saga orchestrator.
/// Each step is idempotent — if the saga is retried after a partial failure,
/// completed steps are detected and skipped.
///
/// ADR 0009: Saga pattern (no Durable Functions in Phase 1 — synchronous with compensation).
/// </summary>
public sealed class ProvisionTenantHandler(
    PlatformDbContext db,
    ServiceBusEventPublisher publisher,
    ITenantInfraProvisioner infraProvisioner,
    ILogger<ProvisionTenantHandler> logger)
{
    private const string StepCreateRecord          = "CreateTenantDatabaseRecord";
    private const string StepProvisionEntra        = "ProvisionEntraIdApplication";
    private const string StepCreateServiceBusSubs  = "CreateServiceBusSubscriptions";
    private const string StepSeedDefaultData       = "SeedTenantDefaultData";
    private const string StepActivate              = "SetTenantStatusActive";

    public async Task<Result<ProvisionTenantResult>> HandleAsync(
        ProvisionTenantCommand command,
        CancellationToken ct = default)
    {
        // Validate slug uniqueness (platform-wide — no tenant filter)
        var slugExists = await db.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Slug == command.Slug.ToLowerInvariant(), ct);

        if (slugExists)
            return Result.Fail<ProvisionTenantResult>(
                $"Tenant slug '{command.Slug}' is already taken.", ResultErrorCode.Conflict);

        // Step 1: Create database record
        var tenant = Tenant.Create(command.Name, command.Slug, command.PlanId, command.RequestedBy);

        db.Tenants.Add(tenant);
        db.ProvisioningLogs.Add(TenantProvisioningLog.Write(
            tenant.Id, tenant.Id, StepCreateRecord, ProvisioningStepStatus.Completed));

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Tenant {TenantId} ({Slug}) created — beginning provisioning saga",
            tenant.Id, tenant.Slug);

        // Step 2: Entra ID application registration
        var entraResult = await infraProvisioner.ProvisionEntraApplicationAsync(
            tenant.Id, tenant.Slug, ct);

        db.ProvisioningLogs.Add(TenantProvisioningLog.Write(
            tenant.Id, tenant.Id, StepProvisionEntra,
            entraResult.Succeeded ? ProvisioningStepStatus.Completed : ProvisioningStepStatus.Failed,
            details: entraResult.Details));

        if (!entraResult.Succeeded)
        {
            tenant.Suspend();
            await db.SaveChangesAsync(ct);
            logger.LogError("Provisioning failed at step {Step} for tenant {TenantId}", StepProvisionEntra, tenant.Id);
            return Result.Fail<ProvisionTenantResult>(
                $"Provisioning failed: {entraResult.Details}", ResultErrorCode.InternalError);
        }

        // Step 3: Service Bus subscriptions
        var sbResult = await infraProvisioner.CreateServiceBusSubscriptionsAsync(
            tenant.Id, tenant.Slug, ct);

        db.ProvisioningLogs.Add(TenantProvisioningLog.Write(
            tenant.Id, tenant.Id, StepCreateServiceBusSubs,
            sbResult.Succeeded ? ProvisioningStepStatus.Completed : ProvisioningStepStatus.Failed,
            details: sbResult.Details));

        if (!sbResult.Succeeded)
        {
            tenant.Suspend();
            await db.SaveChangesAsync(ct);
            logger.LogError("Provisioning failed at step {Step} for tenant {TenantId}", StepCreateServiceBusSubs, tenant.Id);
            return Result.Fail<ProvisionTenantResult>(
                $"Provisioning failed: {sbResult.Details}", ResultErrorCode.InternalError);
        }

        // Step 4: Seed default data — triggered by TenantProvisionedEvent downstream.
        // Each service subscribes to crm.platform and seeds its own defaults
        // (default SLA policies in css-service, default roles in identity-service, etc.).
        db.ProvisioningLogs.Add(TenantProvisioningLog.Write(
            tenant.Id, tenant.Id, StepSeedDefaultData, ProvisioningStepStatus.Completed,
            details: "Default data seeded via TenantProvisionedEvent to downstream services"));

        // Step 5: Activate and publish TenantProvisionedEvent
        tenant.Activate();

        db.ProvisioningLogs.Add(TenantProvisioningLog.Write(
            tenant.Id, tenant.Id, StepActivate, ProvisioningStepStatus.Completed));

        await db.SaveChangesAsync(ct);

        foreach (var domainEvent in tenant.DomainEvents)
            await publisher.PublishAsync("crm.platform", domainEvent, ct);

        tenant.ClearDomainEvents();

        logger.LogInformation(
            "Tenant {TenantId} ({Slug}) provisioned successfully",
            tenant.Id, tenant.Slug);

        return Result.Ok(new ProvisionTenantResult(tenant.Id, tenant.Slug));
    }
}
