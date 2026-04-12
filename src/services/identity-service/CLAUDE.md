# identity-service — CLAUDE.md

All rules in `/CLAUDE.md` and `/src/services/_template/CLAUDE.md` apply.

## Domain

The identity service is the authoritative source for user identity within every tenant. It provisions users from Entra External ID, manages role assignments, records consent, and maintains tenant registry entries used by the APIM/middleware tenant-lookup endpoint. No other service may read or write user identity or role data directly — they must subscribe to events from `crm.identity`.

## Key Entities

| Entity | Key Fields | Notes |
|--------|-----------|-------|
| `TenantUser` | TenantId, EntraObjectId, Email, DisplayName, Status | Status: Active \| Suspended \| Deprovisioned |
| `UserRole` | TenantUserId (FK), Role (string enum), GrantedAt, GrantedBy | A user may hold multiple roles within a tenant |
| `TenantRegistry` | TenantId, EntraTenantId, ExternalIdTenantId, Status | Internal use — middleware tenant-lookup |
| `ConsentRecord` | TenantUserId (FK), ConsentType, ConsentedAt, IpAddressHash | NEVER raw IP. NEVER deleted — immutable audit. |
| `UserProvisioningLog` | TenantUserId (FK), Action, OccurredAt, InitiatedBy | Immutable audit — no soft delete, no hard delete |

### Roles (7)
`SalesRep` · `SalesManager` · `SupportAgent` · `SupportManager` · `MarketingUser` · `TenantAdmin` · `PlatformAdmin`

> **Rule**: `PlatformAdmin` is a platform-level role — it is NOT stored as a tenant-scoped `UserRole`. It is embedded in the JWT by the platform-admin-service and validated at the APIM layer only.

## API Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/api/v1/identity/users` | TenantAdmin | List users for tenant (paginated) |
| `POST` | `/api/v1/identity/users` | TenantAdmin | Provision user from Entra External ID |
| `DELETE` | `/api/v1/identity/users/{id}` | TenantAdmin | Deprovision user (soft delete) |
| `GET` | `/api/v1/identity/users/{id}/roles` | TenantAdmin | List roles for user |
| `POST` | `/api/v1/identity/users/{id}/roles` | TenantAdmin | Grant role to user |
| `DELETE` | `/api/v1/identity/users/{id}/roles/{role}` | TenantAdmin | Revoke role from user |
| `GET` | `/api/v1/identity/tenants/{tenantId}` | **Internal only** (no public APIM route) | Tenant registry lookup for middleware |
| `POST` | `/api/v1/identity/consent` | Authenticated user | Record user consent (GDPR) |

## Service Bus

### Published (topic: `crm.identity`)
| Event | Trigger |
|-------|---------|
| `user.provisioned` | User created and active |
| `user.deprovisioned` | User soft-deleted |
| `user.role.granted` | Role assigned to user |
| `user.role.revoked` | Role removed from user |
| `consent.recorded` | Consent record written |

### Consumed
| Topic | Event | Action |
|-------|-------|--------|
| `crm.platform` | `tenant.suspended` | Soft-delete all TenantUsers for tenant (revoke sessions) |
| `crm.platform` | `tenant.deprovisioned` | GDPR hard-delete all user PII. ConsentRecords retained (anonymised). |

## Business Rules

1. **Multi-role**: A user may hold multiple roles within a single tenant simultaneously.
2. **PlatformAdmin isolation**: Never store `PlatformAdmin` as a `UserRole` row. Validate its absence in `GrantRoleCommand`.
3. **Role cache TTL**: 60 seconds in production; 0 (disabled) in Development.
4. **Idempotency**: All commands use `BaseServiceBusConsumer` idempotency store. Re-delivery of `user.provisioned` must not create duplicate records.
5. **GDPR erasure**: Hard-delete user PII ONLY after receiving `tenant.deprovisioned` from `crm.platform`. Do NOT hard-delete on a user-level deprovision request — use soft delete.
6. **Consent immutability**: `ConsentRecord` rows are never deleted or updated. The `UserProvisioningLog` is similarly immutable.
7. **IP hashing**: Store SHA-256 hash of IP address in `ConsentRecord.IpAddressHash`. Never log or persist raw IP.
8. **Analytics forwarding**: Emit `crm.analytics` shadow events for every state change from day one.
9. **No direct HTTP calls**: Never call platform-admin-service or any other service over HTTP. React to events only.
10. **Tenant isolation test mandatory**: Every endpoint must have a test asserting cross-tenant data is not accessible.
