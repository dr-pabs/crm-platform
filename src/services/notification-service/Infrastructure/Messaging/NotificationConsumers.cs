using CrmPlatform.NotificationService.Application;
using CrmPlatform.NotificationService.Domain.Enums;
using CrmPlatform.NotificationService.Infrastructure.Data;
using CrmPlatform.ServiceTemplate.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CrmPlatform.NotificationService.Infrastructure.Messaging;

// ──────────────────────────────────────────────────────────────────────────────
// Shared inbound message shapes
// ──────────────────────────────────────────────────────────────────────────────
file sealed record TenantProvisionedMessage(Guid EventId, Guid TenantId, string AdminEmail, string AdminName, string TenantName);
file sealed record UserProvisionedMessage(Guid EventId, Guid TenantId, Guid UserId, string Email, string DisplayName);
file sealed record CaseCreatedMessage(Guid EventId, Guid TenantId, Guid CaseId, string CaseNumber, string ContactEmail, string ContactName);
file sealed record CaseAssignedMessage(Guid EventId, Guid TenantId, Guid CaseId, string CaseNumber, Guid AssigneeUserId, string AssigneeEmail, string AssigneeName);
file sealed record CaseStatusChangedMessage(Guid EventId, Guid TenantId, Guid CaseId, string CaseNumber, string NewStatus, string ContactEmail, string ContactName);
file sealed record SlaBreachedMessage(Guid EventId, Guid TenantId, Guid CaseId, string CaseNumber, string Severity, Guid AssigneeUserId, string AssigneeEmail, string ManagerEmail);
file sealed record LeadAssignedMessage(Guid EventId, Guid TenantId, Guid LeadId, string LeadName, Guid AssigneeUserId, string AssigneeEmail);
file sealed record OpportunityWonMessage(Guid EventId, Guid TenantId, Guid OpportunityId, string Name, Guid OwnerUserId, string OwnerEmail);
file sealed record TenantSuspendedMessage(Guid EventId, Guid TenantId);

// ──────────────────────────────────────────────────────────────────────────────
// crm.platform consumers
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Sends a welcome email to the provisioned tenant admin.</summary>
public sealed class TenantProvisionedConsumer(
    Azure.Messaging.ServiceBus.ServiceBusClient sbClient,
    IIdempotencyStore idempotencyStore,
    IOptions<ServiceBusConsumerOptions> opts,
    ILogger<TenantProvisionedConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : BaseServiceBusConsumer<TenantProvisionedMessage>(sbClient, idempotencyStore, opts, logger, scopeFactory)
{
    protected override string TopicName        => "crm.platform";
    protected override string SubscriptionName => "notification-service";

    protected override async Task ProcessAsync(
        TenantProvisionedMessage message,
        IServiceScope scope,
        CancellationToken ct)
    {
        var handler = scope.ServiceProvider.GetRequiredService<SendNotificationHandler>();

        await handler.HandleAsync(new SendNotificationCommand(
            TenantId:         message.TenantId,
            RecipientUserId:  null,
            RecipientAddress: message.AdminEmail,
            Channel:          NotificationChannel.Email,
            Category:         NotificationCategory.TenantWelcome,
            BodyPlain:        $"Welcome to CRM Platform, {message.AdminName}! Your tenant '{message.TenantName}' has been provisioned.",
            Subject:          $"Welcome to CRM Platform — {message.TenantName}"),
            ct);
    }
}

/// <summary>Soft-deletes all in-app notifications for a suspended tenant.</summary>
public sealed class TenantSuspendedConsumer(
    Azure.Messaging.ServiceBus.ServiceBusClient sbClient,
    IIdempotencyStore idempotencyStore,
    IOptions<ServiceBusConsumerOptions> opts,
    ILogger<TenantSuspendedConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : BaseServiceBusConsumer<TenantSuspendedMessage>(sbClient, idempotencyStore, opts, logger, scopeFactory)
{
    protected override string TopicName        => "crm.platform";
    protected override string SubscriptionName => "notification-service-suspended";

    protected override async Task ProcessAsync(
        TenantSuspendedMessage message,
        IServiceScope scope,
        CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        await db.InAppItems
            .Where(n => n.TenantId == message.TenantId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsDeleted,  true)
                .SetProperty(n => n.DeletedAt,  DateTime.UtcNow),
                ct);

        logger.LogInformation(
            "Soft-deleted in-app notifications for suspended tenant {TenantId}",
            message.TenantId);
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// crm.identity consumers
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Sends a welcome/onboarding email to a newly provisioned user.</summary>
public sealed class UserProvisionedConsumer(
    Azure.Messaging.ServiceBus.ServiceBusClient sbClient,
    IIdempotencyStore idempotencyStore,
    IOptions<ServiceBusConsumerOptions> opts,
    ILogger<UserProvisionedConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : BaseServiceBusConsumer<UserProvisionedMessage>(sbClient, idempotencyStore, opts, logger, scopeFactory)
{
    protected override string TopicName        => "crm.identity";
    protected override string SubscriptionName => "notification-service";

    protected override async Task ProcessAsync(
        UserProvisionedMessage message,
        IServiceScope scope,
        CancellationToken ct)
    {
        var handler = scope.ServiceProvider.GetRequiredService<SendNotificationHandler>();

        await handler.HandleAsync(new SendNotificationCommand(
            TenantId:         message.TenantId,
            RecipientUserId:  message.UserId,
            RecipientAddress: message.Email,
            Channel:          NotificationChannel.Email,
            Category:         NotificationCategory.UserWelcome,
            BodyPlain:        $"Hi {message.DisplayName}, your CRM Platform account is ready.",
            Subject:          "Welcome to CRM Platform"),
            ct);
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// crm.css consumers
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Sends a case confirmation email+in-app to the contact.</summary>
public sealed class CaseCreatedConsumer(
    Azure.Messaging.ServiceBus.ServiceBusClient sbClient,
    IIdempotencyStore idempotencyStore,
    IOptions<ServiceBusConsumerOptions> opts,
    ILogger<CaseCreatedConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : BaseServiceBusConsumer<CaseCreatedMessage>(sbClient, idempotencyStore, opts, logger, scopeFactory)
{
    protected override string TopicName        => "crm.css";
    protected override string SubscriptionName => "notification-service";

    protected override async Task ProcessAsync(
        CaseCreatedMessage message,
        IServiceScope scope,
        CancellationToken ct)
    {
        var handler = scope.ServiceProvider.GetRequiredService<SendNotificationHandler>();

        await handler.HandleAsync(new SendNotificationCommand(
            TenantId:         message.TenantId,
            RecipientUserId:  null,
            RecipientAddress: message.ContactEmail,
            Channel:          NotificationChannel.Email,
            Category:         NotificationCategory.CaseCreated,
            BodyPlain:        $"Hi {message.ContactName}, your support case {message.CaseNumber} has been created. We'll be in touch shortly.",
            Subject:          $"Support case {message.CaseNumber} received"),
            ct);
    }
}

/// <summary>Sends an in-app + email notification to the newly assigned agent.</summary>
public sealed class CaseAssignedConsumer(
    Azure.Messaging.ServiceBus.ServiceBusClient sbClient,
    IIdempotencyStore idempotencyStore,
    IOptions<ServiceBusConsumerOptions> opts,
    ILogger<CaseAssignedConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : BaseServiceBusConsumer<CaseAssignedMessage>(sbClient, idempotencyStore, opts, logger, scopeFactory)
{
    protected override string TopicName        => "crm.css";
    protected override string SubscriptionName => "notification-service-assigned";

    protected override async Task ProcessAsync(
        CaseAssignedMessage message,
        IServiceScope scope,
        CancellationToken ct)
    {
        var handler = scope.ServiceProvider.GetRequiredService<SendNotificationHandler>();

        // In-app
        await handler.HandleAsync(new SendNotificationCommand(
            TenantId:         message.TenantId,
            RecipientUserId:  message.AssigneeUserId,
            RecipientAddress: message.AssigneeUserId.ToString(),
            Channel:          NotificationChannel.InApp,
            Category:         NotificationCategory.CaseAssigned,
            BodyPlain:        $"Case {message.CaseNumber} has been assigned to you.",
            Subject:          $"New case assigned: {message.CaseNumber}"),
            ct);

        // Email
        await handler.HandleAsync(new SendNotificationCommand(
            TenantId:         message.TenantId,
            RecipientUserId:  message.AssigneeUserId,
            RecipientAddress: message.AssigneeEmail,
            Channel:          NotificationChannel.Email,
            Category:         NotificationCategory.CaseAssigned,
            BodyPlain:        $"Hi {message.AssigneeName}, case {message.CaseNumber} has been assigned to you.",
            Subject:          $"Case assigned: {message.CaseNumber}"),
            ct);
    }
}

/// <summary>Notifies contact when their case is Resolved or Closed.</summary>
public sealed class CaseStatusChangedConsumer(
    Azure.Messaging.ServiceBus.ServiceBusClient sbClient,
    IIdempotencyStore idempotencyStore,
    IOptions<ServiceBusConsumerOptions> opts,
    ILogger<CaseStatusChangedConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : BaseServiceBusConsumer<CaseStatusChangedMessage>(sbClient, idempotencyStore, opts, logger, scopeFactory)
{
    protected override string TopicName        => "crm.css";
    protected override string SubscriptionName => "notification-service-status";

    protected override async Task ProcessAsync(
        CaseStatusChangedMessage message,
        IServiceScope scope,
        CancellationToken ct)
    {
        // Only notify on terminal transitions
        if (message.NewStatus is not ("Resolved" or "Closed"))
            return;

        var handler = scope.ServiceProvider.GetRequiredService<SendNotificationHandler>();

        await handler.HandleAsync(new SendNotificationCommand(
            TenantId:         message.TenantId,
            RecipientUserId:  null,
            RecipientAddress: message.ContactEmail,
            Channel:          NotificationChannel.Email,
            Category:         NotificationCategory.CaseStatusChanged,
            BodyPlain:        $"Hi {message.ContactName}, your case {message.CaseNumber} has been {message.NewStatus.ToLower()}.",
            Subject:          $"Case {message.CaseNumber} {message.NewStatus}"),
            ct);
    }
}

/// <summary>Alerts assignee + manager on SLA warning (80%) or breach (100%).</summary>
public sealed class SlaBreachedConsumer(
    Azure.Messaging.ServiceBus.ServiceBusClient sbClient,
    IIdempotencyStore idempotencyStore,
    IOptions<ServiceBusConsumerOptions> opts,
    ILogger<SlaBreachedConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : BaseServiceBusConsumer<SlaBreachedMessage>(sbClient, idempotencyStore, opts, logger, scopeFactory)
{
    protected override string TopicName        => "crm.css";
    protected override string SubscriptionName => "notification-service-sla";

    protected override async Task ProcessAsync(
        SlaBreachedMessage message,
        IServiceScope scope,
        CancellationToken ct)
    {
        var handler  = scope.ServiceProvider.GetRequiredService<SendNotificationHandler>();
        var category = message.Severity == "Warning"
            ? NotificationCategory.SlaWarning
            : NotificationCategory.SlaBreached;

        var bodyText = message.Severity == "Warning"
            ? $"SLA warning: Case {message.CaseNumber} is approaching its deadline."
            : $"SLA BREACHED: Case {message.CaseNumber} has exceeded its response deadline.";

        // In-app to assignee
        await handler.HandleAsync(new SendNotificationCommand(
            TenantId:         message.TenantId,
            RecipientUserId:  message.AssigneeUserId,
            RecipientAddress: message.AssigneeUserId.ToString(),
            Channel:          NotificationChannel.InApp,
            Category:         category,
            BodyPlain:        bodyText,
            Subject:          $"SLA {message.Severity}: {message.CaseNumber}"),
            ct);

        // Email to assignee
        await handler.HandleAsync(new SendNotificationCommand(
            TenantId:         message.TenantId,
            RecipientUserId:  message.AssigneeUserId,
            RecipientAddress: message.AssigneeEmail,
            Channel:          NotificationChannel.Email,
            Category:         category,
            BodyPlain:        bodyText,
            Subject:          $"SLA {message.Severity}: {message.CaseNumber}"),
            ct);

        // Email to manager (no UserId — manager may be external address from config)
        if (!string.IsNullOrWhiteSpace(message.ManagerEmail))
        {
            await handler.HandleAsync(new SendNotificationCommand(
                TenantId:         message.TenantId,
                RecipientUserId:  null,
                RecipientAddress: message.ManagerEmail,
                Channel:          NotificationChannel.Email,
                Category:         category,
                BodyPlain:        bodyText,
                Subject:          $"[Manager Alert] SLA {message.Severity}: {message.CaseNumber}"),
                ct);
        }
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// crm.sfa consumers
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>In-app notification to the newly assigned sales rep.</summary>
public sealed class LeadAssignedConsumer(
    Azure.Messaging.ServiceBus.ServiceBusClient sbClient,
    IIdempotencyStore idempotencyStore,
    IOptions<ServiceBusConsumerOptions> opts,
    ILogger<LeadAssignedConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : BaseServiceBusConsumer<LeadAssignedMessage>(sbClient, idempotencyStore, opts, logger, scopeFactory)
{
    protected override string TopicName        => "crm.sfa";
    protected override string SubscriptionName => "notification-service";

    protected override async Task ProcessAsync(
        LeadAssignedMessage message,
        IServiceScope scope,
        CancellationToken ct)
    {
        var handler = scope.ServiceProvider.GetRequiredService<SendNotificationHandler>();

        await handler.HandleAsync(new SendNotificationCommand(
            TenantId:         message.TenantId,
            RecipientUserId:  message.AssigneeUserId,
            RecipientAddress: message.AssigneeUserId.ToString(),
            Channel:          NotificationChannel.InApp,
            Category:         NotificationCategory.LeadAssigned,
            BodyPlain:        $"Lead '{message.LeadName}' has been assigned to you.",
            Subject:          "New lead assigned"),
            ct);
    }
}

/// <summary>In-app + email notification to sales rep + manager on Opportunity Won.</summary>
public sealed class OpportunityWonConsumer(
    Azure.Messaging.ServiceBus.ServiceBusClient sbClient,
    IIdempotencyStore idempotencyStore,
    IOptions<ServiceBusConsumerOptions> opts,
    ILogger<OpportunityWonConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : BaseServiceBusConsumer<OpportunityWonMessage>(sbClient, idempotencyStore, opts, logger, scopeFactory)
{
    protected override string TopicName        => "crm.sfa";
    protected override string SubscriptionName => "notification-service-won";

    protected override async Task ProcessAsync(
        OpportunityWonMessage message,
        IServiceScope scope,
        CancellationToken ct)
    {
        var handler = scope.ServiceProvider.GetRequiredService<SendNotificationHandler>();

        await handler.HandleAsync(new SendNotificationCommand(
            TenantId:         message.TenantId,
            RecipientUserId:  message.OwnerUserId,
            RecipientAddress: message.OwnerUserId.ToString(),
            Channel:          NotificationChannel.InApp,
            Category:         NotificationCategory.OpportunityWon,
            BodyPlain:        $"🎉 Opportunity '{message.Name}' has been marked as Won!",
            Subject:          "Opportunity Won"),
            ct);

        await handler.HandleAsync(new SendNotificationCommand(
            TenantId:         message.TenantId,
            RecipientUserId:  message.OwnerUserId,
            RecipientAddress: message.OwnerEmail,
            Channel:          NotificationChannel.Email,
            Category:         NotificationCategory.OpportunityWon,
            BodyPlain:        $"Congratulations! Opportunity '{message.Name}' has been closed as Won.",
            Subject:          $"Opportunity Won: {message.Name}"),
            ct);
    }
}
