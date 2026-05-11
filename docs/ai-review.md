# CRM Platform — AI Architecture Review

**Date:** 2026-05-10

---

## Current State: 9 Capabilities, Well-Architected

The AI orchestration service is solid. Not a stub — full implementation.

### Architecture

```
Service Bus Events (crm.sfa, crm.css, crm.marketing, crm.platform)
        │
        ▼
┌─────────────────────────────────┐
│  Event Consumers (8 consumers)  │
│  LeadCreated, LeadAssigned,     │
│  OpportunityStageChanged,       │
│  CaseResolved, CaseCommentAdded,│
│  JourneyEnrollmentCreated,      │
│  TenantProvisioned, Suspended   │
└────────────┬────────────────────┘
             │ AiJob.Create()
             ▼
┌─────────────────────────────────┐
│  AiJobWorker (BackgroundService)│
│  Polls every 5s, batch of 20    │
│  Max 3 attempts, 30s retry      │
│  1-hour stale TTL               │
└────────────┬────────────────────┘
             │ Claude model
             ▼
┌─────────────────────────────────┐
│  ClaudeClient (Azure AI Infer.) │
│  Handlebars templating          │
│  Prompt: tenant custom → default│
└────────────┬────────────────────┘
             │ output
             ▼
┌─────────────────────────────────┐
│  Publish result events (crm.ai) │
│  LeadScoredEvent → SFA service  │
│  CaseSummarisedEvent → CSS      │
│  SentimentAnalysedEvent → CSS   │
│  NextBestActionEvent → SFA      │
│  JourneyPersonalisedEvent → Mkt │
└─────────────────────────────────┘
```

### What works

| Capability | Trigger | Pattern | Model output |
|---|---|---|---|
| Lead Scoring | SB lead.created/assigned | Async AiJob | Score 0-100 + rationale |
| Email Draft | POST /ai/email-draft | Sync | Email text |
| Case Summarisation | SB case.resolved + POST | Async/Sync | 2-3 sentence summary |
| Sentiment Analysis | SB case.comment.added | Async | Positive/Neutral/Negative/Mixed |
| Next Best Action | SB lead.assigned/opp.stage.changed | Async | Action + rationale |
| Journey Personalisation | SB journey.enrollment.created | Async | Branch recommendation |
| SMS Composition | POST /ai/sms | Async | SMS text (ACS delivery) |
| Teams Card | POST /ai/teams-notification | Sync | Adaptive Card body |
| Teams Call | POST /ai/teams-call | Sync | ACS outbound call |

### M365 Copilot Integration

- Plugin manifest exists at `wwwroot/.well-known/ai-plugin.json`
- OpenAPI spec route defined at `/ai/openapi.json` but no file found
- Endpoints annotated with `.WithName()/.WithSummary()/.Produces()`

---

## Gaps and Recommendations

### High — Missing or incomplete

**1. Copilot plugin has no OpenAPI spec file**

The route `/ai/openapi.json` exists in Program.cs but no `wwwroot/ai-openapi.json` was found. The plugin manifest references endpoints that can't be discovered.

**Fix:** Generate the OpenAPI spec via Swashbuckle/Swagger at build time, or create `ai-openapi.json` with the 3 Copilot-relevant endpoints (email draft, case summarisation, Teams notification).

**2. AzureKeyCredential instead of Managed Identity**

`ClaudeClient` uses `AzureKeyCredential` with `Azure:AI:ApiKey` from config. This violates the CLAUDE.md rule: "No stored credentials — Key Vault only, accessed via Managed Identity."

**Fix:** Switch to `DefaultAzureCredential` for the Azure AI Inference client.

**3. No AI evaluation framework**

No way to A/B test prompts, measure accuracy, or track model performance over time. Every prompt change is deployed blind.

**Fix:** Add an evaluation pipeline:
- Store ground-truth labels on AiResults (was this output correct? used? edited?)
- Add an `/ai/eval` endpoint that runs a test suite of prompts against known inputs
- Track metrics: acceptance rate, edit distance, sentiment accuracy vs human labels

### Medium — Feature gaps

**4. No RAG / knowledge base**

CRM users frequently ask "how do I handle this type of case?" or "what's the SLA policy for this?" — this requires retrieval-augmented generation over CRM documentation, playbooks, and past resolutions.

**Recommendation:** Add `CapabilityType.KnowledgeQuery` (enum value 10). Index CRM documents (playbooks, runbooks, ADRs) into Azure AI Search. Use the existing ClaudeClient with retrieved context injected into the prompt.

**5. No pipeline forecasting**

Opportunity data (stage, value, age) is a textbook ML use case. Predicting quarterly revenue and win probability would add direct business value.

**Recommendation:** Add `CapabilityType.PipelineForecasting` (enum value 11). Collect historical opportunity snapshots. Use Claude with structured pipeline data to predict: win probability per opportunity, quarterly forecast with confidence bands.

**6. No churn prediction**

Account and contact engagement signals (case frequency, sentiment trends, login recency) can predict churn risk.

**Recommendation:** Add `CapabilityType.ChurnPrediction` (enum value 12). Triggered by scheduled function or tenant health check. Analyses engagement patterns and flags at-risk accounts.

### Low — Hardcoded limitations

**7. Hardcoded model name**

```csharp
var modelName = config[$"Azure:AI:Models:{capabilityType}"]
             ?? config["Azure:AI:Models:Default"]
             ?? "claude-3-7-sonnet-20250219";
```

Model names are hardcoded and version-specific. Claude model IDs change quarterly.

**Fix:** Move model IDs to `appsettings.json` per environment, or use Azure AI Foundry model deployment names (which abstract the underlying model version).

**8. Handlebars — no escaping safety**

```csharp
var hbsTemplate = Handlebars.Compile(userTemplate);
var userMessage  = hbsTemplate(templateVars);
```

Tenant-provided templates could contain malicious Handlebars expressions that leak data or cause injection.

**Fix:** Register a Handlebars `IHandlebars` instance with HTML escaping enabled and no helper access to sensitive data.

**9. No streaming responses**

Sync endpoints (`/ai/email-draft`) return the full response after completion. For Teams cards and email drafts, streaming would improve UX.

**Fix:** Add SSE (Server-Sent Events) streaming endpoint for email draft and Teams card generation.

**10. Copilot plugin limited to 3 endpoints**

Only email draft, case summarisation, and Teams notification are exposed. Lead scoring, sentiment analysis, next-best-action, and knowledge queries would be valuable Copilot skills.

**Recommendation:** Add all 12 capabilities to the Copilot plugin manifest with proper scoping.

---

## Recommended Priority

1. **Switch to Managed Identity** — security fix, quick win
2. **Generate OpenAPI spec** — unblocks Copilot plugin
3. **Add pipeline forecasting** — highest business value
4. **Add RAG/knowledge base** — enables self-service AI
5. **Add eval framework** — quality before more features
6. **Streaming responses** — UX improvement
7. **Churn prediction, Handlebars safety, model versioning** — ongoing improvements
