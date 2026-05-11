# CRM Platform — End-to-End Test Scenarios

**Date:** 2026-05-11

---

## Overview

These E2E scenarios test complete business flows across multiple services. They require:
- Running SQL Server (local Docker or Azure dev)
- Running Service Bus (emulator or Azure dev)
- Running AI stub (in-memory Claude client)
- All 10 backend services started

Each scenario defines: preconditions, steps, expected outcomes, and service boundaries crossed.

---

## Scenarios

### SFA-001: Lead to Closed Opportunity (Full Pipeline)

**Services:** sfa-service, ai-orchestration-service, identity-service

**Preconditions:** TenantA exists. SalesRep user authenticated.

| Step | Action | Service | Expected |
|---|---|---|---|
| 1 | Create Lead (Name, Email, Company) | sfa-service | 201 Created, LeadId returned |
| 2 | Verify LeadCreatedEvent published to crm.sfa | ai-orchestration-service | AiJob queued for LeadScoring |
| 3 | Wait for AiJob to complete | ai-orchestration-service | AiJob status = Succeeded, LeadScoredEvent published |
| 4 | GET Lead → verify Score > 0 | sfa-service | Lead.Score > 0 |
| 5 | Assign Lead to SalesRep | sfa-service | Lead.Status = Contacted, LeadAssignedEvent published |
| 6 | Qualify Lead | sfa-service | Lead.Status = Qualified |
| 7 | Convert Lead to Opportunity | sfa-service | 200 OK, OpportunityId returned, Lead.IsConverted = true |
| 8 | GET Opportunity → verify stage = Prospecting | sfa-service | Opportunity.Stage = Prospecting |
| 9 | Advance stage: Prospecting → Qualification | sfa-service | 204 No Content |
| 10 | Advance stage: Qualification → Proposal | sfa-service | 204 No Content |
| 11 | Advance stage: Proposal → Negotiation | sfa-service | 204 No Content |
| 12 | Create Quote for Opportunity | sfa-service | 201 Created, QuoteId returned |
| 13 | Send Quote | sfa-service | 204 No Content, Quote.SentEvent published |
| 14 | Accept Quote | sfa-service | 204 No Content, Quote.Status = Accepted |
| 15 | Advance stage: Negotiation → ClosedWon | sfa-service | 204 No Content, OpportunityWonEvent published |
| 16 | Verify NextBestAction generated | ai-orchestration-service | AiJob Succeeded for NextBestAction |

---

### CSS-001: Case Lifecycle with AI Summarisation

**Services:** css-service, ai-orchestration-service, identity-service

**Preconditions:** TenantA exists. SupportAgent user authenticated. Contact exists.

| Step | Action | Service | Expected |
|---|---|---|---|
| 1 | Create Case (Subject, Priority=High, ContactId) | css-service | 201 Created, CaseId returned |
| 2 | GET Case → verify Status = New | css-service | Case.Status = New |
| 3 | Open Case (starts SLA clock) | css-service | Case.Status = Open, SlaDeadline set |
| 4 | Add Case Comment (customer reply) | css-service | 201 Created, CommentId returned |
| 5 | Verify SentimentAnalysis AiJob queued | ai-orchestration-service | AiJob for SentimentAnalysis created |
| 6 | Wait for SentimentAnalysis | ai-orchestration-service | AiJob Succeeded, SentimentAnalysedEvent published |
| 7 | Set Case to Pending (waiting customer) | css-service | Case.Status = Pending |
| 8 | Resume Case (customer replies) | css-service | Case.Status = Open |
| 9 | Add internal note | css-service | Comment.IsInternal = true |
| 10 | Escalate Case | css-service | Case.Status = Escalated, EscalationRecord created |
| 11 | Complete Escalation | css-service | Case.Status = Open |
| 12 | Resolve Case | css-service | Case.Status = Resolved |
| 13 | Verify CaseSummarisation AiJob queued | ai-orchestration-service | AiJob for CaseSummarisation created |
| 14 | Wait for summarisation | ai-orchestration-service | AiJob Succeeded, CaseSummarisedEvent published |
| 15 | GET Case → verify AI summary populated | css-service | Case.AiSummary not null |
| 16 | Close Case | css-service | Case.Status = Closed |
| 17 | Attempt to modify closed Case | css-service | 409 Conflict |

---

### CSS-002: Tenant Isolation (Security Critical)

**Services:** css-service

**Preconditions:** TenantA and TenantB exist. TenantA has CaseA. TenantB has CaseB.

| Step | Action | Expected |
|---|---|---|
| 1 | GET /cases with TenantA JWT | Returns only CaseA, not CaseB |
| 2 | GET /cases/{CaseBId} with TenantA JWT | 404 Not Found |
| 3 | POST /cases with TenantB JWT, ContactId=TenantA contact | 403 Forbidden or 400 Validation |
| 4 | GET /cases with no JWT | 401 Unauthorised |

---

### MKT-001: Campaign → Journey → AI Personalisation

**Services:** marketing-service, ai-orchestration-service

**Preconditions:** TenantA exists. Campaign created.

| Step | Action | Service | Expected |
|---|---|---|---|
| 1 | Create Campaign (Name, Channel=Email) | marketing-service | 201 Created |
| 2 | Schedule Campaign (future date) | marketing-service | Campaign.Status = Scheduled |
| 3 | Activate Campaign | marketing-service | Campaign.Status = Active, CampaignActivatedEvent |
| 4 | Create Journey attached to Campaign | marketing-service | 201 Created, JourneyId returned |
| 5 | Set Journey steps (3-step email sequence) | marketing-service | Journey.StepCount = 3 |
| 6 | Publish Journey | marketing-service | Journey.Status = Active, JourneyPublishedEvent |
| 7 | Create JourneyEnrollment (Contact in journey) | marketing-service | 201 Created |
| 8 | Verify JourneyPersonalisation AiJob queued | ai-orchestration-service | AiJob for JourneyPersonalisation |
| 9 | Wait for personalisation | ai-orchestration-service | AiJob Succeeded, personalised branch returned |
| 10 | Complete Campaign | marketing-service | Campaign.Status = Completed |
| 11 | Verify Campaign metrics updated | marketing-service | Impressions/Clicks/Conversions incremented |

---

### AI-001: On-Demand AI (Email Draft + Case Summarisation)

**Services:** ai-orchestration-service

**Preconditions:** TenantA exists. AI stub configured.

| Step | Action | Expected |
|---|---|---|
| 1 | POST /ai/email-draft { leadName, company, productInterest } | 200 OK, email draft returned |
| 2 | Verify AiResult stored | AiResult with capability=EmailDraft |
| 3 | POST /ai/email-draft/stream (SSE) | 200 OK, streaming response |
| 4 | POST /ai/case-summarise { caseId, caseData } | 202 Accepted, AiJob queued |
| 5 | GET /ai/jobs/{jobId} → poll until Succeeded | AiJob.Status = Succeeded |
| 6 | GET /ai/results/{resultId} | AiResult with summary content |

---

### AI-002: Model Routing (Multi-Model)

**Services:** ai-orchestration-service, agent-service

**Preconditions:** Claude + GPT-4o registered in ModelRegistry.

| Step | Action | Expected |
|---|---|---|
| 1 | Configure tenant to use cost-optimised routing | Tenant config updated |
| 2 | POST /ai/email-draft (low-stakes intent) | Routed to cheapest model |
| 3 | Verify ModelUsage record shows correct model | ModelUsage.ModelId matches cheapest |
| 4 | POST /ai/email-draft with quality-preferred header | Routed to quality model |
| 5 | Simulate primary model failure | Fallback to secondary model |
| 6 | Verify ModelUsage shows fallback chain used | FallbackModelId populated |

---

### ID-001: Auth Flow

**Services:** identity-service, all services

**Preconditions:** Entra ID configured (dev stub in local).

| Step | Action | Expected |
|---|---|---|
| 1 | Request without JWT → any endpoint | 401 Unauthorised |
| 2 | Request with valid JWT (TenantA, SalesRep role) | 200 OK |
| 3 | Request with valid JWT but missing tid claim | 403 Forbidden |
| 4 | Request with expired JWT | 401 Unauthorised |
| 5 | Request with valid JWT (TenantB) → TenantA resource | 404 Not Found (tenant isolation) |

---

### BFF-001: Staff Dashboard Aggregation

**Services:** staff-bff, sfa-service, css-service

**Preconditions:** TenantA has leads and cases.

| Step | Action | Expected |
|---|---|---|
| 1 | GET /dashboard (via staff-bff) | 200 OK |
| 2 | Verify lead count matches sfa-service response | Dashboard.openLeads = sfa lead count |
| 3 | Verify case count matches css-service response | Dashboard.openCases = css case count |
| 4 | Simulate sfa-service down | BFF returns partial response with sfa fields = 0 |
| 5 | Simulate css-service down | BFF returns partial response with css fields = 0 |

---

### INF-001: Health Endpoints

**Services:** all

**Preconditions:** All services running.

| Step | Action | Expected |
|---|---|---|
| 1 | GET /health/live on every service | 200 OK (all) |
| 2 | GET /health/ready on every service | 200 OK (DB + SB healthy) |
| 3 | Stop SQL Server | /health/ready returns 503 on all services |
| 4 | Restart SQL Server | /health/ready returns 200 within 30s |
| 5 | GET /health/live during DB outage | 200 OK (liveness unaffected) |

---

### PERF-001: Performance Baseline

**Services:** sfa-service

**Preconditions:** 10,000 leads seeded in TenantA.

| Step | Action | Expected |
|---|---|---|
| 1 | GET /leads?page=1&pageSize=25 | Response < 200ms P95 |
| 2 | GET /leads?page=1&pageSize=100 | Response < 500ms P95 |
| 3 | POST /leads (create) | Response < 300ms P95 |
| 4 | GET /leads?search=smith | Response < 500ms P95 |
| 5 | Concurrent: 50 simultaneous GET /leads | No errors, P95 < 1s |

---

## Test Execution Matrix

| Scenario | Category | Services | Requires AI Stub |
|---|---|---|---|
| SFA-001 | Functional | 3 | Yes (LeadScoring, NextBestAction) |
| CSS-001 | Functional | 2 | Yes (Sentiment, Summarisation) |
| CSS-002 | Security | 1 | No |
| MKT-001 | Functional | 2 | Yes (JourneyPersonalisation) |
| AI-001 | Functional | 1 | Yes (stub returns predefined) |
| AI-002 | Functional | 2 | Yes (multi-model routing) |
| ID-001 | Security | All | No |
| BFF-001 | Integration | 3 | No |
| INF-001 | Operational | All | No |
| PERF-001 | Performance | 1 | No |
