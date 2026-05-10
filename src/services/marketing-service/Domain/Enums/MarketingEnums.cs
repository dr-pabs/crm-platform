namespace CrmPlatform.MarketingService.Domain.Enums;

/// <summary>Campaign lifecycle status.</summary>
public enum CampaignStatus
{
    Draft     = 0,
    Scheduled = 1,
    Active    = 2,
    Paused    = 3,
    Completed = 4,
    Cancelled = 5
}

/// <summary>Channel via which a campaign sends communications.</summary>
public enum CampaignChannel
{
    Email  = 0,
    Sms    = 1,
    InApp  = 2,
    Push   = 3
}

/// <summary>Status of a contact's participation in a journey.</summary>
public enum EnrollmentStatus
{
    Active    = 0,
    Completed = 1,
    Exited    = 2,  // explicit opt-out or disqualification
    Failed    = 3   // unrecoverable processing error
}


/// <summary>Journey lifecycle status. Published journeys are frozen.</summary>
public enum JourneyStatus
{
    Draft     = 0,
    Active    = 1,
    Paused    = 2,
    Completed = 3,
    Archived  = 4
}
/// <summary>Email template rendering engine.</summary>
public enum TemplateEngine
{
    Handlebars = 0,
    Razor      = 1
}
