using CrmPlatform.SfaService.Domain.Entities;
using CrmPlatform.SfaService.Infrastructure.Data;
using CrmPlatform.ServiceTemplate.Application;
using CrmPlatform.ServiceTemplate.Infrastructure.MultiTenancy;

namespace CrmPlatform.SfaService.Application.Accounts;

public sealed record CreateAccountCommand(
    string      Name,
    string?     Industry,
    int?        EmployeeCount,
    string?     Phone,
    decimal?    AnnualRevenue,
    string?     BillingAddress,
    string?     Website);

public sealed class CreateAccountHandler(
    SfaDbContext db,
    ITenantContext tenantContext)
{
    public async Task<Result<Guid>> HandleAsync(
        CreateAccountCommand cmd, CancellationToken ct = default)
    {
        var account = Account.Create(
            tenantContext.TenantId,
            cmd.Name,
            cmd.Industry,
            cmd.EmployeeCount,
            cmd.Phone,
            cmd.AnnualRevenue,
            cmd.BillingAddress,
            cmd.Website);

        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return Result.Ok(account.Id);
    }
}
