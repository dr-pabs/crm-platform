using CrmPlatform.ServiceTemplate.Domain;

namespace CrmPlatform.SfaService.Domain.Entities;

public sealed class Account : BaseEntity
{
    private Account() { } // EF Core

    public string   Name           { get; private set; } = string.Empty;
    public string?  Industry       { get; private set; }
    public int?     EmployeeCount  { get; private set; }
    public string?  Phone          { get; private set; }
    public decimal? AnnualRevenue  { get; private set; }
    public string?  BillingAddress { get; private set; }
    public string?  Website        { get; private set; }

    // Navigation
    public IReadOnlyList<Contact>     Contacts      { get; private set; } = [];
    public IReadOnlyList<Opportunity> Opportunities { get; private set; } = [];

    public static Account Create(
        Guid tenantId,
        string name,
        string? industry = null,
        int? employeeCount = null,
        string? phone = null,
        decimal? annualRevenue = null,
        string? billingAddress = null,
        string? website = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Account
        {
            TenantId       = tenantId,
            Name           = name,
            Industry       = industry,
            EmployeeCount  = employeeCount,
            Phone          = phone,
            AnnualRevenue  = annualRevenue,
            BillingAddress = billingAddress,
            Website        = website,
        };
    }

    public void Update(string? name, string? industry, int? employeeCount, string? phone, decimal? annualRevenue, string? billingAddress, string? website)
    {
        if (!string.IsNullOrWhiteSpace(name))     Name           = name;
        if (industry       != null) Industry       = industry;
        if (employeeCount  != null) EmployeeCount  = employeeCount;
        if (phone          != null) Phone          = phone;
        if (annualRevenue  != null) AnnualRevenue  = annualRevenue;
        if (billingAddress != null) BillingAddress = billingAddress;
        if (website        != null) Website        = website;
    }
}
