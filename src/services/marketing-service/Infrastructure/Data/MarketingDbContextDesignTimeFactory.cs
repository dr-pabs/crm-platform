using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using CrmPlatform.ServiceTemplate.Infrastructure.Data;

namespace CrmPlatform.MarketingService.Infrastructure.Data;

public sealed class MarketingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<MarketingDbContext>
{
    public MarketingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MarketingDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=CrmPlatform;Trusted_Connection=True;TrustServerCertificate=True;",
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "marketing"));
        return new MarketingDbContext(optionsBuilder.Options, new DesignTimeTenantContextAccessor());
    }
}

internal sealed class DesignTimeTenantContextAccessor : ITenantContextAccessor
{
    public Guid TenantId => Guid.Empty;
}
