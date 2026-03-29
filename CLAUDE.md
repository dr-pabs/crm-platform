# CRM Platform — Root CLAUDE.md

> This file applies to every agent working anywhere in this repository.
> Read this before writing any code. Service-level CLAUDE.md files extend these rules.

## Project Overview

A multi-tenant SaaS CRM platform with four modules: SFA, CS&S, Marketing Automation, Analytics.
Backend: C# .NET 8 microservices. Frontend: TypeScript/React. Infrastructure: Azure PaaS (Bicep).
Dual deployment: SaaS (shared, multi-tenant) and client-hosted (dedicated Azure subscription).

## Architecture Principles

- **Multi-tenant first**: every EF Core entity MUST have `TenantId` (Guid), filtered via `HasQueryFilter`. No exceptions.
- **Service-to-service**: always via Azure Service Bus — never direct HTTP calls between backend services.
- **Auth**: always validate JWT claims middleware BEFORE any controller action. Never trust unvalidated input.
- **Secrets**: NEVER in code, config files, or environment variables. Key Vault only, accessed via Managed Identity.
- **No stored credentials**: all Azure service access uses Managed Identity. No connection strings with passwords.

## Established Patterns

- **Base template**: copy from `/src/services/_template` — never start a new service from scratch.
- **Async**: thread `CancellationToken` through every async method signature without exception.
- **Service Bus consumers**: must be idempotent — always check `MessageId` for duplicates before processing.
- **Service layer returns**: use `Result<T>` pattern — never throw exceptions for expected business logic failures.
- **EF Core SaveChanges**: never call `SaveChangesAsync` more than once per unit of work.
- **Soft deletes**: never hard-delete any business entity. Use `IsDeleted` + `DeletedAt` fields.
- **Pagination**: every list endpoint MUST be paginated. Never return unbounded result sets.

## Testing Standards

- **Coverage gate**: 80% minimum — CI pipeline fails below this. No exceptions or suppressions without Tech Director approval.
- **Integration tests**: must hit real Azure SQL in the dev subscription. Never use EF Core in-memory provider for integration tests.
- **Every endpoint requires**:
  1. Happy path test
  2. 401 unauthorised test (no token)
  3. Tenant isolation test — call with TenantB JWT, assert zero TenantA data returned
- **Tenant isolation test is mandatory** — this is the most critical security test in the codebase.

## Do Not

- Add NuGet or npm packages without a comment in the PR explaining the justification.
- Use `static` classes or fields for stateful logic.
- Write directly to the database from controllers — always through the service layer.
- Return HTTP 500 for business rule violations — use 400/422 with a structured `ProblemDetails` body.
- Log PII (email addresses, names, phone numbers, IP addresses) to Application Insights or any log sink.
- Use `DateTime.Now` — always use `DateTime.UtcNow` or `TimeProvider`.
- Catch and swallow exceptions silently — always log or rethrow.
- Write TODO comments without a linked GitHub issue number.

## Cross-Cutting Concerns

### Tenant Context
Every HTTP request must have `X-Tenant-Id` resolved from the JWT `tid` claim and stored in `ITenantContext`.
The `TenantId` is injected into all EF Core query filters automatically — never filter by TenantId manually in queries.

### Service Bus Topics
| Topic | Purpose |
|---|---|
| `crm.sfa` | Lead, opportunity, quote events |
| `crm.css` | Case, escalation, SLA events |
| `crm.marketing` | Journey, campaign, segment events |
| `crm.identity` | User provisioning, consent events |
| `crm.platform` | Tenant lifecycle, health, billing events |

### Error Response Format
All errors must return `ProblemDetails` (RFC 7807):
```json
{ "type": "...", "title": "...", "status": 400, "detail": "...", "traceId": "..." }
```

### Observability
- Structured logging via `ILogger<T>` — all log entries must include `TenantId` and `CorrelationId`.
- Distributed tracing via OpenTelemetry — all services export to Application Insights.
- Health endpoints: `/health/live`, `/health/ready`, `/health/start` on every service.

## Repository Structure

```
src/services/          → .NET 8 microservices (one folder per service)
src/services/_template → base microservice template — copy this to create new services
src/functions/         → Azure Durable Functions (journey engine, SLA timers, lead score decay)
src/frontend/          → React/TypeScript applications
  staff-portal/        → main CRM UI (agents, sales reps, managers)
  customer-portal/     → customer-facing portal (Entra External ID auth)
infra/modules/         → reusable Bicep modules
infra/platform/        → SaaS platform deployment (main.bicep + parameters per env)
infra/client-hosted/   → client-hosted deployment template
scripts/               → operational scripts (provision-tenant.sh, deprovision-tenant.sh)
docs/adr/              → Architecture Decision Records
docs/runbooks/         → operational runbooks
.github/workflows/     → CI/CD pipelines
```
