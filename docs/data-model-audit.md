# CRM Platform — Data Model Audit

**Date:** 2026-05-10
**Scope:** All domain entities across 10 services vs. Staff Portal + Customer Portal UI types

---

## 1. Entity Inventory

### 1.1 SFA Service — Sales Force Automation

| Entity | Table | Schema | Purpose | Salesforce Equivalent |
|---|---|---|---|---|
| Lead | Leads | sfa | Sales prospect entry point | Lead |
| Contact | Contacts | sfa | Individual person record | Contact |
| Account | Accounts | sfa | Company/organisation record | Account |
| Opportunity | Opportunities | sfa | Sales deal pipeline | Opportunity |
| Quote | Quotes | sfa | Priced proposal linked to opportunity | Quote |
| Activity | Activities | sfa | Polymorphic timeline record | Task / Event |

### 1.2 CSS Service — Customer Service & Support

| Entity | Table | Schema | Purpose |
|---|---|---|---|
| Case | Cases | css | Support ticket / service request |
| CaseComment | CaseComments | css | Threaded comment on a case |
| EscalationRecord | EscalationRecords | css | Audit record of case escalation |
| SlaPolicy | SlaPolicies | css | SLA definition per priority level |

### 1.3 Marketing Service

| Entity | Table | Schema | Purpose |
|---|---|---|---|
| Campaign | Campaigns | marketing | Marketing initiative |
| Journey | Journeys | marketing | Multi-step automation flow |
| JourneyEnrollment | JourneyEnrollments | marketing | Contact enrollment in a journey |
| EmailTemplate | EmailTemplates | marketing | Reusable email content template |

### 1.4 AI Orchestration Service

| Entity | Table | Schema | Purpose |
|---|---|---|---|
| AiJob | AiJobs | ai | Async AI job queue with retry/status |
| AiResult | AiResults | ai | Immutable result record per job |
| PromptTemplate | PromptTemplates | ai | Per-tenant prompt with platform defaults |
| SmsRecord | SmsRecords | ai | Outbound SMS audit log |
| TeamsCallRecord | TeamsCallRecords | ai | Outbound Teams call audit log |

### 1.5 Analytics Service

| Entity | Table | Schema | Purpose |
|---|---|---|---|
| MetricSnapshot | MetricSnapshots | analytics | Periodic aggregated metric value |
| AnalyticsEvent | AnalyticsEvents | analytics | Raw event for metric computation |

### 1.6 Other Services (no domain entities)

| Service | Purpose |
|---|---|
| identity-service | User provisioning, Entra ID sync |
| platform-admin-service | Tenant lifecycle, billing, provisioning saga |
| notification-service | Multi-channel notification delivery |
| integration-service | Third-party OAuth flows (Salesforce, HubSpot) |
| staff-bff | Aggregation/composition for staff portal |

---

## 2. Critical Mismatches — Will Break at Runtime

### 2.1 Opportunity Stage Names

| Backend (C# Enum) | UI (TypeScript Type) |
|---|---|
| `Qualify` | `Prospecting` |
| — | `Qualification` |
| `Propose` | `Proposal` |
| `Negotiate` | `Negotiation` |
| `Won` | `ClosedWon` |
| `Lost` | `ClosedLost` |

Backend has 5 stages with sequential progression enforcement (`Qualify → Propose → Negotiate → Won/Lost`). UI has 6 stages with different names. **Stage transitions will fail.** The `AdvanceStageHandler` validates stage order — passing `"Prospecting"` or `"Qualification"` will fail enum parsing.

### 2.2 Lead Name Structure

- **Backend**: single `Name` field (string)
- **UI**: `firstName` + `lastName` + `jobTitle` (three separate fields)

`CreateLeadCommand` expects `Name`. UI sends `firstName`/`lastName`. **Create/update fails.**

### 2.3 Case Title vs Subject

- **Backend**: `Title` (on `Case` entity)
- **UI**: `subject` (in `Case` TypeScript type)

Field name mismatch. API sends `Title`, UI expects `subject`. **Display breaks.**

### 2.4 Case Status Gap

| Backend | Staff UI | Customer UI |
|---|---|---|
| `New` | — missing — | — |
| `Open` | `Open` | `Open` |
| `Pending` | `Pending` | `WaitingOnCustomer` |
| `Escalated` | — missing — | — |
| — | — | `InProgress` (no backend equiv) |
| `Resolved` | `Resolved` | `Resolved` |
| `Closed` | `Closed` | `Closed` |

Staff UI is missing `New` and `Escalated`. Customer UI has `InProgress` with no backend equivalent. `WaitingOnCustomer` is intentionally customer-friendly for `Pending` — this is good and should be preserved.

---

## 3. High — Missing UI Coverage

| # | Issue | Impact |
|---|---|---|
| 1 | **Quote entity has no UI** | Staff cannot create, send, or manage quotes at all |
| 2 | **Activity timeline only on CaseDetail** | No activity view on Lead, Contact, Account, or Opportunity detail pages |
| 3 | **Journey: IsPublished bool vs status enum** | UI has Draft/Active/Paused/Archived; backend has only a boolean |
| 4 | **Campaign page is read-only** | No create/edit form. Cannot create campaigns from UI |
| 5 | **Campaign metrics in UI but not backend** | Impressions, clicks, conversions are in UI types but have no DB columns |

---

## 4. Medium — Field-Level Mismatches

| Backend Field | UI Field | Issue |
|---|---|---|
| `Account.Size` (string) | `employees` (number) | Different type and semantics |
| `Account.BillingAddress` (string) | `billingAddress` (Address object) | Flat string vs structured street/city/state/postCode/country |
| `Account` (no phone) | `phone` (string) | Missing from backend entity |
| `Account` (no annualRevenue) | `annualRevenue` (number) | Missing from backend entity |
| `Contact` (no jobTitle) | `jobTitle` (string) | Missing from backend entity |
| `Lead.Source` (LeadSource enum) | `source` (string) | Enum vs free text |
| `Campaign.Status=Scheduled` | — | Missing from UI type |
| `Campaign.Status=Cancelled` | — | Missing from UI type |
| — | `Campaign.Status=Archived` | Missing from backend enum |

---

## 5. Customer-Friendly Naming Audit

| Entity | Backend Field | UI Label | Verdict |
|---|---|---|---|
| Case | `Title` | `subject` | Good — customer-friendly |
| Case | `Status=Pending` | `WaitingOnCustomer` | Good — more descriptive |
| Opportunity | `Value` | `amount` | Good — more intuitive |
| Opportunity | `Stage=Won` | `ClosedWon` | Good — clearer |
| Opportunity | `Stage=Lost` | `ClosedLost` | Good — clearer |
| Lead | `Name` | `firstName + lastName` | Better granularity |

---

## 6. Entity Relationship Diagram

```
                         +-----------+
                         |  Account  |
                         +-----+-----+
                               |
                 +-------------+-------------+
                 |                           |
            +----+----+               +-----+------+
            | Contact |               | Opportunity |
            +----+----+               +-----+------+
                 |                           |
                 |                     +-----+-----+
                 |                     |   Quote    |
                 |                     +-----------+
                 |
      +----------+----------+
      |                     |
 +----+----+          +-----+------+
 |  Lead   |          |   Case     |
 +---------+          +-----+------+
                            |
                  +---------+---------+
                  |                   |
           +------+------+    +-------+--------+
           | CaseComment |    | EscalationRecord|
           +-------------+    +----------------+

 +----------+          +-----------+
 | Campaign |----------|  Journey  |
 +----------+          +-----+-----+
                             |
                      +------+-------+
                      | Enrollment   |
                      +--------------+

 +----------+
 | Activity |---(polymorphic)--- Lead / Contact / Opportunity
 +----------+

 +---------+          +-----------+
 |  AiJob  |---------->| AiResult  |
 +---------+          +-----------+
```

**Relationships:**
- Account 1:N Contact, 1:N Opportunity
- Contact 1:N Opportunity, 1:N Lead  
- Opportunity 1:N Quote
- Campaign 1:N Journey
- Journey 1:N JourneyEnrollment
- Case 1:N CaseComment, 1:N EscalationRecord
- Activity: polymorphic via `RelatedEntityId` + `RelatedEntityType` ("Lead", "Opportunity", "Contact")
- AiJob 1:N AiResult (retries create multiple results)

**Cross-service (Service Bus events, no FK):**
- Lead → `LeadCreatedEvent` → AI orchestration → `LeadScoredEvent` → SFA service
- Case → `CaseResolvedEvent` → AI orchestration → `CaseSummarisedEvent` → CSS service
- Journey → `EnrollmentCreatedEvent` → AI orchestration → `JourneyPersonalisedEvent` → Marketing service

---

## 7. Recommendations

### Immediate (blockers — fix before any new features)

1. **Align Opportunity stage enum**: Use UI names (`Prospecting`, `Qualification`, `Proposal`, `Negotiation`, `ClosedWon`, `ClosedLost`). Update backend `OpportunityStage` enum and `Opportunity.AdvanceStage()` state machine.
2. **Fix Lead name**: Split backend `Lead.Name` into `FirstName` + `LastName`, OR have UI combine them into `Name`. Add `JobTitle` to backend.
3. **Rename Case.Title → Subject**: Pick one name. Backend rename is cleanest.
4. **Add `New` and `Escalated`** to Staff UI `CaseStatus` type.

### Short-term

5. **Build Quote UI**: List page, detail page, create/send/accept/reject actions.
6. **Add Activity timeline** to LeadDetail, ContactDetail, AccountDetail, OpportunityDetail pages.
7. **Add to Account entity**: `Phone`, `AnnualRevenue`, change `Size` (string) to `EmployeeCount` (int).
8. **Add to Campaign entity**: `Impressions`, `Clicks`, `Conversions`.
9. **Add Campaign create/edit form**.
10. **Fix Journey status**: Add proper enum (`Draft`, `Active`, `Paused`, `Completed`, `Archived`) instead of `IsPublished` bool.

### Naming Standardisation

| Concept | Current Backend | Current UI | Recommended |
|---|---|---|---|
| Deal name | `Title` | `name` | `Name` |
| Deal value | `Value` | `amount` | `Amount` |
| Ticket name | `Title` | `subject` | `Subject` |
| Company size | `Size` (string) | `employees` (number) | `EmployeeCount` (int) |
| Owner/user FK | `AssignedToUserId` | `ownerId` | `OwnerId` |
