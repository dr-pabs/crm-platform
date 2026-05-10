# Base Microservice Template — CLAUDE.md

This is the base template for all CRM backend services. Copy this directory to create a new service.
All rules in the root `/CLAUDE.md` apply here plus the following.

## Structure (do not change)
```
src/
  Api/                → Minimal API endpoint mappings
  Application/         → Command handlers (one per use case)
  Domain/
    Entities/          → EF Core entities (must inherit BaseEntity)
    Enums/             → Domain enums
    Events/            → Domain event definitions
  Infrastructure/
    Data/              → DbContext, entity configurations
    Messaging/         → Service Bus consumers
```

## BaseEntity
Every EF Core entity MUST inherit `BaseEntity`:
```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
```

## Health Endpoints
Every service must expose:
- `GET /health/live`  → liveness
- `GET /health/ready` → readiness (checks DB + Service Bus)
- `GET /health/start` → startup probe

## Configuration
Read all config via `IOptions<T>` — never read `IConfiguration` directly in business logic.
Secrets are injected at runtime from Key Vault via Managed Identity — never set in appsettings.json.
