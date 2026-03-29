# Base Microservice Template — CLAUDE.md

This is the base template for all CRM backend services. Copy this directory to create a new service.
All rules in the root `/CLAUDE.md` apply here plus the following.

## Structure (do not change)
```
src/
  Controllers/       → Minimal API endpoint mappings or Controller classes
  Services/          → Business logic (interfaces + implementations)
  Repositories/      → EF Core data access
  Models/
    Entities/        → EF Core entities (must inherit BaseEntity)
    DTOs/            → Request/response objects (never expose entities directly)
    Events/          → Service Bus message contracts
  Infrastructure/
    Data/            → DbContext, migrations, configurations
    Messaging/       → Service Bus publisher/consumer wiring
```

## BaseEntity
Every EF Core entity MUST inherit `BaseEntity`:
```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }          // filtered via HasQueryFilter
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
```

## Health Endpoints
Every service must expose:
- `GET /health/live`  → liveness (is the process running)
- `GET /health/ready` → readiness (can it serve traffic — checks DB + Service Bus)
- `GET /health/start` → startup probe

## Configuration
Read all config via `IOptions<T>` — never read `IConfiguration` directly in business logic.
Secrets are injected at runtime from Key Vault via Managed Identity — never set in appsettings.json.
