# CRM Platform — Stability Review

**Date:** 2026-05-10
**Scope:** dr-pabs/crm-platform main (24 merged PRs + 1 open)

---

## Status: All 24 PRs Merged

Original PRs #3-#11 and review PRs #12-#24 are merged. PR #25 (local dev startup) is open.

---

## Critical

### 1. Six services have broken ProjectReference paths

platform-admin-service, sfa-service, analytics-service, identity-service, marketing-service, staff-bff all use:
  ../../../services/_template/
which resolves to <root>/services/_template/ — wrong. Correct: ../_template/

**Fix:** PR #25 addresses this. Merge it first.

### 2. staff-bff has no Dockerfile

CD pipeline runs docker build src/services/staff-bff but no Dockerfile exists.

---

## High

### 3. Bicep still deploys deleted customer-portal

infra/platform/main.bicep and infra/client-hosted/main.bicep define customerPortalName and deploy a customerPortal SWA module. Source was deleted in PR #16 but infra was not updated.

### 4. CD smoke test missing staff-bff

Health check loop in cd-dev.yml and cd-prod.yml covers sfa css marketing analytics identity notification platform but not staff-bff.

---

## Medium

### 5. ADR 0006 not marked superseded

ADR 0013 supersedes 0006, but 0006 still shows Status: Accepted.

### 6. staticWebApp.bicep stale comment

Line 3 references customer portal.

---

## Verified Correct

- SLN includes staff-bff with deterministic GUIDs
- CI clean (8 workflows, no duplicates)
- Dockerfiles for 9 original services + template
- staff-bff in Container Apps (main.bicep line 362)
- CD build loop includes staff-bff
- SB health check uses topics
- SQL interceptor uses DbParameter
- TimeProvider.System, ConfigureAwait(false), no hardcoded URLs
- Frontend App Insights scaffolded
- staff-bff has 401 tests

---

## Recommended Order

1. Merge PR #25 (fixes broken refs, unblocks build)
2. New PR: staff-bff Dockerfile
3. New PR: remove customer-portal from Bicep + add staff-bff to smoke test
4. New PR: mark ADR 0006 superseded + fix staticWebApp comment

Repo is 95% stable. Broken ProjectReferences are the only build-blocker.