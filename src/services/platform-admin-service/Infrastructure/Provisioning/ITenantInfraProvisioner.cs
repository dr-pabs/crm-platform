namespace CrmPlatform.PlatformAdminService.Infrastructure.Provisioning;

/// <summary>
/// Provisions the Azure infrastructure required for a new tenant:
/// Entra ID app registration and Service Bus subscriptions.
/// ADR 0009: provisioning steps must be idempotent — safe to retry on failure.
/// </summary>
public interface ITenantInfraProvisioner
{
    /// <summary>
    /// Creates (or verifies existence of) an Entra ID application registration for this tenant.
    /// For SaaS: creates a service principal in the platform's Entra tenant.
    /// For client-hosted: records the client's existing Entra tenant ID.
    /// </summary>
    Task<ProvisioningStepResult> ProvisionEntraApplicationAsync(
        Guid tenantId, string tenantSlug, CancellationToken ct = default);

    /// <summary>
    /// Creates Service Bus subscriptions on all platform topics for this tenant.
    /// Topics: crm.sfa, crm.css, crm.marketing, crm.identity, crm.platform.
    /// Subscription name convention: {tenantSlug}-{service}.
    /// </summary>
    Task<ProvisioningStepResult> CreateServiceBusSubscriptionsAsync(
        Guid tenantId, string tenantSlug, CancellationToken ct = default);
}

public sealed record ProvisioningStepResult(bool Succeeded, string Details);
