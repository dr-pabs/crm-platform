# css-service — CLAUDE.md

All rules in `/CLAUDE.md` and `/src/services/_template/CLAUDE.md` apply.

## Domain

The CS&S service owns all customer support cases. It enforces two isolation layers: (1) tenant isolation via TenantId EF query filter, and (2) customer/company isolation — a customer portal user may only see cases for their own AccountId. Staff see all cases within the tenant.

## Key Entities

| Entity | Key Fields | Notes |
|--------|-----------|-------|
| `Case` | TenantId, Title, Description, Status, Priority, Channel, ContactId, AccountId, AssignedToUserId, SlaDeadline, DurableFunctionInstanceId | Soft delete |
| `CaseComment` | TenantId, CaseId, AuthorId, AuthorType (Staff/Customer), Body, IsInternal | IsInternal=true hidden from customer portal |
| `CaseAttachment` | TenantId, CaseId, FileName, BlobUri, UploadedBy | Azure Blob Storage |
| `SlaPolicy` | TenantId, Name, Priority, FirstResponseMinutes, ResolutionMinutes, BusinessHoursOnly | Per-tenant configurable |
| `EscalationRecord` | TenantId, CaseId, EscalatedAt, EscalatedBy, Reason, PreviousAssigneeId, NewAssigneeId | Immutable audit |
| `CaseLinkedOpportunity` | TenantId, CaseId, OpportunityId | Bridge table |

All entities inherit `BaseEntity`.

### Case Status Machine

```
New → Open → Pending → Resolved → Closed
        ↓
    Escalated → Open (reassigned)
```

- Cases move to `Closed` only from `Resolved`. Closed cases are read-only.
- `Escalated` is a sub-state: case returns to `Open` after escalation completes.
- SLA clock starts on `New → Open` transition.
- SLA clock pauses on `Open → Pending`, resumes on customer reply.

### Case Priority
`Low` | `Medium` | `High` | `Critical`

### Case Channel
`Email` | `Phone` | `Portal` | `Chat` | `API`

## API Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/api/v1/css/cases` | SupportAgent+ | Paginated list (filter: status, priority, assignee) |
| `POST` | `/api/v1/css/cases` | SupportAgent+ | Create case |
| `GET` | `/api/v1/css/cases/{id}` | SupportAgent+ | Case detail |
| `PATCH` | `/api/v1/css/cases/{id}/status` | SupportAgent+ | Transition status |
| `POST` | `/api/v1/css/cases/{id}/assign` | SupportManager+ | Assign to agent |
| `POST` | `/api/v1/css/cases/{id}/escalate` | SupportAgent+ | Trigger escalation |
| `POST` | `/api/v1/css/cases/{id}/comments` | SupportAgent+ | Add comment |
| `GET` | `/api/v1/css/cases/{id}/comments` | SupportAgent+ | List comments (IsInternal filtered by role) |
| `GET` | `/api/v1/css/sla-policies` | TenantAdmin | List SLA policies |
| `POST` | `/api/v1/css/sla-policies` | TenantAdmin | Create SLA policy |

Every list endpoint: paginated, max page size 100.

## Service Bus

### Published (topic: `crm.css`)

| Event | Trigger |
|-------|---------|
| `case.created` | New case opened |
| `case.assigned` | Case assigned to agent |
| `case.status.changed` | Any status transition |
| `case.escalated` | Escalation triggered |
| `sla.breached` | SLA deadline passed (fired by SLA monitor) |
| `case.resolved` | Case moved to Resolved |
| `case.closed` | Case moved to Closed |

### Consumed

| Topic | Event | Action |
|-------|-------|--------|
| `crm.sfa` | `opportunity.won` | Auto-create onboarding case if tenant has rule configured |
| `crm.platform` | `tenant.suspended` | Mark all open cases as suspended (read-only) |

## Business Rules

- Customer portal users (JWT `companyId` claim) may only see cases where `AccountId == companyId`. This is a SECOND query filter applied on top of the tenant filter. It is NOT the same as the tenant filter.
- `portal.superuser` role: bypasses the company filter within their tenant.
- Staff (SupportAgent, SupportManager, TenantAdmin) have no company filter — they see all cases in the tenant.
- `IsInternal = true` comments are never returned to customer portal requests. Filter based on caller role in the handler, not the EF query filter.
- Closed cases are immutable: status change, comment, assignment all return HTTP 422.
- SLA breach alert fires at 80% of window (early warning) AND at 100% (actual breach). Both publish `sla.breached` with different severity fields.
- `DurableFunctionInstanceId` is stored on the Case record immediately after orchestration starts — allows cancel on resolve/close.

## SLA Monitor

Phase 2 implementation: `IHostedService` background worker polls for cases past their SLA deadline every 60 seconds and publishes `sla.breached`. (Durable Function upgrade is Phase 3.)

## Test Requirements

- Tenant isolation: TenantB token cannot read TenantA cases — mandatory on every endpoint (CI hard gate).
- Customer isolation: CustomerA (companyId=X) cannot read cases for CustomerB (companyId=Y) within the same tenant.
- Internal comment: assert customer portal request does NOT return `IsInternal = true` comments.
- Closed case: assert all write operations return 422 on a Closed case.
- SLA monitor: unit test that cases past deadline are identified and event published.
