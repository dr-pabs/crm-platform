namespace CrmPlatform.SfaService.Domain.Enums;

public enum LeadStatus
{
    New,
    Contacted,
    Qualified,
    Converted,
    Disqualified,
}

public enum OpportunityStage
{
    Prospecting   = 1,
    Qualification = 2,
    Proposal      = 3,
    Negotiation   = 4,
    ClosedWon     = 5,
    ClosedLost    = 6,
}

public enum QuoteStatus
{
    Draft,
    Sent,
    Accepted,
    Rejected,
}

public enum ActivityType
{
    Call,
    Email,
    Meeting,
    Note,
}

public enum LeadSource
{
    Web,
    Referral,
    Campaign,
    InboundCall,
    Partner,
    Other,
}

public enum AccountSize
{
    Micro,     // 1-9
    Small,     // 10-49
    Medium,    // 50-249
    Large,     // 250-999
    Enterprise, // 1000+
}
