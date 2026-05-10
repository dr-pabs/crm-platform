# CRM Platform — Implementation Plan

**Date:** 2026-05-10
**Repo:** `dr-pabs/crm-platform`

---

## Phase 1: Data Model Alignment — 4/10 done

### Critical (done)

- [x] PR #30 — Align Opportunity stage enum with UI names
- [x] PR #31 — Split Lead.Name into FirstName+LastName+JobTitle
- [x] PR #32 — Rename Case.Title to Subject
- [x] PR #33 — Add New and Escalated to Staff UI CaseStatus

### Short-term (pending)

- [ ] 5. Build Quote UI — list, detail, create, send, accept, reject
- [ ] 6. Add Activity timeline to Lead/Contact/Account/Opportunity detail pages
- [ ] 7. Account entity: add Phone, AnnualRevenue, rename Size→EmployeeCount
- [ ] 8. Campaign entity: add Impressions, Clicks, Conversions
- [ ] 9. Campaign create/edit form
- [ ] 10. Journey status: IsPublished bool → proper enum (Draft/Active/Paused/Completed/Archived)

### Naming standardisation (later)

- [ ] Standardise: Title→Name, Value→Amount, Size→EmployeeCount, AssignedToUserId→OwnerId

---

## Phase 2: UI/UX Upgrade

- [ ] Install UI UX Pro Max skill (`npm install -g uipro-cli && uipro init --ai claude`)
- [ ] Generate design system for SaaS CRM product type
- [ ] Apply consistent color palette, typography, spacing tokens
- [ ] Redesign dashboard with chart recommendations
- [ ] Mobile-responsive audit
- [ ] Accessibility pass (contrast, focus states, ARIA labels)

---

## Phase 3: AI Feature Expansion

- [ ] Review existing 9 AI capabilities (LeadScoring, EmailDraft, CaseSummarisation, SentimentAnalysis, NextBestAction, JourneyPersonalisation, SmsComposition, TeamsNotification, TeamsCall)
- [ ] Identify gaps: churn prediction, pipeline forecasting, RAG over knowledge base, voice agent
- [ ] Prioritise based on business impact and implementation complexity

---

## Phase 4: Headless CRM Strategy

- [ ] Document agent-driven interaction model (no customer UI)
- [ ] Design agent orchestration layer (multi-agent, tool-using)
- [ ] Map intents to CRM actions (case creation, status query, FAQ)
- [ ] Human-in-the-loop escalation design
- [ ] Audit trail of all agent decisions

---

## Completed (prior sessions)

- [x] 24 PRs merged — Dockerfiles, ADRs, CI cleanup, stability fixes
- [x] ADR 0013 — staff-bff required for R1
- [x] ADR 0006 — marked superseded
- [x] staff-bff Dockerfile
- [x] CD smoke test includes staff-bff
- [x] Customer portal restored (PR #26)
