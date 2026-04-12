# sfa-service — CLAUDE.md

All rules in `/CLAUDE.md` and `/src/services/_template/CLAUDE.md` apply.

## Domain

The SFA service is the authoritative source for the sales pipeline. It owns Leads, Contacts, Accounts, Opportunities, Quotes, and Activities. No other service writes to these entities. Other services consume events from `crm.sfa`.

## Key Entities

| Entity | Key Fields | Notes |
|--------|-----------|-------|
| `Lead` | TenantId, Name, Email, Phone, Company, Source, Status, Score, AssignedToUserId | Soft delete. Score 0–100. |
| `Contact` | TenantId, FirstName, LastName, Email, Phone, AccountId | FK to Account |
| `Account` | TenantId, Name, Industry, Size, BillingAddress | Parent of Contacts and Opportunities |
| `Opportunity` | TenantId, Title, Stage, Value, CloseDate, ContactId, AccountId, AssignedToUserId, ConvertedFromLeadId | Pipeline stages: Qualify → Propose → Negotiate → Won/Lost |
| `Quote` | TenantId, OpportunityId, LineItemsJson, TotalValue, Status, ValidUntil | Linked to Opportunity. Status: Draft/Sent/Accepted/Rejected |
| `Activity` | TenantId, Type, RelatedEntityId, RelatedEntityType, OccurredAt, Notes, AuthorUserId | Polymorphic. Types: Call/Email/Meeting/Note |

All entities inherit `BaseEntity`.

### Lead Status
`New` → `Contacted` → `Qualified` → `Converted` | `Disqualified`

### Opportunity Stage (ordered — must follow sequence)
`Qualify` → `Propose` → `Negotiate` → `Won` | `Lost`

### Quote Status
`Draft` → `Sent` → `Accepted` | `Rejected`

## API Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/api/v1/sfa/leads` | SalesRep+ | Paginated list (filter: status, assignee) |
| `POST` | `/api/v1/sfa/leads` | SalesRep+ | Create lead |
| `GET` | `/api/v1/sfa/leads/{id}` | SalesRep+ | Lead detail |
| `PATCH` | `/api/v1/sfa/leads/{id}` | SalesRep+ | Update lead fields |
| `DELETE` | `/api/v1/sfa/leads/{id}` | SalesManager+ | Soft delete |
| `POST` | `/api/v1/sfa/leads/{id}/assign` | SalesManager+ | Assign to user |
| `POST` | `/api/v1/sfa/leads/{id}/convert` | SalesRep+ | Convert → Opportunity (saga) |
| `GET` | `/api/v1/sfa/opportunities` | SalesRep+ | Paginated opportunity list |
| `POST` | `/api/v1/sfa/opportunities` | SalesRep+ | Create opportunity |
| `GET` | `/api/v1/sfa/opportunities/{id}` | SalesRep+ | Opportunity detail |
| `PATCH` | `/api/v1/sfa/opportunities/{id}/stage` | SalesRep+ | Advance stage (sequential only) |
| `GET` | `/api/v1/sfa/contacts` | SalesRep+ | Paginated contact list |
| `POST` | `/api/v1/sfa/contacts` | SalesRep+ | Create contact |
| `GET` | `/api/v1/sfa/accounts` | SalesRep+ | Paginated account list |
| `POST` | `/api/v1/sfa/accounts` | SalesRep+ | Create account |

Every list endpoint: paginated, max page size 100.

## Service Bus

### Published (topic: `crm.sfa`)
| Event | Trigger |
|-------|---------|
| `lead.created` | New lead |
| `lead.assigned` | Lead assigned to user |
| `lead.converted` | Lead converted to Opportunity |
| `opportunity.stage.changed` | Stage transition |
| `opportunity.won` | Stage set to Won |
| `opportunity.lost` | Stage set to Lost |
| `quote.sent` | Quote sent to contact |

### Consumed
| Topic | Event | Action |
|-------|-------|--------|
| `crm.marketing` | `journey.completed` | Update lead status |
| `crm.platform` | `tenant.suspended` | Soft-delete or mark suspended — no active changes permitted |

## Business Rules

- A Lead may only be converted once. Second convert attempt returns HTTP 422 with ProblemDetails.
- Lead conversion is a two-step saga: (1) SFA creates Opportunity + publishes `lead.converted`, (2) consumer marks Lead as Converted. Idempotent on MessageId.
- Lead score is 0–100. Score is written by the SFA service only. Decay is applied by a nightly background job (not a Durable Function in Phase 2 — use `IHostedService` with a timer).
- Opportunity stages must follow the defined sequence: Qualify → Propose → Negotiate → Won/Lost. Skipping is not permitted.
- A Quote may only have status `Sent` when linked Opportunity is in `Negotiate` stage or later.
- `SalesRep` may only read/write records where `AssignedToUserId == currentUser.UserId` OR where they created the record. `SalesManager` sees all records for the tenant. `TenantAdmin` sees all.
- Tenant isolation test is mandatory on every endpoint (CI hard gate).

## Test Requirements

- Tenant isolation: TenantB token cannot read TenantA leads — mandatory on every endpoint.
- Lead convert: happy path returns Opportunity. Second convert call returns 422.
- Stage advance: sequential advance succeeds. Skipping a stage returns 422.
- Score field: assert score defaults to 0 on creation and is validated 0–100.
- Role filter: `SalesRep` cannot see another rep's unassigned leads.
