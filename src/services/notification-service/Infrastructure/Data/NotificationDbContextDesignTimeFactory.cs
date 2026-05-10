using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using CrmPlatform.ServiceTemplate.Infrastructure.Data;

namespace CrmPlatform.NotificationService.Infrastructure.Data;

public sealed class NotificationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NotificationDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=CrmPlatform;Trusted_Connection=True;TrustServerCertificate=True;",
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications"));
        return new NotificationDbContext(optionsBuilder.Options, new DesignTimeTenantContextAccessor());
    }
}

internal sealed class DesignTimeTenantContextAccessor : ITenantContextAccessor
{
    public Guid TenantId => Guid.Empty;
}
