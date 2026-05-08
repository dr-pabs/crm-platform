using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CrmPlatform.PlatformAdminService.Infrastructure.Provisioning;

/// <summary>
/// Azure-backed provisioner. Uses Managed Identity — no connection strings with credentials.
/// Service Bus Administration client uses the namespace URI + DefaultAzureCredential.
/// Entra ID provisioning via Microsoft Graph is a future extension point (see TODO below).
/// </summary>
public sealed class AzureTenantInfraProvisioner(
    ServiceBusAdministrationClient sbAdmin,
    IConfiguration configuration,
    ILogger<AzureTenantInfraProvisioner> logger)
    : ITenantInfraProvisioner
{
    private static readonly string[] PlatformTopics =
    [
        "crm.sfa", "crm.css", "crm.marketing", "crm.identity", "crm.platform"
    ];

    public async Task<ProvisioningStepResult> ProvisionEntraApplicationAsync(
        Guid tenantId, string tenantSlug, CancellationToken ct = default)
    {
        // TODO(#github-issue): Wire Microsoft Graph SDK to create an Entra ID service principal
        // for this tenant. Requires Graph API permission Application.ReadWrite.OwnedBy on the
        // platform's Managed Identity. Track in: https://github.com/dr-pabs/crm-platform/issues
        //
        // For SaaS: POST https://graph.microsoft.com/v1.0/applications with displayName = tenantSlug
        // Store the returned appId in the Tenant registry (IdentityService.TenantRegistry table).
        logger.LogWarning(
            "ProvisionEntraApplication not yet implemented for tenant {TenantId} ({Slug}). " +
            "Manual Entra setup required until Graph API provisioning is wired.",
            tenantId, tenantSlug);

        return new ProvisioningStepResult(
            Succeeded: true,
            Details: "Entra provisioning deferred — manual setup required (see GitHub issue)");
    }

    public async Task<ProvisioningStepResult> CreateServiceBusSubscriptionsAsync(
        Guid tenantId, string tenantSlug, CancellationToken ct = default)
    {
        var errors = new List<string>();

        foreach (var topic in PlatformTopics)
        {
            var subscriptionName = $"{tenantSlug}-platform-admin";

            try
            {
                var exists = await sbAdmin.SubscriptionExistsAsync(topic, subscriptionName, ct);
                if (exists.Value)
                {
                    logger.LogInformation(
                        "Service Bus subscription {Sub} on topic {Topic} already exists — skipping",
                        subscriptionName, topic);
                    continue;
                }

                var options = new CreateSubscriptionOptions(topic, subscriptionName)
                {
                    MaxDeliveryCount = 10,
                    LockDuration = TimeSpan.FromMinutes(5),
                    DefaultMessageTimeToLive = TimeSpan.FromDays(14),
                    DeadLetteringOnMessageExpiration = true,
                };

                await sbAdmin.CreateSubscriptionAsync(options, ct);

                logger.LogInformation(
                    "Created Service Bus subscription {Sub} on topic {Topic} for tenant {TenantId}",
                    subscriptionName, topic, tenantId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to create Service Bus subscription {Sub} on topic {Topic} for tenant {TenantId}",
                    subscriptionName, topic, tenantId);
                errors.Add($"{topic}/{subscriptionName}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
            return new ProvisioningStepResult(false, $"Failed subscriptions: {string.Join("; ", errors)}");

        return new ProvisioningStepResult(true, $"Created subscriptions on {PlatformTopics.Length} topics");
    }
}
