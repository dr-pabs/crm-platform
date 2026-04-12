# ai-orchestration-service — CLAUDE.md

All rules in `/CLAUDE.md` and `/src/services/_template/CLAUDE.md` apply.

---

## Overview

The ai-orchestration-service is the single AI brain of the CRM platform. It:

1. Consumes Service Bus events from other services and executes AI tasks asynchronously via an `AiJob` queue
2. Exposes synchronous endpoints for on-demand AI requests (email drafting, on-demand summarisation, SMS composition, Teams notifications/calls)
3. Writes AI results back to owning services via **Service Bus events** — owning services have internal HTTP endpoints to apply those results
4. Manages per-tenant prompt templates with fallback to platform hard-coded defaults
5. Exposes a Copilot Studio plugin manifest for M365 Copilot integration

**AI provider**: Claude models via **Azure AI Inference SDK** (`Azure.AI.Inference`). Model configured per capability in `appsettings.json`. Architecture supports swapping to Azure OpenAI or Foundry agents in Phase 2.

---

## Capabilities & Execution Pattern

| Capability | `CapabilityType` enum | Trigger | Pattern | Write-back event |
|-----------|----------------------|---------|---------|-----------------|
| Lead scoring | `LeadScoring` | `LeadCreated`, `LeadAssigned` (SB) | Async `AiJob` | `LeadScoredEvent` |
| Email draft | `EmailDraft` | On-demand POST | **Sync** | None — stored in `AiResults` only |
| Case summarisation | `CaseSummarisation` | `CaseResolved` (SB) + on-demand POST | Async / Sync | `CaseSummarisedEvent` |
| Sentiment analysis | `SentimentAnalysis` | `CaseCommentAdded` (SB) | Async `AiJob` | `SentimentAnalysedEvent` |
| Next-best-action | `NextBestAction` | `LeadAssigned`, `OpportunityStageChanged` (SB) | Async `AiJob` | `NextBestActionGeneratedEvent` |
| Journey personalisation | `JourneyPersonalisation` | `JourneyEnrollmentCreated` (SB) | Async `AiJob` | `JourneyPersonalisedEvent` |
| AI-composed SMS | `SmsComposition` | On-demand POST | Async `AiJob` | None — stored in `SmsRecords` |
| Teams Adaptive Card | `TeamsNotification` | On-demand POST | **Sync** | None — stored in `AiResults` |
| Teams outbound call | `TeamsCall` | On-demand POST | Sync (initiate) | Transcript stored in `TeamsCallRecords` + `ActivityCreatedEvent` to sfa-service |

---

## Domain Enums

```csharp
// CapabilityType — identifies which AI capability an AiJob or AiResult belongs to
enum CapabilityType
{
    LeadScoring           = 1,
    EmailDraft            = 2,
    CaseSummarisation     = 3,
    SentimentAnalysis     = 4,
    NextBestAction        = 5,
    JourneyPersonalisation = 6,
    SmsComposition        = 7,
    TeamsNotification     = 8,
    TeamsCall             = 9,
}

// AiJobStatus — async job lifecycle
enum AiJobStatus
{
    Queued      = 1,
    InProgress  = 2,
    Succeeded   = 3,
    Failed      = 4,   // retryable — under max attempts
    Abandoned   = 5,   // terminal — max retries exhausted
    Stalled     = 6,   // terminal — TTL exceeded before first attempt
}

// UseCase — prompt template discriminator within a capability
enum UseCase
{
    // EmailDraft use-cases
    LeadAssigned           = 1,
    OpportunityWon         = 2,
    OpportunityStageChange = 3,
    // CaseSummarisation use-cases
    CaseResolved           = 10,
    OnDemand               = 11,
    // NextBestAction use-cases
    LeadAssignedNba        = 20,
    OpportunityStageChanged = 21,
    // SentimentAnalysis — single use-case
    CaseCommentAdded       = 30,
    // LeadScoring — single use-case
    LeadCreatedOrAssigned  = 40,
    // JourneyPersonalisation — single use-case
    EnrollmentCreated      = 50,
    // SmsComposition — single use-case
    SmsOnDemand            = 60,
}
```

---

## Key Entities

### `AiJob` (async job queue)

```csharp
AiJob.Create(tenantId, capabilityType, useCase, entityId, entityType, requestingUserId, inputPayload)
// → Status = Queued, AttemptCount = 0, QueuedAt = UtcNow

// Mutators
job.MarkInProgress()          // Status = InProgress, AttemptCount++, FirstAttemptAt ??= UtcNow
job.MarkSucceeded(resultId)   // Status = Succeeded, AiResultId = resultId (terminal)
job.MarkFailed(reason)        // Status = Failed, FailureReason (retryable if AttemptCount < 3)
job.Abandon(reason)           // Status = Abandoned (terminal — max retries exhausted)
job.MarkStalled()             // Status = Stalled (terminal — TTL exceeded)

// Properties
bool IsRetryable => Status == Failed && AttemptCount < 3
bool IsTerminal  => Status is Succeeded or Abandoned or Stalled
```

**Retry policy**: 3 attempts max, fixed 30-second delay between attempts (`NextRetryAt = UtcNow.AddSeconds(30)`).
**TTL**: Jobs queued for >1 hour without a first attempt → `MarkStalled()`.

### `AiResult` (all AI outputs — append-only audit)

```csharp
AiResult.Create(tenantId, capabilityType, useCase, entityId, entityType, modelId,
                inputTokens, outputTokens, durationMs, rawOutput, structuredOutput)
// structuredOutput: JSON — capability-specific (score, summary text, NBA text, sentiment score, draft text)
```

### `PromptTemplate` (per-tenant per-capability per-use-case)

```csharp
PromptTemplate.Create(tenantId, capabilityType, useCase, systemPrompt, userPromptTemplate, createdByUserId)
// userPromptTemplate: Handlebars-style — variables injected at runtime e.g. {{leadName}}, {{company}}

PromptTemplate.Update(systemPrompt, userPromptTemplate, updatedByUserId)
PromptTemplate.Deactivate()
PromptTemplate.Activate()
```

**Fallback**: If no active `PromptTemplate` exists for `(TenantId, CapabilityType, UseCase)`, fall back to hard-coded platform defaults defined as `const string` in `PromptDefaults` static class.

**Unique constraint**: `UIX_PromptTemplates_Tenant_Capability_UseCase_Active` — one active template per `(TenantId, CapabilityType, UseCase)`.

### `SmsRecord` (AI-composed SMS audit)

```csharp
SmsRecord.Create(tenantId, recipientPhone, recipientEntityId, recipientEntityType,
                 composedMessage, aiResultId)
SmsRecord.MarkSent(acsSmsMessageId)
SmsRecord.MarkFailed(reason)
```

### `TeamsCallRecord` (outbound Teams call log)

```csharp
TeamsCallRecord.Create(tenantId, calledPhone, calledEntityId, calledEntityType, initiatedByUserId)
TeamsCallRecord.MarkConnected(acsCallId)
TeamsCallRecord.MarkEnded(durationSeconds)
TeamsCallRecord.SetTranscript(transcriptText)
TeamsCallRecord.MarkFailed(reason)
```

---

## DB Schema — `ai.*`

6 tables:

| Table | Notes |
|-------|-------|
| `AiJobs` | Async job queue. Index: `IX_AiJobs_Status_NextRetryAt` (worker poll), `IX_AiJobs_QueuedAt` (TTL stall check) |
| `AiResults` | Append-only AI output store. Index: `IX_AiResults_EntityId_Capability` |
| `PromptTemplates` | Per-tenant customisation. UIX: `UIX_PromptTemplates_Tenant_Capability_UseCase` (partial, `IsActive=1 AND IsDeleted=0`) |
| `SmsRecords` | AI-composed SMS audit. Index: `IX_SmsRecords_Tenant_Status` |
| `TeamsCallRecords` | Outbound call log. Index: `IX_TeamsCallRecords_Tenant_EntityId` |
| `IdempotencyRecords` | Standard pattern — `MessageId` PK |

**Tenant isolation**: Global `HasQueryFilter` on `TenantId + !IsDeleted` via `ServiceDbContext` base (standard pattern). Platform admin uses `IgnoreQueryFilters()`.

---

## Service Bus

### Topics consumed

| Topic | Subscription | Events handled |
|-------|-------------|----------------|
| `crm.sfa` | `ai-orchestration-service` | `LeadCreatedEvent`, `LeadAssignedEvent`, `OpportunityStageChangedEvent` |
| `crm.css` | `ai-orchestration-service` | `CaseResolvedEvent`, `CaseCommentAddedEvent` |
| `crm.marketing` | `ai-orchestration-service` | `JourneyEnrollmentCreatedEvent` |
| `crm.platform` | `ai-orchestration-service` | `TenantProvisionedEvent`, `TenantSuspendedEvent` |

### Events published to `crm.ai`

| Event | Subject | Payload |
|-------|---------|---------|
| `LeadScoredEvent` | `ai.lead.scored` | TenantId, LeadId, Score (0-100), Rationale, AiResultId |
| `CaseSummarisedEvent` | `ai.case.summarised` | TenantId, CaseId, Summary, AiResultId |
| `SentimentAnalysedEvent` | `ai.sentiment.analysed` | TenantId, CaseId, CommentId, SentimentScore (-1.0 to 1.0), Label (Positive/Neutral/Negative), AiResultId |
| `NextBestActionGeneratedEvent` | `ai.nba.generated` | TenantId, LeadId (or OpportunityId), EntityType, ActionText, ActionType, AiResultId |
| `JourneyPersonalisedEvent` | `ai.journey.personalised` | TenantId, EnrollmentId, ContactId, RecommendedBranchIndex, Rationale, AiResultId |
| `AiJobFailedEvent` | `ai.job.failed` | TenantId, AiJobId, CapabilityType, EntityId, FailureReason, RequestingUserId |
| `AiJobStalledEvent` | `ai.job.stalled` | TenantId, AiJobId, CapabilityType, EntityId, QueuedAt, RequestingUserId |
| `TeamsCallTranscriptReadyEvent` | `ai.call.transcript` | TenantId, CallRecordId, CalledEntityId, EntityType, TranscriptSummary |

---

## API Endpoints

### Sync on-demand endpoints (staff-facing)

```
POST   /ai/email-draft
       Body: { leadId?, opportunityId?, useCase, additionalContext? }
       Returns: { draftSubject, draftBody, aiResultId }

POST   /ai/case-summarise
       Body: { caseId, useCase: "OnDemand" }
       Returns: { summary, aiResultId }

POST   /ai/sms
       Body: { recipientEntityId, recipientEntityType, recipientPhone, context, useCase }
       Returns: { smsRecordId, composedMessage } [202 Accepted — job queued]

POST   /ai/teams-notification
       Body: { recipientUpn, title, body, facts[], actionUrl? }
       Returns: { delivered: bool }

POST   /ai/teams-call
       Body: { calledEntityId, calledEntityType, calledPhone }
       Returns: { callRecordId, status: "Initiating" }
```

### AiJob management (staff + admin)

```
GET    /ai/jobs?status=&capabilityType=&page=&pageSize=
GET    /ai/jobs/{id}
POST   /ai/jobs/{id}/retry    [re-queue an Abandoned job — resets AttemptCount]
```

### AiResult read (staff)

```
GET    /ai/results?entityId=&capabilityType=&page=&pageSize=
GET    /ai/results/{id}
```

### Prompt template management (TenantAdmin + AiPromptEditor permission)

```
GET    /ai/prompts?capabilityType=&useCase=
GET    /ai/prompts/{id}
POST   /ai/prompts               [Create custom prompt]
PUT    /ai/prompts/{id}          [Update prompt content]
DELETE /ai/prompts/{id}          [Soft-delete — reverts to platform default]
POST   /ai/prompts/{id}/activate
POST   /ai/prompts/{id}/deactivate
```

### SMS records (staff read)

```
GET    /ai/sms-records?status=&page=&pageSize=
GET    /ai/sms-records/{id}
```

### Teams call records (staff read)

```
GET    /ai/teams-calls?page=&pageSize=
GET    /ai/teams-calls/{id}
```

### Copilot Studio plugin manifest (unauthenticated static files)

```
GET    /.well-known/ai-plugin.json     [Plugin descriptor]
GET    /ai/openapi.json                [OpenAPI spec — Copilot-scoped subset]
```

---

## Internal endpoints needed in owning services

These are added to existing services so ai-orchestration-service consumers can apply write-backs via HTTP (testable, explicit contract):

| Service | Endpoint | Purpose |
|---------|----------|---------|
| sfa-service | `PATCH /internal/leads/{id}/ai-score` | Apply `LeadScoredEvent` result |
| sfa-service | `PATCH /internal/leads/{id}/next-best-action` | Apply `NextBestActionGeneratedEvent` |
| sfa-service | `POST /internal/leads/{id}/activities` | Create activity from Teams call transcript |
| css-service | `PATCH /internal/cases/{id}/ai-summary` | Apply `CaseSummarisedEvent` result |
| css-service | `PATCH /internal/cases/{id}/sentiment` | Apply `SentimentAnalysedEvent` result |
| marketing-service | `POST /internal/enrollments/{id}/ai-personalisation` | Apply `JourneyPersonalisedEvent` result |

These endpoints are added to `SfaInternalEndpoints.cs`, `CssInternalEndpoints.cs`, `MarketingInternalEndpoints.cs` in their respective services. They are consumed by new **consumers in each owning service** that subscribe to `crm.ai` topic.

**New consumers in owning services:**
- sfa-service: `LeadScoredConsumer`, `NextBestActionGeneratedConsumer`, `TeamsCallTranscriptConsumer`
- css-service: `CaseSummarisedConsumer`, `SentimentAnalysedConsumer`
- marketing-service: `JourneyPersonalisedConsumer`

---

## Application Handlers

### `AiJobHandler`
- `EnqueueAsync(tenantId, capabilityType, useCase, entityId, entityType, requestingUserId, inputPayload)` → creates `AiJob`, saves, returns jobId
- `RetryJobAsync(jobId, tenantId)` → validates Abandoned status, resets AttemptCount=0, Status=Queued

### `AiJobWorker` (BackgroundService, 5s PeriodicTimer)
Poll loop:
1. Query `AiJobs` where `Status IN (Queued, Failed) AND NextRetryAt <= UtcNow AND !IsStalled` — batch of 10
2. **Stall check first**: any job where `Status=Queued AND QueuedAt < UtcNow - 1hr AND AttemptCount=0` → `MarkStalled()`, publish `AiJobStalledEvent`, send InApp notification to `RequestingUserId`
3. For each dispatchable job: `MarkInProgress()` → resolve handler → call Claude → `MarkSucceeded(resultId)` or `MarkFailed(reason)`
4. If `MarkFailed` and `!IsRetryable` (AttemptCount >= 3) → `Abandon(reason)` → publish `AiJobFailedEvent` → send InApp notification to `RequestingUserId`
5. Set `NextRetryAt = UtcNow.AddSeconds(30)` on `MarkFailed` when still retryable

### `SyncAiHandler`
- `ExecuteEmailDraftAsync(tenantId, request)` → resolve prompt → call Claude → save `AiResult` → return draft
- `ExecuteCaseSummariseAsync(tenantId, request)` → resolve prompt → call Claude → save `AiResult` → enqueue `AiJob` (type: CaseSummarisation, useCase: OnDemand) → publish `CaseSummarisedEvent` immediately

### `PromptResolver`
- `ResolveAsync(tenantId, capabilityType, useCase)` → query `PromptTemplates` for active tenant template → fallback to `PromptDefaults.Get(capabilityType, useCase)`
- Returns `(systemPrompt, userPromptTemplate)` tuple

### `PromptTemplateHandler`
- CRUD operations for `PromptTemplate` — enforces TenantAdmin or `AiPromptEditor` claim

### `SmsHandler`
- `ComposeAndQueueAsync(tenantId, request)` → enqueue `AiJob` (SmsComposition) → return `SmsRecord` (Queued)
- Worker calls Claude to compose message → calls ACS SMS → `SmsRecord.MarkSent/MarkFailed`

### `TeamsHandler`
- `SendAdaptiveCardAsync(tenantId, recipientUpn, card)` → POST to Teams incoming webhook URL (stored per tenant in `appsettings` or Key Vault)
- `InitiateCallAsync(tenantId, request)` → ACS Calling SDK → `TeamsCallRecord.Create()` → returns callRecordId

---

## Infrastructure

### `IAiClient` / `ClaudeAiClient`
```csharp
interface IAiClient
{
    Task<AiResponse> CompleteAsync(string systemPrompt, string userPrompt,
                                   string modelId, CancellationToken ct);
}

record AiResponse(string Content, int InputTokens, int OutputTokens, int DurationMs);
```
- `ClaudeAiClient` wraps `Azure.AI.Inference.ChatCompletionsClient` pointed at Azure AI Services endpoint
- Model configured per capability: `appsettings.json → "AiModels": { "LeadScoring": "claude-3-5-sonnet", "EmailDraft": "claude-3-haiku", ... }`

### `ITeamsWebhookClient` / `TeamsWebhookClient`
- Posts Adaptive Card JSON to Teams incoming webhook URL via `HttpClient`
- Webhook URL per tenant stored in Key Vault: `teams-webhook-{tenantId}`

### `IAcsSmsClient` / `AcsSmsClient`
- Wraps `Azure.Communication.Sms.SmsClient`
- Uses Managed Identity + ACS resource endpoint from `appsettings`

### `IAcsCallingClient` / `AcsCallingClient`
- Wraps `Azure.Communication.Calling` SDK
- Initiates outbound call, stores ACS call ID on `TeamsCallRecord`

---

## Capability Handlers (one per async capability, called by AiJobWorker)

| Handler | Input payload fields | Output |
|---------|---------------------|--------|
| `LeadScoringCapabilityHandler` | leadId, leadName, company, source, assignedUserName, activityCount | score (0-100), rationale |
| `CaseSummarisationCapabilityHandler` | caseId, subject, description, comments[], resolutionNote | summary text |
| `SentimentCapabilityHandler` | caseId, commentId, commentBody, authorType | score (-1.0..1.0), label |
| `NextBestActionCapabilityHandler` | entityId, entityType, stageName, leadScore, company, recentActivity | actionText, actionType |
| `JourneyPersonalisationCapabilityHandler` | enrollmentId, contactId, journeySteps[], contactProfile | recommendedBranchIndex, rationale |
| `SmsCompositionCapabilityHandler` | recipientName, recipientPhone, entityId, entityType, context | composedMessage |

Each handler:
1. Fetches entity data via HTTP from owning service (`HttpClient` with named client)
2. Calls `PromptResolver.ResolveAsync()`
3. Renders user prompt with Handlebars substitution
4. Calls `IAiClient.CompleteAsync()`
5. Parses structured JSON from Claude response
6. Saves `AiResult`
7. Publishes write-back event to `crm.ai`

---

## Copilot Studio Plugin

Static files served at:
- `GET /.well-known/ai-plugin.json` — plugin descriptor (name, description, auth: bearer)
- `GET /ai/openapi.json` — trimmed OpenAPI spec exposing only:
  - `POST /ai/email-draft`
  - `POST /ai/case-summarise`
  - `GET /ai/results?entityId=&capabilityType=`
  - `GET /ai/jobs/{id}`

Both files are embedded resources in the project, served as static middleware. No auth required on these two discovery endpoints.

---

## NuGet Packages

```xml
<PackageReference Include="Azure.AI.Inference"                   Version="1.0.0-beta.6" />
<PackageReference Include="Azure.Communication.Sms"             Version="1.1.0"        />
<PackageReference Include="Azure.Communication.CallingServer"    Version="1.0.0-beta.4" />
<PackageReference Include="Azure.Security.KeyVault.Secrets"      Version="4.6.0"        />
<PackageReference Include="Handlebars.Net"                       Version="2.1.6"        />
```

Plus standard: `EF Core SqlServer`, `Hellang.Middleware.ProblemDetails`, `Swashbuckle.AspNetCore`, `Azure.Messaging.ServiceBus`, `Azure.Identity`.

---

## Business Rules

1. **Tenant isolation**: All DB queries scoped by `TenantId` global query filter. `IgnoreQueryFilters()` only in platform-admin operations.
2. **Prompt fallback chain**: Tenant custom (active, not deleted) → hard-coded platform default. If no platform default exists for a capability/use-case combination, throw `InvalidOperationException` — do NOT call Claude with an empty prompt.
3. **Prompt edit access**: JWT must have `TenantAdmin` role OR `AiPromptEditor` custom claim. Staff without this claim get 403.
4. **AiJob retry**: max 3 attempts. Fixed 30-second delay. After 3 failures → `Abandon()` → `AiJobFailedEvent` published → InApp notification sent to `RequestingUserId`.
5. **AiJob TTL**: `AiJobWorker` checks on every poll for jobs where `Status=Queued AND AttemptCount=0 AND QueuedAt < UtcNow - 1hr` → `MarkStalled()` → `AiJobStalledEvent` published → InApp notification to `RequestingUserId`.
6. **Idempotency**: All Service Bus consumers check `IdempotencyRecords` before processing. Duplicate message → `202 Accepted` with no side effects.
7. **Claude response parsing**: All Claude responses must return structured JSON matching the capability's output schema. If parsing fails → treat as `MarkFailed("InvalidAiResponse")`.
8. **Soft delete**: `AiResults` are **never soft-deleted** — immutable audit record. All other entities follow standard soft-delete pattern.
9. **SMS ACS**: ai-orchestration-service owns its own ACS SMS client. It does NOT call notification-service for SMS sending.
10. **Teams webhook URL**: stored per-tenant in Key Vault secret `teams-webhook-{tenantId}`. Missing secret → Teams notification silently skipped + warning logged (non-fatal).
11. **Write-back via events**: After an async capability completes, publish the result event to `crm.ai`. The owning service consumer calls its own internal endpoint to apply the result. The ai-service also always writes to `AiResults`.
12. **Copilot plugin endpoints** (`/.well-known/ai-plugin.json`, `/ai/openapi.json`) are unauthenticated static file responses.
13. **On-demand case summarisation**: creates AND publishes `CaseSummarisedEvent` immediately (no AiJob queue for on-demand path) — the sync endpoint handles the full cycle.

---

## File Structure

```
ai-orchestration-service/
├── CrmPlatform.AiOrchestrationService.csproj
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── wwwroot/
│   └── .well-known/
│       └── ai-plugin.json
├── Domain/
│   ├── Enums/AiEnums.cs
│   ├── Entities/AiJob.cs
│   ├── Entities/AiResult.cs
│   ├── Entities/PromptTemplate.cs
│   ├── Entities/SmsRecord.cs
│   ├── Entities/TeamsCallRecord.cs
│   └── Events/AiEvents.cs
├── Infrastructure/
│   ├── Data/AiDbContext.cs
│   ├── Data/AiIdempotencyStore.cs
│   ├── AiClient/IAiClient.cs + ClaudeAiClient.cs
│   ├── AiClient/PromptDefaults.cs
│   ├── Teams/TeamsWebhookClient.cs
│   ├── Sms/AcsSmsClient.cs
│   ├── Calling/AcsCallingClient.cs
│   ├── Workers/AiJobWorker.cs
│   └── Messaging/AiConsumers.cs
├── Application/
│   ├── CapabilityHandlers/LeadScoringCapabilityHandler.cs
│   ├── CapabilityHandlers/CaseSummarisationCapabilityHandler.cs
│   ├── CapabilityHandlers/SentimentCapabilityHandler.cs
│   ├── CapabilityHandlers/NextBestActionCapabilityHandler.cs
│   ├── CapabilityHandlers/JourneyPersonalisationCapabilityHandler.cs
│   ├── CapabilityHandlers/SmsCompositionCapabilityHandler.cs
│   ├── AiJobHandler.cs
│   ├── SyncAiHandler.cs
│   ├── PromptResolver.cs
│   ├── PromptTemplateHandler.cs
│   ├── SmsHandler.cs
│   └── TeamsHandler.cs
├── Api/
│   ├── Dtos/AiDtos.cs
│   └── AiEndpoints.cs
└── Tests/
    ├── CrmPlatform.AiOrchestrationService.Tests.csproj
    ├── Domain/AiDomainTests.cs
    ├── Application/AiHandlerTests.cs
    └── TenantIsolation/AiTenantIsolationTests.cs
```

---

## New files needed in existing services

| Service | File | New endpoints |
|---------|------|--------------|
| sfa-service | `SfaInternalEndpoints.cs` (extend) | `PATCH /internal/leads/{id}/ai-score`, `PATCH /internal/leads/{id}/next-best-action`, `POST /internal/leads/{id}/activities` |
| css-service | `CssInternalEndpoints.cs` (extend) | `PATCH /internal/cases/{id}/ai-summary`, `PATCH /internal/cases/{id}/sentiment` |
| marketing-service | `MarketingInternalEndpoints.cs` (extend) | `POST /internal/enrollments/{id}/ai-personalisation` |
| sfa-service | `Infrastructure/Messaging/AiResultConsumers.cs` | Consumers for `LeadScoredEvent`, `NextBestActionGeneratedEvent`, `TeamsCallTranscriptReadyEvent` |
| css-service | `Infrastructure/Messaging/AiResultConsumers.cs` | Consumers for `CaseSummarisedEvent`, `SentimentAnalysedEvent` |
| marketing-service | `Infrastructure/Messaging/AiResultConsumers.cs` | Consumer for `JourneyPersonalisedEvent` |
