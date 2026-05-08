# Staff BFF Service

Backend-for-Frontend that aggregates responses from sfa-service, css-service, marketing-service, and analytics-service for the staff portal UI. No database — read-only aggregation over HTTP only.

## Architecture constraints

- **No direct DB access** — this service has no DbContext. All data comes from HTTP calls to downstream services.
- **Token forwarding** — `BearerTokenHandler` forwards the inbound JWT to every downstream call. Never re-issue or cache tokens here.
- **Fan-out in parallel** — `DashboardAggregator` uses `Task.WhenAll` for all four service calls. Do not chain them sequentially.
- **ADR 0005** — This is the only service authorised to make cross-domain aggregation calls within a single HTTP request cycle.

## Test requirements (CLAUDE.md §4)

- Every endpoint must have a `401 Unauthorized` test with no token.
- `DashboardAggregator` must have unit tests using `StubbedHttpHandler` (not mocks of the clients themselves).
- No integration tests against live downstream services in CI — stub handlers only.

## Service clients

Each client in `Infrastructure/ServiceClients/` wraps a named `HttpClient` registered in `Program.cs` with:
- Base address from `ServiceClients:{Name}:BaseAddress` config
- `BearerTokenHandler` message handler
- `AddStandardResilienceHandler()` retry/circuit-breaker

## Internal BFF endpoints in downstream services

Each domain service exposes `GET /internal/bff/summary` (no auth, internal network only) returning its summary DTO. The paths and shapes are:

| Service | Path | Response type |
|---------|------|---------------|
| sfa-service | `/internal/bff/summary` | `SfaBffSummaryResponse` |
| css-service | `/internal/bff/summary` | `CssBffSummaryResponse` |
| marketing-service | `/internal/bff/summary` | `MarketingBffSummaryResponse` |
| analytics-service | `/analytics/dashboard` (existing) | `AnalyticsSummaryDto` (subset) |
