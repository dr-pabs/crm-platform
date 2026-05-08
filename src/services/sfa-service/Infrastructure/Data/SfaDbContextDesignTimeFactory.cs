using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using CrmPlatform.ServiceTemplate.Infrastructure.Data;

namespace CrmPlatform.SfaService.Infrastructure.Data;

/// <summary>
/// Allows `dotnet ef migrations add` to instantiate SfaDbContext without
/// the full DI container. Only used at design-time — never at runtime.
/// Run from the service directory: dotnet ef migrations add InitialCreate
/// </summary>
public sealed class SfaDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SfaDbContext>
{
    public SfaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SfaDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=CrmPlatform;Trusted_Connection=True;TrustServerCertificate=True;",
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "sfa"));

        return new SfaDbContext(optionsBuilder.Options, new DesignTimeTenantContextAccessor());
    }
}

/// <summary>
/// Returns Guid.Empty for all design-time operations — query filters are
/// not evaluated during migration generation.
/// </summary>
internal sealed class DesignTimeTenantContextAccessor : ITenantContextAccessor
{
    public Guid TenantId => Guid.Empty;
}
