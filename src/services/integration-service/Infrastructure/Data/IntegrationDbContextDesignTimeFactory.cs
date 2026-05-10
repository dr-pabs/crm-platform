using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using CrmPlatform.ServiceTemplate.Infrastructure.Data;

namespace CrmPlatform.IntegrationService.Infrastructure.Data;

public sealed class IntegrationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IntegrationDbContext>
{
    public IntegrationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IntegrationDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=CrmPlatform;Trusted_Connection=True;TrustServerCertificate=True;",
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "integrations"));
        return new IntegrationDbContext(optionsBuilder.Options, new DesignTimeTenantContextAccessor());
    }
}

internal sealed class DesignTimeTenantContextAccessor : ITenantContextAccessor
{
    public Guid TenantId => Guid.Empty;
}
