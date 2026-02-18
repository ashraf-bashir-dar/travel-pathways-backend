namespace TravelPathways.Api.Common;

public enum UserRole
{
    SuperAdmin = 0,
    Admin = 1,
    Agent = 2,
    Viewer = 3
}

/// <summary>Department / user type for labeling (Sales, HR, Accounts). Does not change permissions; use Role and AllowedModules.</summary>
public enum UserDepartment
{
    General = 0,
    Sales = 1,
    HR = 2,
    Accounts = 3
}

public enum AppModuleKey
{
    Dashboard = 0,
    Leads = 1,
    Packages = 2,
    Hotels = 3,
    Houseboats = 4,
    Transport = 5,
    Master = 6,
    Users = 7,
    Accounts = 8
}

/// <summary>Payment direction: received from client or made to vendor.</summary>
public enum PaymentType
{
    Received = 0,
    Made = 1
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
    Matured = 0,
    NotInterested = 1,
    NoResponse = 2,
    TripCancelled = 3,
    TripConfirmed = 4,
    PackageSent = 5,
    Followup = 6,
    AlreadyBooked = 7,
    New = 8
}

/// <summary>Status of a single follow-up for a lead. Aligned with LeadStatus where applicable.</summary>
public enum FollowUpStatus
{
    Matured = 0,
    NotInterested = 1,
    NoResponse = 2,
    TripCancelled = 3,
    TripConfirmed = 4,
    PackageSent = 5,
    Followup = 6,
    AlreadyBooked = 7,
    New = 8
}

public enum AccommodationMealPlan
{
    RoomOnly = 0,   // EP - Room only
    CP = 1,
    MAP = 2,        // Dinner + Breakfast
    AP = 3,         // Breakfast + Lunch + Dinner
    BreakfastOnly = 4
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

/// <summary>Package status matches lead status (same values). When lead status changes, package status is synced.</summary>
public enum PackageStatus
{
    Matured = 0,
    NotInterested = 1,
    NoResponse = 2,
    TripCancelled = 3,
    TripConfirmed = 4,
    PackageSent = 5,
    Followup = 6,
    AlreadyBooked = 7,
    New = 8
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

