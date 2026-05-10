using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using CrmPlatform.ServiceTemplate.Infrastructure.Data;

namespace CrmPlatform.CssService.Infrastructure.Data;

public sealed class CssDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CssDbContext>
{
    public CssDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CssDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=CrmPlatform;Trusted_Connection=True;TrustServerCertificate=True;",
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "css"));
        return new CssDbContext(optionsBuilder.Options, new DesignTimeTenantContextAccessor());
    }
}

internal sealed class DesignTimeTenantContextAccessor : ITenantContextAccessor
{
    public Guid TenantId => Guid.Empty;
}
