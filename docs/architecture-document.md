# CRM Platform — Architecture Document

**Date:** 2026-05-11
**Status:** Active Development

---

## 1. Introduction

The CRM Platform is a multi-tenant SaaS Customer Relationship Management system built on Microsoft Azure. It serves four core business modules — Sales Force Automation (SFA), Customer Service & Support (CS&S), Marketing Automation, and Analytics — delivered through a React/TypeScript staff portal and headless APIs for customer self-service and third-party integrations. AI capabilities powered by Claude models (via Azure AI Inference) are embedded across all functional areas.

The platform supports dual deployment models: a shared multi-tenant SaaS deployment and a client-hosted dedicated Azure subscription model, both managed through Bicep Infrastructure-as-Code.

---

## 2. System Overview

### 2.1 Technology Stack

| Layer | Technology |
|---|---|
| Backend | C# .NET 8, ASP.NET Core Minimal APIs |
| Frontend | TypeScript, React 18, Tailwind CSS, Vite |
| Database | Azure SQL (Hyperscale in prod) |
| Messaging | Azure Service Bus (topics/subscriptions) |
| AI | Claude 3.7 Sonnet via Azure AI Inference SDK |
| Infrastructure | Bicep (Azure PaaS) |
| Orchestration | Azure Durable Functions |
| Identity | Microsoft Entra ID (Azure AD) |
| API Gateway | Azure API Management |
| Observability | OpenTelemetry → Application Insights |
| Container Hosting | Azure Container Apps |

### 2.2 Service Topology

```
                          ┌──────────────┐
                          │  APIM (API   │
                          │  Management) │
                          └──────┬───────┘
                                 │
         ┌───────────────────────┼───────────────────────┐
         │                       │                       │
    ┌────▼─────┐          ┌──────▼──────┐         ┌─────▼──────┐
    │  Staff   │          │  Customer/  │         │ Third-Party│
    │  Portal  │          │  Self-Svc   │         │  Integ.    │
    └────┬─────┘          └─────────────┘         └────────────┘
         │
    ┌────▼─────┐
    │ Staff BFF│  (aggregation + composition)
    └────┬─────┘
         │
    ┌────▼──────────────────────────────────────────────┐
    │              Azure Service Bus                     │
    │  crm.sfa | crm.css | crm.marketing | crm.identity │
    │  crm.platform | crm.ai                            │
    └────┬──────────────────────────────────────────────┘
         │
    ┌────▼──────────────────────────────────────────────┐
    │              Backend Services (10)                  │
    │  sfa-service | css-service | marketing-service     │
    │  analytics-service | identity-service               │
    │  platform-admin-service | notification-service     │
    │  integration-service | ai-orchestration-service     │
    │  staff-bff                                         │
    └───────────────────────────────────────────────────┘
```

All inter-service communication flows through Azure Service Bus topics. No service-to-service HTTP calls exist — events are published and consumed asynchronously.

### 2.3 Deployment Architecture

| Environment | Purpose | SQL SKU | APIM SKU |
|---|---|---|---|
| dev | Developer iteration | GeneralPurpose, 2 vCores | Developer |
| test | CI/CD pipelines, integration tests | GeneralPurpose, 2 vCores | Developer |
| staging | Pre-production validation | Hyperscale, 4 vCores | Premium |
| prod | Live SaaS platform | Hyperscale, 4 vCores | Premium |

Each service runs as an Azure Container App with health probes, auto-scaling, and Managed Identity for all Azure resource access. No connection strings with passwords exist — all secrets are accessed via Key Vault through Managed Identity.

---

## 3. Multi-Tenancy Architecture

Every database entity carries a `TenantId` (Guid). Tenant isolation is enforced at three layers:

1. **EF Core Query Filters** — global `HasQueryFilter` on every `BaseEntity` filters by `TenantId` and `IsDeleted` (soft delete)
2. **SQL Row-Level Security** — `SESSION_CONTEXT` set per-request via a `DbCommandInterceptor`
3. **JWT Claim Validation** — `tid` claim extracted in `TenantContextMiddleware`, rejected if missing

All API endpoints carry `X-Tenant-Id` resolved from the JWT. Tenant isolation tests are mandatory for every endpoint — calling with Tenant B's JWT must return zero Tenant A data.

---

## 4. Functional Modules

### 4.1 Sales Force Automation (SFA)

**Service:** `sfa-service`
**Database schema:** `sfa`

#### Domain Model

| Entity | Description | Key Fields |
|---|---|---|
| Lead | Sales prospect, entry point of pipeline | FirstName, LastName, Email, Company, Source, Status (New→Contacted→Qualified→Converted/Disqualified), Score (0-100) |
| Contact | Individual person record | FirstName, LastName, Email, Phone, AccountId |
| Account | Company/organisation | Name, Industry, EmployeeCount, Phone, AnnualRevenue, Website |
| Opportunity | Sales deal in pipeline | Title, Stage (Prospecting→Qualification→Proposal→Negotiation→ClosedWon/ClosedLost), Amount, CloseDate |
| Quote | Priced proposal linked to opportunity | OpportunityId, LineItemsJson, TotalValue, Status (Draft→Sent→Accepted/Rejected) |
| Activity | Polymorphic timeline record | ActivityType (Call/Email/Meeting/Note), RelatedEntityId, Notes |

#### State Machines

- **Lead**: New → Contacted → Qualified → Converted / Disqualified
- **Opportunity**: Prospecting → Qualification → Proposal → Negotiation → ClosedWon / ClosedLost (strict sequential progression enforced)
- **Quote**: Draft → Sent → Accepted / Rejected (requires opportunity in Negotiation stage or later)

#### Events Published (crm.sfa topic)

`lead.created`, `lead.assigned`, `lead.converted`, `opportunity.stage.changed`, `opportunity.won`, `opportunity.lost`, `quote.sent`

#### UI Coverage

- Leads list/detail (create, update, delete, AI scoring)
- Contacts list/detail (create, update)
- Accounts list/detail (create, update)
- Opportunities kanban board + list/detail (stage advancement)
- Quotes list/detail (create, send, accept, reject)
- Activity timeline on all detail pages

---

### 4.2 Customer Service & Support (CS&S)

**Service:** `css-service`
**Database schema:** `css`

#### Domain Model

| Entity | Description | Key Fields |
|---|---|---|
| Case | Support ticket/service request | Subject, Description, Status (New→Open→Pending→Escalated→Resolved→Closed), Priority (Low/Medium/High/Critical), Channel (Email/Phone/Portal/Chat/Api) |
| CaseComment | Threaded comment on a case | Body, IsInternal, AuthorId |
| EscalationRecord | Audit record of case escalation | Reason, EscalatedBy, NewAssigneeId |
| SlaPolicy | SLA definition per priority | Priority, ResponseHours, ResolutionHours |

#### State Machine

New → Open (SLA clock starts) → Pending (waiting customer) → Open (customer replies) → Escalated → Resolved → Closed (immutable). SLA breach detection via Durable Function orchestrator.

#### Events Published (crm.css topic)

`case.created`, `case.assigned`, `case.resolved`, `case.closed`, `case.escalated`, `case.comment.added`

#### UI Coverage

- Cases list/detail with AI summarisation and sentiment analysis
- Customer portal: MyCases, AllCases (superusers), NewCase, CaseDetail
- CaseTimeline with threaded comments and internal notes

---

### 4.3 Marketing Automation

**Service:** `marketing-service`
**Database schema:** `marketing`

#### Domain Model

| Entity | Description | Key Fields |
|---|---|---|
| Campaign | Marketing initiative | Name, Channel (Email/SMS/InApp/Push), Status (Draft→Scheduled→Active→Paused→Completed/Cancelled), Impressions, Clicks, Conversions |
| Journey | Multi-step automation flow | Name, StepsJson, Status (Draft→Active→Paused→Completed→Archived), CampaignId |
| JourneyEnrollment | Contact enrollment in a journey | ContactId, CurrentStepIndex, Status (Active→Completed→Exited→Failed) |
| EmailTemplate | Reusable email content | Name, Subject, Body (Handlebars), Engine (Handlebars/Razor) |

#### Events Published (crm.marketing topic)

`campaign.activated`, `journey.published`, `journey.completed`, `enrollment.created`

#### UI Coverage

- Campaigns list with create form, metrics display
- Journeys list with status management

---

### 4.4 Analytics Service

**Service:** `analytics-service`
**Database schema:** `analytics`

#### Domain Model

| Entity | Description |
|---|---|
| MetricSnapshot | Periodic aggregated metric value (by tenant, metric name, period) |
| AnalyticsEvent | Raw event for metric computation (immutable) |

The analytics service provides dashboard metrics for the staff portal. Data is aggregated from events published by other services and stored as periodic snapshots for efficient querying.

#### UI Coverage

- Analytics dashboard with live metric cards (via `useAnalyticsDashboard` hook)

---

### 4.5 AI Orchestration

**Service:** `ai-orchestration-service`
**Database schema:** `ai`

#### Domain Model

| Entity | Description |
|---|---|
| AiJob | Async AI job queue with retry (max 3 attempts), 1-hour TTL |
| AiResult | Immutable result record with token counts, feedback fields |
| PromptTemplate | Per-tenant prompt template with platform defaults fallback |
| SmsRecord | Outbound SMS audit log (ACS delivery) |
| TeamsCallRecord | Outbound Teams call audit log (ACS calling) |

#### Capabilities (12)

| # | Capability | Trigger | Pattern |
|---|---|---|---|
| 1 | Lead Scoring | SB lead.created/assigned | Async AiJob |
| 2 | Email Draft | POST /ai/email-draft | Sync |
| 3 | Case Summarisation | SB case.resolved + POST | Async + Sync |
| 4 | Sentiment Analysis | SB case.comment.added | Async AiJob |
| 5 | Next Best Action | SB lead.assigned/opp.stage.changed | Async AiJob |
| 6 | Journey Personalisation | SB journey.enrollment.created | Async AiJob |
| 7 | SMS Composition | POST /ai/sms | Async AiJob → ACS |
| 8 | Teams Notification | POST /ai/teams-notification | Sync → ACS |
| 9 | Teams Outbound Call | POST /ai/teams-call | Sync → ACS |
| 10 | Knowledge Query (RAG) | On-demand | Async AiJob |
| 11 | Pipeline Forecasting | Scheduled/on-demand | Async AiJob |
| 12 | Churn Prediction | Scheduled/tenant health | Async AiJob |

#### AI Provider Integration

- **Model**: Claude 3.7 Sonnet via Azure AI Inference SDK
- **Auth**: Managed Identity (DefaultAzureCredential) — no API keys stored
- **Prompt Resolution**: Tenant custom (DB) → Platform default (hardcoded)
- **Templating**: Handlebars with HTML escaping (XSS protection)
- **Output**: Structured JSON parsed per capability

#### Async Job Lifecycle

```
Queued → InProgress → Succeeded / Failed → (retry) → Abandoned
                       ↓                     ↓
                  AiResult stored        Stale TTL (1hr)
                       ↓
              Publish result event to crm.ai
                       ↓
            Owning service applies result
```

#### Evaluation Framework

- AiResult carries `Feedback`, `EditedOutput`, `IsAccepted` fields
- `POST /ai/eval/feedback` — submit user feedback on AI output
- `GET /ai/eval/metrics` — acceptance rates by capability

#### M365 Copilot Integration

- Plugin manifest at `.well-known/ai-plugin.json`
- OpenAPI spec at `/ai/openapi.json`
- 9 Copilot-exposed functions
- SSE streaming for email draft generation

#### AI Consumers (Service Bus)

| Consumer | Topic | Subscription | Action |
|---|---|---|---|
| LeadCreatedConsumer | crm.sfa | lead.created | Queue LeadScoring job |
| LeadAssignedConsumer | crm.sfa | lead.assigned | Queue LeadScoring + NextBestAction |
| OpportunityStageChangedConsumer | crm.sfa | opp.stage.changed | Queue NextBestAction |
| CaseResolvedConsumer | crm.css | case.resolved | Queue CaseSummarisation |
| CaseCommentAddedConsumer | crm.css | case.comment.added | Queue SentimentAnalysis |
| JourneyEnrollmentCreatedConsumer | crm.marketing | enrollment.created | Queue JourneyPersonalisation |
| TenantProvisionedConsumer | crm.platform | tenant.provisioned | No-op placeholder |
| TenantSuspendedConsumer | crm.platform | tenant.suspended | Abandon queued jobs for tenant |

---

### 4.6 Identity Service

**Service:** `identity-service`

Handles user provisioning, Entra ID synchronisation, and consent management. Publishes to `crm.identity` topic. Events include user provisioning and consent changes. The `TenantContextMiddleware` resolves tenant context from JWT claims and populates `ITenantContext` for all downstream services.

---

### 4.7 Platform Admin Service

**Service:** `platform-admin-service`

Manages tenant lifecycle: provisioning (Entra tenant creation, Service Bus namespace, SQL schema), suspension, deprovisioning. Implements the tenant provisioning saga pattern (ADR 0010) using Azure Service Bus for orchestration.

---

### 4.8 Notification Service

**Service:** `notification-service`

Multi-channel notification delivery: InApp, Email, SMS, Teams. Consumes events from other services and delivers notifications through the appropriate channel. InApp notifications are displayed in the staff portal. The AI orchestration service handles its own SMS and Teams delivery via Azure Communication Services.

---

### 4.9 Integration Service

**Service:** `integration-service`

Manages third-party connector OAuth flows for Salesforce and HubSpot. Handles OAuth token exchange, refresh, and callback. Connector configuration is stored per-tenant.

---

### 4.10 Staff BFF

**Service:** `staff-bff`

Aggregation and composition layer for the staff portal. Calls downstream services (SFA, CSS, Marketing, Analytics) via HTTP with bearer token forwarding and resilience pipelines (`Microsoft.Extensions.Http.Resilience`). Serves the dashboard aggregation endpoint, combining data from multiple services into a single staff-oriented response. No database — pure HTTP aggregation.

---

## 5. Frontend Architecture

### 5.1 Staff Portal

**Location:** `src/frontend/staff-portal`

Single-page React 18 application with TypeScript, Tailwind CSS, and React Router. Authentication via MSAL.js with Microsoft Entra ID. API calls proxied through APIM in production, Vite dev proxy in local development.

**Design System:** SaaS CRM palette — trust blue (#2563EB) primary, orange (#EA580C) accent, Poppins headings + Open Sans body (Modern Professional pairing from UI UX Pro Max skill). Accessible: skip-to-content link, aria-labels, visible focus states, responsive breakpoints.

**Pages (17):**

| Page | Route | Purpose |
|---|---|---|
| Dashboard | `/` | Stat cards, recent leads/cases |
| Leads | `/leads` | Lead list with create modal |
| LeadDetail | `/leads/:id` | Edit form, AI draft composer, activity timeline |
| Contacts | `/contacts` | Contact list with create modal |
| ContactDetail | `/contacts/:id` | Edit form, activity timeline |
| Accounts | `/accounts` | Account list with create modal |
| AccountDetail | `/accounts/:id` | Edit form, activity timeline |
| Opportunities | `/opportunities` | Kanban board + list view |
| OpportunityDetail | `/opportunities/:id` | Stage advancement, activity timeline |
| Quotes | `/quotes` | Quote list with create modal |
| QuoteDetail | `/quotes/:id` | Send/accept/reject actions |
| Cases | `/cases` | Case list with create modal |
| CaseDetail | `/cases/:id` | Timeline, AI summary, sentiment |
| Campaigns | `/campaigns` | Campaign list with create form |
| Journeys | `/journeys` | Journey list |
| Analytics | `/analytics` | Metric dashboard with live API data |
| Notifications | `/notifications` | InApp notification list |

### 5.2 Customer Portal

**Location:** `src/frontend/customer-portal`

Lightweight React application for customer self-service case management. Pages: MyCases, AllCases (superusers), NewCase, CaseDetail. Uses `WaitingOnCustomer` status label instead of `Pending` for customer-friendly language.

---

## 6. Infrastructure (Bicep IaC)

### 6.1 Reusable Modules (14)

`infra/modules/`: apiManagement, appConfiguration, aiSearch, containerApp, containerAppsEnvironment, containerRegistry, cosmosDb, keyVault, logAnalytics, redis, serviceBus, sqlDatabase, staticWebApp, storageAccount

### 6.2 Deployment Orchestration

`infra/platform/main.bicep` deploys the full SaaS platform:

1. Resource Group
2. Key Vault (secrets via Managed Identity)
3. Storage Account (attachments, durable functions)
4. Service Bus Namespace (topics for all modules)
5. SQL Database (Hyperscale in prod, with analytics replica)
6. App Configuration (environment-specific key-values)
7. API Management (Premium in prod, single external entry point)
8. Static Web Apps (staff portal + customer portal)
9. Container Apps for all 10 backend services
10. Azure Container Registry (image storage)

### 6.3 Client-Hosted Deployment

`infra/client-hosted/main.bicep` — dedicated Azure subscription template for enterprise clients requiring isolated infrastructure.

---

## 7. CI/CD Pipeline

### Build (ci-backend.yml)

Matrix build across all 10 backend services. Each service: restore → build → test with coverage. Coverage gate at 80% minimum enforced. Security scans: CodeQL SAST, OWASP Dependency Check, TruffleHog secret scanning.

### Deploy (cd-dev.yml / cd-prod.yml)

1. Docker build + push to ACR (all 10 services + staff-bff)
2. Microsoft Defender for Containers vulnerability scan
3. Bicep infrastructure deployment
4. EF Core migrations applied
5. Smoke tests — health endpoint verification through APIM

### IaC Validation (ci-iac.yml)

Bicep lint and preflight validation. Drift detection via scheduled workflow.

---

## 8. Cross-Cutting Concerns

### 8.1 Authentication & Authorisation

- Layer 1: JWT Bearer validation (Entra ID in prod, HMAC stub in local dev)
- Layer 2: Tenant context middleware — extracts `tid`, `sub`, `roles` from claims
- APIM validates JWT at the gateway before routing to backend services
- Prompt template management requires `TenantAdmin` or `AiPromptEditor` role

### 8.2 Observability

- Structured logging via `ILogger<T>` — all entries include `TenantId` and `CorrelationId`
- OpenTelemetry: ASP.NET Core + HttpClient instrumentation, OTLP export
- Health endpoints: `/health/live` (liveness), `/health/ready` (readiness — checks DB + Service Bus), `/health/start` (startup probe)

### 8.3 Resilience

- Service Bus consumers are idempotent (check `MessageId` before processing)
- EF Core `SaveChangesAsync` called once per unit of work
- All service-to-service communication via Service Bus — no HTTP coupling
- `Result<T>` pattern — never throw exceptions for expected business failures
- ProblemDetails (RFC 7807) for all error responses
- Staff BFF uses `AddStandardResilienceHandler()` for downstream HTTP calls

### 8.4 Data Integrity

- Soft deletes only — `IsDeleted` + `DeletedAt` on every `BaseEntity`
- `DateTime.UtcNow` / `TimeProvider.System` — no `DateTime.Now`
- No PII in logs (emails, names, phones, IP addresses)
- Tenant isolation tested for every endpoint (mandatory in CI)

### 8.5 Local Development

- Docker Compose: SQL Server 2022, Service Bus Emulator, Azurite (blob storage), Mailpit (email)
- Auth stub service for local JWT generation without Entra ID
- `make dev` starts full local stack
- Per-service port map: identity:5001, platform:5002, sfa:5010, css:5020, marketing:5030, analytics:5040, ai:5050, bff:5060
