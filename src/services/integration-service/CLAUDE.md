# integration-service — CLAUDE.md

All rules in `/CLAUDE.md` and `/src/services/_template/CLAUDE.md` apply.

---

## Domain

The integration-service is the **sole gateway** between the CRM platform and all external systems. It owns:

- **Connector configuration** — per-tenant OAuth2 credentials (stored as Key Vault secret references, never raw tokens)
- **Outbound job queue** — async background worker dispatches CRM events to external systems with time-bounded retry
- **Inbound webhook reception** — validates, logs, and translates external payloads into `crm.integrations` Service Bus events
- **Data platform export** — two adapters: Event Hub (real-time streaming) and Blob Storage (scheduled batch export)

No other service may call an external system directly. No other service may write to the `integrations.*` schema.

---

## Connector Types

```csharp
public enum ConnectorType
{
    Salesforce      = 1,   // OAuth2 — inbound + outbound (outbound only at v1, inbound optional)
    HubSpot         = 2,   // OAuth2 — inbound (webhooks) + outbound
    GenericWebhook  = 3,   // HMAC-signed inbound webhook, no outbound
    AzureEventHub   = 4,   // Outbound only — real-time event streaming to data platform
    AzureBlobExport = 5,   // Outbound only — scheduled daily/hourly JSON extract to Blob Storage
}

public enum ConnectorStatus { Disconnected = 0, Connected = 1, Error = 2, Suspended = 3 }

public enum OutboundJobStatus { Queued = 0, InProgress = 1, Succeeded = 2, Failed = 3, Abandoned = 4 }

public enum InboundEventStatus { Received = 0, Published = 1, Failed = 2, Skipped = 3 }
```

---

## Key Entities

### `ConnectorConfig` (schema: `integrations.ConnectorConfigs`)
```
Id                    Guid        PK
TenantId              Guid        FK — global query filter
ConnectorType         enum
Status                enum        default Disconnected
KeyVaultSecretName    string?     e.g. "integration-{tenantId}-salesforce" — null until connected
OAuthScopes           string?     space-separated scopes granted
TokenExpiresUtc       DateTime?   access token expiry metadata (not the token itself)
WebhookSecret         string?     HMAC secret for inbound validation (Salesforce/HubSpot/Generic)
                                  NOTE: this IS stored in DB (low-sensitivity — used only for HMAC verify)
ExternalAccountId     string?     e.g. Salesforce OrgId, HubSpot PortalId
RetryPolicy           owned       see RetryPolicy value object below
IsDeleted / DeletedAt             soft delete
CreatedAt / UpdatedAt
```

#### `RetryPolicy` (owned value object on ConnectorConfig)
```
MaxRetryDurationMinutes   int     default 60  — give up after this wall-clock window
InitialRetryDelaySeconds  int     default 30
MaxRetryDelaySeconds      int     default 300 (5 min cap)
BackoffMultiplier         double  default 2.0
```

### `OutboundJob` (schema: `integrations.OutboundJobs`)
```
Id                    Guid        PK
TenantId              Guid        global query filter
ConnectorConfigId     Guid        FK → ConnectorConfigs
ConnectorType         enum        denormalised for query efficiency
EventType             string      e.g. "lead.assigned", "opportunity.won"
Payload               string      JSON — the CRM event payload
Status                enum        Queued → InProgress → Succeeded | Failed | Abandoned
AttemptCount          int         default 0
FirstAttemptAt        DateTime?
LastAttemptAt         DateTime?
NextRetryAt           DateTime?   null when terminal
AbandonedAt           DateTime?
FailureReason         string?
ExternalId            string?     ID returned by external system on success
IsDeleted / DeletedAt
CreatedAt / UpdatedAt
```

### `InboundEvent` (schema: `integrations.InboundEvents`)
```
Id                    Guid        PK
TenantId              Guid        global query filter
ConnectorType         enum
ExternalEventId       string?     dedupe key from external system
RawPayload            string      JSON — original body as received
NormalisedEventType   string?     e.g. "hubspot.contact.updated"
Status                enum        Received → Published | Failed | Skipped
ServiceBusMessageId   string?     set when published to crm.integrations
FailureReason         string?
ReceivedAt            DateTime    UTC
ProcessedAt           DateTime?
IsDeleted / DeletedAt
```

### `IdempotencyRecord` — standard pattern (schema: `integrations.IdempotencyRecords`)

---

## Factory Methods & Domain Rules

### `ConnectorConfig`
- `ConnectorConfig.Create(tenantId, connectorType, retryPolicy)` → Status = Disconnected
- `Connect(keyVaultSecretName, externalAccountId, scopes, tokenExpiry)` → Status = Connected
- `Disconnect()` → Status = Disconnected, clears KV name, ExternalAccountId, TokenExpiresUtc
- `MarkError(reason)` → Status = Error
- `Suspend()` → Status = Suspended (platform admin only — e.g. tenant overdue on billing)
- `UpdateRetryPolicy(policy)` → replaces RetryPolicy value object
- **Business rule**: only one active (non-Disconnected, non-Deleted) `ConnectorConfig` per `{TenantId, ConnectorType}`. Enforced by unique filtered index.

### `OutboundJob`
- `OutboundJob.Create(tenantId, connectorConfigId, connectorType, eventType, payload)` → Status = Queued
- `MarkInProgress()` → Status = InProgress, increments AttemptCount, sets FirstAttemptAt (if null), LastAttemptAt
- `MarkSucceeded(externalId)` → Status = Succeeded
- `MarkFailed(reason, nextRetryAt)` → Status = Failed, sets NextRetryAt (if within retry window)
- `Abandon(reason)` → Status = Abandoned, sets AbandonedAt (retry window exceeded)
- **Business rule**: `nextRetryAt` must be calculated by the worker using the `ConnectorConfig.RetryPolicy` — the entity does not calculate it.
- **Business rule**: `Abandon()` must be called (not `MarkFailed`) when `LastAttemptAt - FirstAttemptAt > RetryPolicy.MaxRetryDurationMinutes`. Worker enforces this.

### `InboundEvent`
- `InboundEvent.Receive(tenantId, connectorType, externalEventId, rawPayload)` → Status = Received
- `MarkPublished(serviceBusMessageId)` → Status = Published
- `MarkFailed(reason)` → Status = Failed
- `Skip(reason)` → Status = Skipped (e.g. unrecognised event type, duplicate)

---

## API Endpoints

All endpoints require JWT. TenantId resolved from `tid` claim via `ITenantContext`.

### Connector Management (Tenant Admin only — `require role TenantAdmin`)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/integrations/connectors` | List all connector configs for tenant (incl. Disconnected) |
| `GET` | `/integrations/connectors/{id}` | Get single connector config |
| `POST` | `/integrations/connectors` | Create connector config (Status=Disconnected) |
| `PUT` | `/integrations/connectors/{id}/retry-policy` | Update retry policy |
| `DELETE` | `/integrations/connectors/{id}` | Soft-delete (must be Disconnected first) |

### OAuth2 Flow (Tenant Admin only)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/integrations/connectors/{id}/oauth/authorize` | Returns the OAuth2 authorization URL to redirect the user to |
| `GET` | `/integrations/oauth/callback` | OAuth2 callback — exchanges code, stores token in Key Vault, marks Connected |

### Outbound Jobs (read-only — Tenant Admin + Staff)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/integrations/jobs` | List outbound jobs — paginated, filter by status/connectorType |
| `GET` | `/integrations/jobs/{id}` | Get single job detail (payload, attempts, failure reason) |
| `POST` | `/integrations/jobs/{id}/replay` | Re-queue an Abandoned job (Tenant Admin only) |

### Inbound Events (read-only — Tenant Admin + Staff)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/integrations/inbound-events` | List inbound events — paginated, filter by status/connectorType |
| `GET` | `/integrations/inbound-events/{id}` | Get single inbound event with raw payload |

### Inbound Webhooks (unauthenticated — validated by connector signature)

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/webhooks/inbound/{tenantId}/{connectorType}` | Receive inbound webhook from external system |

### Platform Admin (requires `PlatformAdmin` role — uses `IgnoreQueryFilters`)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/integrations/admin/connectors` | List all connectors across all tenants |
| `POST` | `/integrations/admin/connectors/{id}/suspend` | Suspend a connector (e.g. tenant billing lapsed) |
| `POST` | `/integrations/admin/connectors/{id}/reinstate` | Reinstate a suspended connector |

---

## Service Bus

### Topic: `crm.integrations` (NEW — owned by this service)

| Event | Subject | Published when |
|-------|---------|----------------|
| `ExternalEventReceivedEvent` | `external.event.received` | Inbound webhook validated and translated |
| `OutboundJobFailedEvent` | `outbound.job.failed` | Job reaches Abandoned status |
| `ConnectorDisconnectedEvent` | `connector.disconnected` | OAuth token revoked or connector manually disconnected |

### Consumed from other topics

| Topic | Subscription | Event(s) | Action |
|-------|-------------|----------|--------|
| `crm.platform` | `integration-service` | `TenantProvisionedEvent` | No-op — log only (no connectors needed at provision time) |
| `crm.platform` | `integration-service-suspended` | `TenantSuspendedEvent` | Suspend all ConnectorConfigs for tenant |
| `crm.sfa` | `integration-service` | `LeadAssignedEvent`, `OpportunityWonEvent` | Enqueue OutboundJob for connected SFA connectors |
| `crm.css` | `integration-service` | `CaseCreatedEvent`, `CaseResolvedEvent` | Enqueue OutboundJob for connected CSS connectors |

---

## Background Workers

### `OutboundDispatchWorker` (BackgroundService, `PeriodicTimer` every 10 seconds)
1. Query `OutboundJobs` where `Status IN (Queued, Failed)` AND `NextRetryAt <= UtcNow` AND `ConnectorConfig.Status == Connected`, ordered by `CreatedAt ASC`, take 50.
2. For each job: `MarkInProgress()` → save → dispatch to connector adapter → `MarkSucceeded` or `MarkFailed`/`Abandon`.
3. Retry window check: if `(UtcNow - job.FirstAttemptAt) > retryPolicy.MaxRetryDurationMinutes` → `Abandon()` → publish `OutboundJobFailedEvent`.
4. Never throw from the worker loop — catch, log, continue.

### `BlobExportWorker` (BackgroundService, daily at 02:00 UTC via `PeriodicTimer` aligned to next 02:00)
1. For each tenant with an active `AzureBlobExport` connector, write a JSON export of the previous day's CRM events to `wasb://crm-exports/{tenantId}/{yyyy}/{MM}/{dd}/export.json`.
2. Container name and storage account from Key Vault (secret name stored in `ConnectorConfig.KeyVaultSecretName`).
3. Skips tenants where the connector is Suspended or Disconnected.

---

## Connector Adapters (internal interfaces)

```csharp
public interface IConnectorAdapter
{
    ConnectorType ConnectorType { get; }
    Task<AdapterResult> SendAsync(OutboundJob job, ConnectorConfig config, CancellationToken ct);
}

public record AdapterResult(bool Success, string? ExternalId, string? FailureReason);
```

Implementations:
- `SalesforceAdapter` — REST API v58, OAuth2 bearer token fetched from KV
- `HubSpotAdapter` — HubSpot v3 API, OAuth2 bearer token fetched from KV
- `AzureEventHubAdapter` — `Azure.Messaging.EventHubs` producer, connection string from KV
- `AzureBlobExportAdapter` — `Azure.Storage.Blobs`, SAS token or Managed Identity

### Inbound signature validation

```csharp
public interface IWebhookValidator
{
    ConnectorType ConnectorType { get; }
    bool Validate(HttpRequest request, string rawBody, string? secret);
}
```

Implementations:
- `HubSpotWebhookValidator` — SHA256 HMAC of `{clientSecret}{requestBody}` vs `x-hubspot-signature-v3`
- `GenericWebhookValidator` — HMAC-SHA256 of body vs `x-webhook-signature`
- `SalesforceWebhookValidator` — stub (inbound optional at v1)

---

## Inbound Webhook Processing Pipeline

`POST /webhooks/inbound/{tenantId}/{connectorType}`:

1. Resolve `ConnectorConfig` by `{tenantId, connectorType}` — 404 if not found or Disconnected.
2. Read raw body as string (must buffer request body).
3. Validate signature using `IWebhookValidator` — 401 if invalid.
4. `InboundEvent.Receive(...)` → save to DB.
5. Translate payload to `ExternalEventReceivedEvent` with `NormalisedEventType` (e.g. `hubspot.contact.updated`).
6. Publish to `crm.integrations` topic.
7. `MarkPublished(serviceBusMessageId)` → save.
8. Return `202 Accepted` — always. Never return payload details (security).

---

## OAuth2 Flow Detail

`GET /integrations/connectors/{id}/oauth/authorize`:
- Builds the authorization URL for the connector type (Salesforce or HubSpot)
- Returns `{ "authorizationUrl": "https://..." }` — frontend redirects the user

`GET /integrations/oauth/callback?code=...&state=...`:
- `state` param = `{connectorConfigId}` (base64 encoded, validated)
- Exchanges code for access + refresh tokens via connector's token endpoint
- Stores refresh token in Key Vault: `await kvClient.SetSecretAsync("integration-{tenantId}-{connectorType}", refreshToken)`
- Stores KV secret name + token expiry in `ConnectorConfig`
- Calls `config.Connect(...)` → Status = Connected
- Redirects to staff portal deep link: `/settings/integrations/{connectorType}?connected=true`

---

## DB Schema Summary (`integrations.*`)

| Table | Key indexes |
|-------|-------------|
| `ConnectorConfigs` | `UIX_ConnectorConfigs_Tenant_Type_Active` — unique filtered `[IsDeleted]=0, [Status] != 0` (one active config per connector type per tenant) |
| `OutboundJobs` | `IX_OutboundJobs_Status_NextRetryAt` — worker polling query; `IX_OutboundJobs_ConnectorConfigId` |
| `InboundEvents` | `IX_InboundEvents_Tenant_Connector_ReceivedAt`; `UIX_InboundEvents_ExternalEventId` partial (NOT NULL) — dedupe |
| `IdempotencyRecords` | standard |

---

## NuGet Packages (additions to template)

```xml
<PackageReference Include="Azure.Messaging.EventHubs" Version="5.11.5" />
<PackageReference Include="Azure.Storage.Blobs" Version="12.21.2" />
<PackageReference Include="Azure.Security.KeyVault.Secrets" Version="4.6.0" />
```

(Salesforce and HubSpot use plain `HttpClient` — no vendor SDK needed for v1 REST calls.)

---

## Business Rules

1. **No raw credentials in DB** — `ConnectorConfig` stores only the Key Vault secret *name*, never the token value. Exception: `WebhookSecret` (HMAC key) IS stored in DB — it is not a credential, it only validates inbound payloads.
2. **One active connector per type per tenant** — enforced by `UIX_ConnectorConfigs_Tenant_Type_Active`. Attempting to create a second Connected config for the same type → 409 Conflict.
3. **Connector must be Disconnected before deletion** — `DELETE /connectors/{id}` returns 422 if `Status != Disconnected`.
4. **Inbound webhooks always return 202** — never 4xx/5xx to the caller after signature validation passes (prevents enumeration of processing errors).
5. **Suspended tenants** — `TenantSuspendedEvent` → all connectors for that tenant suspended. No outbound jobs dispatched for suspended connectors.
6. **Replay is idempotent** — `POST /jobs/{id}/replay` only allowed on `Abandoned` jobs. Creates a new `OutboundJob` from the same payload rather than mutating the original (preserves audit trail).
7. **PII in payloads** — `OutboundJob.Payload` and `InboundEvent.RawPayload` are NOT logged to Application Insights. They are stored in DB only. Log the job ID and event type only.
8. **OAuth state parameter** — must be validated on callback to prevent CSRF. State = base64(`{connectorConfigId}:{nonce}`) where nonce stored in distributed cache (Azure Cache for Redis) with 10-minute TTL.
9. **BlobExportWorker** — export file must be written atomically (write to temp path, rename). If export fails mid-write, partial file must not remain.
10. **ConnectorType.AzureEventHub / AzureBlobExport** — these are platform-level connectors configured by Platform Admins, not tenant admins. They are not shown in the tenant-facing connector UI.
