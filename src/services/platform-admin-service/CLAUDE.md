# platform-admin-service — CLAUDE.md

All rules in `/CLAUDE.md` and `/src/services/_template/CLAUDE.md` apply.

## Domain

The platform-admin-service owns the tenant lifecycle — provisioning, suspension, reinstatement, deprovisioning, and GDPR erasure. It is the only service that may use `.IgnoreQueryFilters()` on cross-tenant queries, because PlatformAdmin operations are by definition cross-tenant. It orchestrates multi-step lifecycle sagas (ADR 0009) using Durable Functions. No other service may create or modify Tenant records.

## Key Entities

| Entity | Key Fields | Notes |
|--------|-----------|-------|
| `Tenant` | Name, Slug, Status, PlanId, ProvisionedAt, SuspendedAt | Status: Provisioning → Active → Suspended → Deprovisioning → Deprovisioned → Erased |
| `TenantProvisioningLog` | TenantId, Step, StepStatus, OccurredAt, Details | Immutable. Written by saga orchestrator. |
| `Deployment` | ServiceName, Version, Environment, DeployedAt, DeployedBy | Written by CD pipeline post-deploy hook |
| `PlatformHealthRecord` | ServiceName, Status, CheckedAt, Details | Aggregated from service health endpoints |

## API Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/api/v1/platform/tenants` | PlatformAdmin | List all tenants (paginated) |
| `POST` | `/api/v1/platform/tenants` | PlatformAdmin | Initiate tenant provisioning saga |
| `GET` | `/api/v1/platform/tenants/{id}` | PlatformAdmin | Get tenant detail |
| `PATCH` | `/api/v1/platform/tenants/{id}` | PlatformAdmin | Update tenant metadata (name, plan) |
| `DELETE` | `/api/v1/platform/tenants/{id}` | PlatformAdmin | Initiate tenant deprovisioning saga |
| `POST` | `/api/v1/platform/tenants/{id}/suspend` | PlatformAdmin | Initiate suspension saga |
| `POST` | `/api/v1/platform/tenants/{id}/reinstate` | PlatformAdmin | Lift suspension |
| `POST` | `/api/v1/platform/tenants/{id}/gdpr-erase` | PlatformAdmin | Trigger GDPR erasure (72h deadline) |
| `GET` | `/api/v1/platform/tenants/{id}/audit` | PlatformAdmin | Tenant provisioning audit log |
| `GET` | `/api/v1/platform/health` | PlatformAdmin | Aggregated platform health |

## Service Bus

### Published (topic: `crm.platform`)

| Event | Trigger |
|-------|---------|
| `tenant.provisioned` | Provisioning saga step 5 completed |
| `tenant.suspended` | Suspension saga completed |
| `tenant.reactivated` | Suspension lifted |
| `tenant.deprovisioned` | Deprovisioning saga completed |
| `tenant.erased` | GDPR erasure completed |
| `platform.health.degraded` | Aggregated health check fails > 2 min |

### Consumed

None in Phase 1. Downstream services react to `crm.platform` events.

## Tenant Lifecycle Saga (ADR 0009)

### Provisioning (5 steps, all compensating)

1. `CreateTenantDatabaseRecord` → compensate: `DeleteTenantDatabaseRecord`
2. `ProvisionEntraIdApplication` → compensate: `DeleteEntraIdApplication`
3. `CreateServiceBusSubscriptions` → compensate: `DeleteServiceBusSubscriptions`
4. `SeedTenantDefaultData` → compensate: `DeleteTenantDefaultData`
5. `SetTenantStatusActive` → publish `tenant.provisioned` to `crm.platform`

### Suspension (4 steps, all must succeed or all compensate)

1. `RevokeUserSessions` (publishes event, identity-service reacts)
2. `PauseActiveCampaigns` (publishes event, marketing-service reacts)
3. `SuspendOpenCases` (publishes event, css-service reacts)
4. `SetTenantStatusSuspended` → publish `tenant.suspended`

## Business Rules

1. **PlatformAdmin only**: All endpoints require `PlatformAdmin` role. No tenant-scoped user may access platform endpoints.
2. **Slug uniqueness**: Tenant slug must be globally unique — validated before provisioning saga starts.
3. **GDPR deadline**: GDPR erasure must complete within 72 hours of `tenant.erased` event publish.
4. **Audit retention**: `TenantProvisioningLog` and `Deployment` records retained 7 years. Never soft-deleted.
5. **Saga idempotency**: All saga steps are idempotent — re-running a completed step must be a no-op.
6. **No cross-tenant leak**: Even though PlatformAdmin may query across tenants, API responses must never accidentally include data for a tenant not in the request scope.
7. **Analytics forwarding**: Emit `crm.analytics` shadow events for every tenant status transition from day one.
