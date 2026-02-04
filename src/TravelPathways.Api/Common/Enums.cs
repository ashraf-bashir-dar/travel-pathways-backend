namespace TravelPathways.Api.Common;

public enum UserRole
{
    SuperAdmin = 0,
    Admin = 1,
    Agent = 2,
    Viewer = 3
}

public enum AppModuleKey
{
    Dashboard = 0,
    Leads = 1,
    Packages = 2,
    Hotels = 3,
    Houseboats = 4,
    Transport = 5
}

public enum TenantDocumentType
{
    Logo = 0,
    Registration = 1,
    PAN = 2,
    GST = 3,
    Other = 4
}

public enum LeadSource
{
    Website = 0,
    Referral = 1,
    SocialMedia = 2,
    DirectCall = 3,
    Email = 4,
    WalkIn = 5,
    Advertisement = 6,
    Other = 7
}

public enum LeadStatus
{
    New = 0,
    Contacted = 1,
    Qualified = 2,
    Converted = 3,
    Lost = 4
}

/// <summary>Status of a single follow-up for a lead.</summary>
public enum FollowUpStatus
{
    InProgress = 0,
    Contacted = 1,
    PlanPostponed = 2,
    PlanCanceled = 3,
    Confirmed = 4,
    NoResponse = 5,
    CallbackScheduled = 6,
    NotInterested = 7
}

public enum AccommodationMealPlan
{
    RoomOnly = 0,
    CP = 1,
    MAP = 2,
    AP = 3
}

public enum VehicleType
{
    Sedan = 0,
    SUV = 1,
    TempoTraveller = 2,
    MiniBus = 3,
    Bus = 4,
    Luxury = 5,
    Other = 6
}

public enum RateType
{
    PerDay = 0,
    PerKm = 1,
    PerTrip = 2,
    Flat = 3
}

public enum PackageStatus
{
    Draft = 0,
    Quoted = 1,
    Confirmed = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5
}

/// <summary>Billing cycle for subscription pricing. All amounts in INR.</summary>
public enum BillingCycle
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    ThreeMonths = 3,
    SixMonths = 4,
    Yearly = 5
}

public enum SubscriptionStatus
{
    Active = 0,
    PastDue = 1,
    Expired = 2,
    Cancelled = 3
}

