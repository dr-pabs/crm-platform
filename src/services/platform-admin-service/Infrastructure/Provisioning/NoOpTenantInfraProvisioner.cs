using Microsoft.Extensions.Logging;

namespace CrmPlatform.PlatformAdminService.Infrastructure.Provisioning;

/// <summary>
/// No-op provisioner used in local development and integration tests.
/// Reports success without making any Azure API calls.
/// Register via: services.AddScoped&lt;ITenantInfraProvisioner, NoOpTenantInfraProvisioner&gt;()
/// when IHostEnvironment.IsDevelopment() is true.
/// </summary>
public sealed class NoOpTenantInfraProvisioner(ILogger<NoOpTenantInfraProvisioner> logger)
    : ITenantInfraProvisioner
{
    public Task<ProvisioningStepResult> ProvisionEntraApplicationAsync(
        Guid tenantId, string tenantSlug, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[NoOp] ProvisionEntraApplication skipped for tenant {TenantId} ({Slug}) in dev mode",
            tenantId, tenantSlug);
        return Task.FromResult(new ProvisioningStepResult(true, "Skipped — no-op provisioner (dev mode)"));
    }

    public Task<ProvisioningStepResult> CreateServiceBusSubscriptionsAsync(
        Guid tenantId, string tenantSlug, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[NoOp] CreateServiceBusSubscriptions skipped for tenant {TenantId} ({Slug}) in dev mode",
            tenantId, tenantSlug);
        return Task.FromResult(new ProvisioningStepResult(true, "Skipped — no-op provisioner (dev mode)"));
    }
}
