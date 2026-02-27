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
    Accounts = 8,
    Pricing = 9,
    /// <summary>Deprecated: use EmployeeManagement. Kept for backward compatibility in tenant/user EnabledModules.</summary>
    EmployeeMonitoring = 10,
    /// <summary>Vendor payables reports (hotels, houseboats, transport). Super Admin can enable/disable per tenant.</summary>
    Reports = 11,
    /// <summary>Employee management: daily tasks, compensation, employee packages, attendance, salary, details. Super Admin can enable/disable per tenant.</summary>
    EmployeeManagement = 12,
    /// <summary>Vendor management: vendor summary, hotel/houseboat/transport payables, and everything related to vendors. Super Admin can enable/disable per tenant.</summary>
    VendorManagement = 13,
    /// <summary>Bank & Payment: bank accounts and QR codes for package PDFs. Default for every tenant; tenant admin assigns to users.</summary>
    BankAndPayment = 14,
    /// <summary>TimeSheet only: daily tasks (add what you did for the day). Can be assigned without full Employee Management.</summary>
    TimeSheet = 15
}

/// <summary>Payment direction: received from client or made to vendor/employee/other.</summary>
public enum PaymentType
{
    Received = 0,
    Made = 1
}

/// <summary>When PaymentType is Made: who was paid. Determines which FK or PayeeDescription is required.</summary>
public enum PaymentPayeeCategory
{
    /// <summary>Hotel (not houseboat). Use HotelId.</summary>
    VendorHotel = 0,
    /// <summary>Houseboat. Use HotelId.</summary>
    VendorHouseboat = 1,
    /// <summary>Transport company. Use TransportCompanyId.</summary>
    VendorTransport = 2,
    /// <summary>Employee. Use UserId.</summary>
    Employee = 3,
    /// <summary>Driver (e.g. freelance). Use PayeeDescription or TransportCompanyId if on roster.</summary>
    Driver = 4,
    /// <summary>Office or other expenditure. Use PayeeDescription.</summary>
    OfficeOther = 5
}

/// <summary>When PayeeCategory is Employee: kind of payment (salary, incentive, bonus).</summary>
public enum EmployeePaymentType
{
    Salary = 0,
    Incentive = 1,
    Bonus = 2
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

/// <summary>Type of compensation paid to an employee.</summary>
public enum CompensationType
{
    Salary = 0,
    Incentive = 1,
    Bonus = 2
}

/// <summary>Type of leave applied by an employee.</summary>
public enum LeaveType
{
    CasualLeave = 0,
    SickLeave = 1
}

/// <summary>Status of a leave request.</summary>
public enum LeaveStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

