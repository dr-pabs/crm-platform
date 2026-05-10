using CrmPlatform.SfaService.Infrastructure.Data;
using CrmPlatform.ServiceTemplate.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrmPlatform.SfaService.Application.Leads;

public sealed record UpdateLeadCommand(
    Guid     LeadId,
    string?  FirstName,
    string?  LastName,
    string?  JobTitle,
    string?  Email,
    string?  Phone,
    string?  Company);

public sealed class UpdateLeadHandler(SfaDbContext db)
{
    public async Task<Result<bool>> HandleAsync(UpdateLeadCommand cmd, CancellationToken ct = default)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == cmd.LeadId, ct);

        if (lead is null)
            return Result.Fail<bool>("Lead not found.", ResultErrorCode.NotFound);

        lead.Update(cmd.FirstName, cmd.LastName, cmd.JobTitle, cmd.Email, cmd.Phone, cmd.Company);
        await db.SaveChangesAsync(ct);
        return Result.Ok(true);
    }
}
