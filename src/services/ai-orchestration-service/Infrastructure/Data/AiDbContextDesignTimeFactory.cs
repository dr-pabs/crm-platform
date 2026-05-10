using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using CrmPlatform.ServiceTemplate.Infrastructure.Data;

namespace CrmPlatform.AiOrchestrationService.Infrastructure.Data;

public sealed class AiDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AiDbContext>
{
    public AiDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AiDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=CrmPlatform;Trusted_Connection=True;TrustServerCertificate=True;",
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "ai"));
        return new AiDbContext(optionsBuilder.Options, new DesignTimeTenantContextAccessor());
    }
}

internal sealed class DesignTimeTenantContextAccessor : ITenantContextAccessor
{
    public Guid TenantId => Guid.Empty;
}
