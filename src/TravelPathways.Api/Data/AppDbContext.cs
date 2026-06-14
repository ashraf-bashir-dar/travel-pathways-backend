using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;

namespace TravelPathways.Api.Data;

public sealed class AppDbContext : DbContext
{
    private readonly TenantContext _tenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, TenantContext tenant) : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDocument> TenantDocuments => Set<TenantDocument>();
    public DbSet<TenantBankAccount> TenantBankAccounts => Set<TenantBankAccount>();
    public DbSet<TenantQrCode> TenantQrCodes => Set<TenantQrCode>();
    public DbSet<PdfTemplate> PdfTemplates => Set<PdfTemplate>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<CallLog> CallLogs => Set<CallLog>();
    public DbSet<EmployeeLocationLog> EmployeeLocationLogs => Set<EmployeeLocationLog>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanPrice> PlanPrices => Set<PlanPrice>();

    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<LeadFollowUp> LeadFollowUps => Set<LeadFollowUp>();
    public DbSet<TenantLeadIntegration> TenantLeadIntegrations => Set<TenantLeadIntegration>();
    public DbSet<InboundLeadEvent> InboundLeadEvents => Set<InboundLeadEvent>();

    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<AccommodationRate> AccommodationRates => Set<AccommodationRate>();

    public DbSet<TransportCompany> TransportCompanies => Set<TransportCompany>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehiclePricing> VehiclePricing => Set<VehiclePricing>();

    public DbSet<TourPackage> Packages => Set<TourPackage>();
    public DbSet<PackageLog> PackageLogs => Set<PackageLog>();
    public DbSet<DayItinerary> DayItineraries => Set<DayItinerary>();
    public DbSet<ItineraryTemplate> ItineraryTemplates => Set<ItineraryTemplate>();
    public DbSet<PackageInclusionMaster> PackageInclusionMasters => Set<PackageInclusionMaster>();
    public DbSet<PackageLocationMaster> PackageLocationMasters => Set<PackageLocationMaster>();
    public DbSet<B2bAgent> B2bAgents => Set<B2bAgent>();
    public DbSet<B2bAgentDocument> B2bAgentDocuments => Set<B2bAgentDocument>();

    public DbSet<State> States => Set<State>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Area> Areas => Set<Area>();

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationDayCompletion> ReservationDayCompletions => Set<ReservationDayCompletion>();
    public DbSet<ReservationPaymentScreenshot> ReservationPaymentScreenshots => Set<ReservationPaymentScreenshot>();
    public DbSet<ReservationHotelBooking> ReservationHotelBookings => Set<ReservationHotelBooking>();
    public DbSet<ReservationHotelBookingDocument> ReservationHotelBookingDocuments => Set<ReservationHotelBookingDocument>();
    public DbSet<EmployeeDailyTask> EmployeeDailyTasks => Set<EmployeeDailyTask>();
    public DbSet<EmployeeCompensation> EmployeeCompensations => Set<EmployeeCompensation>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Leave> Leaves => Set<Leave>();
    public DbSet<UserActivityDailySummary> UserActivityDailySummaries => Set<UserActivityDailySummary>();
    public DbSet<UserActivityPageVisit> UserActivityPageVisits => Set<UserActivityPageVisit>();
    public DbSet<ExtensionCatalogItem> ExtensionCatalogItems => Set<ExtensionCatalogItem>();

    public DbSet<ChatGroup> ChatGroups => Set<ChatGroup>();
    public DbSet<ChatGroupMember> ChatGroupMembers => Set<ChatGroupMember>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------- Common ----------
        modelBuilder.Entity<Tenant>().HasIndex(t => t.Code).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();

        modelBuilder.Entity<AppUser>()
            .Property(u => u.Role)
            .HasConversion<string>();

        modelBuilder.Entity<AppUser>()
            .Property(u => u.Department)
            .HasConversion<string>();

        modelBuilder.Entity<Lead>()
            .Property(x => x.LeadSource)
            .HasConversion<string>();

        modelBuilder.Entity<Lead>()
            .Property(x => x.Status)
            .HasConversion(new LeadStatusConverter());

        modelBuilder.Entity<Lead>()
            .HasMany<LeadFollowUp>()
            .WithOne(f => f.Lead)
            .HasForeignKey(f => f.LeadId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LeadFollowUp>()
            .Property(f => f.Status)
            .HasConversion(new FollowUpStatusConverter());

        modelBuilder.Entity<Lead>()
            .Property(l => l.InboundProvider)
            .HasConversion<string>();

        modelBuilder.Entity<Lead>()
            .HasIndex(l => new { l.TenantId, l.InboundProvider, l.InboundExternalId })
            .IsUnique()
            .HasFilter("\"InboundExternalId\" IS NOT NULL");

        modelBuilder.Entity<AppUser>()
            .Property(u => u.InboundAllowedLeadSources)
            .HasConversion(new JsonValueConverter<List<LeadSource>>());
        modelBuilder.Entity<AppUser>()
            .Property(u => u.InboundAllowedLeadSources)
            .Metadata.SetValueComparer(new JsonValueComparer<List<LeadSource>, LeadSource>());

        modelBuilder.Entity<TenantLeadIntegration>()
            .HasIndex(i => i.InboundKey)
            .IsUnique();
        modelBuilder.Entity<TenantLeadIntegration>()
            .HasIndex(i => i.TenantId)
            .IsUnique();

        modelBuilder.Entity<InboundLeadEvent>()
            .Property(e => e.Provider)
            .HasConversion<string>();

        modelBuilder.Entity<Hotel>()
            .Property(h => h.Amenities)
            .HasConversion(new JsonValueConverter<List<string>>());
        modelBuilder.Entity<Hotel>()
            .Property(h => h.Amenities)
            .Metadata.SetValueComparer(new JsonValueComparer<List<string>, string>());

        modelBuilder.Entity<Hotel>()
            .Property(h => h.ImageUrls)
            .HasConversion(new JsonValueConverter<List<string>>());
        modelBuilder.Entity<Hotel>()
            .Property(h => h.ImageUrls)
            .Metadata.SetValueComparer(new JsonValueComparer<List<string>, string>());

        modelBuilder.Entity<AccommodationRate>()
            .Property(r => r.MealPlan)
            .HasConversion<string>();

        modelBuilder.Entity<B2bAgent>()
            .ToTable("B2bAgents");

        modelBuilder.Entity<B2bAgent>()
            .Property(x => x.Name)
            .HasMaxLength(200);

        modelBuilder.Entity<B2bAgent>()
            .Property(x => x.ContactPerson)
            .HasMaxLength(200);

        modelBuilder.Entity<B2bAgent>()
            .Property(x => x.ContactNumber1)
            .HasMaxLength(32);

        modelBuilder.Entity<B2bAgent>()
            .Property(x => x.ContactNumber2)
            .HasMaxLength(32);

        modelBuilder.Entity<B2bAgent>()
            .Property(x => x.Email)
            .HasMaxLength(256);

        modelBuilder.Entity<B2bAgent>()
            .Property(x => x.WebsiteUrl)
            .HasMaxLength(500);

        modelBuilder.Entity<B2bAgent>()
            .Property(x => x.State)
            .HasMaxLength(120);

        modelBuilder.Entity<B2bAgent>()
            .Property(x => x.City)
            .HasMaxLength(120);

        modelBuilder.Entity<B2bAgent>()
            .Property(x => x.Country)
            .HasMaxLength(120);

        modelBuilder.Entity<B2bAgent>()
            .Property(x => x.PinCode)
            .HasMaxLength(16);

        modelBuilder.Entity<B2bAgent>()
            .HasIndex(x => new { x.TenantId, x.Name });

        modelBuilder.Entity<B2bAgent>()
            .HasMany(a => a.Documents)
            .WithOne(d => d.B2bAgent)
            .HasForeignKey(d => d.B2bAgentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<B2bAgentDocument>()
            .ToTable("B2bAgentDocuments");

        modelBuilder.Entity<B2bAgentDocument>()
            .Property(d => d.FileName)
            .HasMaxLength(260);

        modelBuilder.Entity<B2bAgentDocument>()
            .Property(d => d.Url)
            .HasMaxLength(1000);

        modelBuilder.Entity<TransportCompany>()
            .HasMany(c => c.Vehicles)
            .WithOne(v => v.TransportCompany)
            .HasForeignKey(v => v.TransportCompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Vehicle>()
            .Property(v => v.VehicleType)
            .HasConversion<string>();

        modelBuilder.Entity<Vehicle>()
            .Property(v => v.Features)
            .HasConversion(new JsonValueConverter<List<string>>());
        modelBuilder.Entity<Vehicle>()
            .Property(v => v.Features)
            .Metadata.SetValueComparer(new JsonValueComparer<List<string>, string>());

        modelBuilder.Entity<VehiclePricing>()
            .Property(p => p.CostPrice)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<VehiclePricing>()
            .Property(p => p.SellingPrice)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<VehiclePricing>()
            .Property(p => p.RateType)
            .HasConversion<string>();

        modelBuilder.Entity<AccommodationRate>()
            .Property(p => p.CostPrice)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<AccommodationRate>()
            .Property(p => p.SellingPrice)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<AccommodationRate>()
            .Property(p => p.ExtraBedCostPrice)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<AccommodationRate>()
            .Property(p => p.ExtraBedSellingPrice)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<AccommodationRate>()
            .Property(p => p.CnbCostPrice)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<AccommodationRate>()
            .Property(p => p.CnbSellingPrice)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<TourPackage>()
            .Property(p => p.Status)
            .HasConversion(new PackageStatusConverter());

        modelBuilder.Entity<TourPackage>()
            .Property(p => p.TotalAmount)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<TourPackage>()
            .Property(p => p.AdvanceAmount)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<TourPackage>()
            .Property(p => p.BalanceAmount)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<TourPackage>()
            .Property(p => p.Discount)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<TourPackage>()
            .Property(p => p.InclusionIds)
            .HasConversion(new JsonValueConverter<List<string>>());
        modelBuilder.Entity<TourPackage>()
            .Property(p => p.InclusionIds)
            .Metadata.SetValueComparer(new JsonValueComparer<List<string>, string>());
        modelBuilder.Entity<TourPackage>()
            .Property(p => p.ExclusionIds)
            .HasConversion(new JsonValueConverter<List<string>>());
        modelBuilder.Entity<TourPackage>()
            .Property(p => p.ExclusionIds)
            .Metadata.SetValueComparer(new JsonValueComparer<List<string>, string>());

        modelBuilder.Entity<PackageLog>()
            .Property(l => l.FinalAmount)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<PackageLog>()
            .Property(l => l.MarginAmount)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<PackageLog>()
            .Property(l => l.Action)
            .HasConversion<string>();
        modelBuilder.Entity<PackageLog>()
            .Property(l => l.Status)
            .HasConversion(new PackageStatusConverter());
        modelBuilder.Entity<PackageLog>()
            .HasOne(l => l.Lead)
            .WithMany()
            .HasForeignKey(l => l.LeadId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PackageLog>()
            .HasOne(l => l.Package)
            .WithMany()
            .HasForeignKey(l => l.PackageId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PackageLog>()
            .HasOne(l => l.ChangedByUser)
            .WithMany()
            .HasForeignKey(l => l.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PackageLog>()
            .HasIndex(l => new { l.LeadId, l.CreatedAt });
        modelBuilder.Entity<PackageLog>()
            .HasIndex(l => new { l.PackageId, l.CreatedAt });

        modelBuilder.Entity<DayItinerary>()
            .Property(d => d.MealPlan)
            .HasConversion<string>();

        modelBuilder.Entity<DayItinerary>()
            .Property(d => d.Activities)
            .HasConversion(new JsonValueConverter<List<string>>());
        modelBuilder.Entity<DayItinerary>()
            .Property(d => d.Activities)
            .Metadata.SetValueComparer(new JsonValueComparer<List<string>, string>());

        modelBuilder.Entity<DayItinerary>()
            .Property(d => d.Meals)
            .HasConversion(new JsonValueConverter<List<string>>());
        modelBuilder.Entity<DayItinerary>()
            .Property(d => d.Meals)
            .Metadata.SetValueComparer(new JsonValueComparer<List<string>, string>());

        modelBuilder.Entity<DayItinerary>()
            .Property(d => d.HotelCost)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<PackageInclusionMaster>()
            .ToTable("PackageInclusionMasters");

        modelBuilder.Entity<PackageInclusionMaster>()
            .Property(x => x.Code)
            .HasMaxLength(64);

        modelBuilder.Entity<PackageInclusionMaster>()
            .Property(x => x.Label)
            .HasMaxLength(500);

        modelBuilder.Entity<PackageInclusionMaster>()
            .HasIndex(x => new { x.TenantId, x.Code })
            .IsUnique();

        modelBuilder.Entity<PackageInclusionMaster>()
            .HasIndex(x => new { x.TenantId, x.SortOrder });

        modelBuilder.Entity<PackageLocationMaster>()
            .ToTable("PackageLocationMasters");

        modelBuilder.Entity<PackageLocationMaster>()
            .Property(x => x.Name)
            .HasMaxLength(200);

        modelBuilder.Entity<PackageLocationMaster>()
            .HasIndex(x => new { x.TenantId, x.Name })
            .IsUnique();

        modelBuilder.Entity<PackageLocationMaster>()
            .HasIndex(x => new { x.TenantId, x.SortOrder });

        modelBuilder.Entity<ItineraryTemplate>()
            .ToTable("DestinationMaster");

        modelBuilder.Entity<Tenant>()
            .Property(t => t.EnabledModules)
            .HasConversion(new JsonValueConverter<List<AppModuleKey>>());
        modelBuilder.Entity<Tenant>()
            .Property(t => t.EnabledModules)
            .Metadata.SetValueComparer(new JsonValueComparer<List<AppModuleKey>, AppModuleKey>());
        modelBuilder.Entity<Tenant>()
            .Property(t => t.TermsAndConditions)
            .HasConversion(new JsonValueConverter<List<string>>());
        modelBuilder.Entity<Tenant>()
            .Property(t => t.TermsAndConditions)
            .Metadata.SetValueComparer(new JsonValueComparer<List<string>, string>());
        modelBuilder.Entity<Tenant>()
            .Property(t => t.CancellationPolicy)
            .HasConversion(new JsonValueConverter<List<string>>());
        modelBuilder.Entity<Tenant>()
            .Property(t => t.CancellationPolicy)
            .Metadata.SetValueComparer(new JsonValueComparer<List<string>, string>());
        modelBuilder.Entity<Tenant>()
            .Property(t => t.SupplementCosts)
            .HasConversion(new JsonValueConverter<List<string>>());
        modelBuilder.Entity<Tenant>()
            .Property(t => t.SupplementCosts)
            .Metadata.SetValueComparer(new JsonValueComparer<List<string>, string>());

        modelBuilder.Entity<Tenant>()
            .HasOne(t => t.DefaultUser)
            .WithMany()
            .HasForeignKey(t => t.DefaultUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Tenant>()
            .HasOne(t => t.Plan)
            .WithMany()
            .HasForeignKey(t => t.PlanId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Tenant>()
            .Property(t => t.BillingCycle)
            .HasConversion<string>();
        modelBuilder.Entity<Tenant>()
            .Property(t => t.SubscriptionStatus)
            .HasConversion(
                v => v.ToString(),
                v => string.IsNullOrWhiteSpace(v) ? SubscriptionStatus.Active : Enum.Parse<SubscriptionStatus>(v));

        modelBuilder.Entity<Plan>()
            .HasMany(p => p.Prices)
            .WithOne(pr => pr.Plan)
            .HasForeignKey(pr => pr.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlanPrice>()
            .Property(p => p.BillingCycle)
            .HasConversion<string>();
        modelBuilder.Entity<PlanPrice>()
            .Property(p => p.BasePriceInr)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<PlanPrice>()
            .Property(p => p.PricePerUserInr)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<PlanPrice>()
            .HasIndex(p => new { p.PlanId, p.BillingCycle })
            .IsUnique();

        modelBuilder.Entity<AppUser>()
            .Property(u => u.AllowedModules)
            .HasConversion(new JsonValueConverter<List<AppModuleKey>>());
        modelBuilder.Entity<AppUser>()
            .Property(u => u.AllowedModules)
            .Metadata.SetValueComparer(new JsonValueComparer<List<AppModuleKey>, AppModuleKey>());

        modelBuilder.Entity<TenantDocument>()
            .Property(d => d.Type)
            .HasConversion<string>();

        modelBuilder.Entity<Tenant>()
            .HasMany(t => t.BankAccounts)
            .WithOne(b => b.Tenant)
            .HasForeignKey(b => b.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Tenant>()
            .HasMany(t => t.QrCodes)
            .WithOne(q => q.Tenant)
            .HasForeignKey(q => q.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PdfTemplate>()
            .Property(t => t.Key)
            .HasMaxLength(120);
        modelBuilder.Entity<PdfTemplate>()
            .Property(t => t.Name)
            .HasMaxLength(180);
        modelBuilder.Entity<PdfTemplate>()
            .HasIndex(t => t.Key)
            .IsUnique();

        modelBuilder.Entity<State>()
            .HasMany(s => s.Cities)
            .WithOne(c => c.State)
            .HasForeignKey(c => c.StateId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<State>().HasIndex(s => s.Name).IsUnique();
        modelBuilder.Entity<City>().HasIndex(c => new { c.StateId, c.Name }).IsUnique();
        modelBuilder.Entity<Area>().HasIndex(a => a.Name).IsUnique();

        modelBuilder.Entity<Hotel>()
            .HasOne(h => h.Area)
            .WithMany()
            .HasForeignKey(h => h.AreaId)
            .IsRequired(false);

        modelBuilder.Entity<Payment>()
            .Property(p => p.PaymentType)
            .HasConversion<string>();
        modelBuilder.Entity<Payment>()
            .Property(p => p.PaymentMode)
            .HasConversion<string>();
        modelBuilder.Entity<Payment>()
            .Property(p => p.PayeeCategory)
            .HasConversion<string>();
        modelBuilder.Entity<Payment>()
            .Property(p => p.EmployeePaymentKind)
            .HasConversion<string>();
        modelBuilder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.RecordedBy)
            .WithMany()
            .HasForeignKey(p => p.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        modelBuilder.Entity<Reservation>()
            .HasMany(r => r.HotelBookings)
            .WithOne(b => b.Reservation)
            .HasForeignKey(b => b.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ReservationHotelBooking>()
            .HasIndex(b => new { b.ReservationId, b.DayNumber });
        modelBuilder.Entity<ReservationHotelBooking>()
            .Property(b => b.Status)
            .HasConversion<string>();
        modelBuilder.Entity<ReservationHotelBooking>()
            .Property(b => b.CancellationReason)
            .HasConversion<string>();
        modelBuilder.Entity<ReservationHotelBooking>()
            .Property(b => b.RatePerNight)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<ReservationHotelBooking>()
            .Property(b => b.ExtraBedRate)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<ReservationHotelBooking>()
            .Property(b => b.CnbRate)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<ReservationHotelBooking>()
            .Property(b => b.TotalAmount)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<ReservationHotelBooking>()
            .Property(b => b.AdvancePaid)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<ReservationHotelBooking>()
            .Property(b => b.BalanceAmount)
            .HasColumnType("decimal(18,2)");
        modelBuilder.Entity<ReservationHotelBookingDocument>()
            .HasOne(d => d.ReservationHotelBooking)
            .WithMany(b => b.Documents)
            .HasForeignKey(d => d.ReservationHotelBookingId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ReservationHotelBookingDocument>()
            .Property(d => d.Type)
            .HasConversion<string>();
        modelBuilder.Entity<ReservationHotelBookingDocument>()
            .Property(d => d.Amount)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<EmployeeDailyTask>()
            .ToTable("Tasks");
        modelBuilder.Entity<EmployeeDailyTask>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EmployeeCompensation>()
            .ToTable("EmployeeSalary");
        modelBuilder.Entity<EmployeeCompensation>()
            .HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EmployeeCompensation>()
            .Property(c => c.Type)
            .HasConversion<string>();
        modelBuilder.Entity<EmployeeCompensation>()
            .Property(c => c.Amount)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<Attendance>()
            .ToTable("Attendance")
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Attendance>()
            .HasIndex(a => new { a.TenantId, a.UserId, a.AttendanceDate })
            .IsUnique();

        modelBuilder.Entity<Leave>()
            .HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserActivityDailySummary>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UserActivityDailySummary>()
            .HasIndex(s => new { s.TenantId, s.UserId, s.ActivityDate })
            .IsUnique();

        modelBuilder.Entity<UserActivityPageVisit>()
            .Property(v => v.Path)
            .HasMaxLength(500);
        modelBuilder.Entity<UserActivityPageVisit>()
            .Property(v => v.Url)
            .HasMaxLength(2000);
        modelBuilder.Entity<UserActivityPageVisit>()
            .Property(v => v.PageTitle)
            .HasMaxLength(500);
        modelBuilder.Entity<UserActivityPageVisit>()
            .Property(v => v.Source)
            .HasMaxLength(32)
            .HasDefaultValue(UserActivityVisitSource.InApp);
        modelBuilder.Entity<UserActivityPageVisit>()
            .HasOne(v => v.User)
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UserActivityPageVisit>()
            .HasIndex(v => new { v.TenantId, v.UserId, v.VisitedAtUtc });

        modelBuilder.Entity<ExtensionCatalogItem>()
            .Property(e => e.Code)
            .HasMaxLength(64);
        modelBuilder.Entity<ExtensionCatalogItem>()
            .Property(e => e.Name)
            .HasMaxLength(200);
        modelBuilder.Entity<ExtensionCatalogItem>()
            .Property(e => e.Summary)
            .HasMaxLength(500);
        modelBuilder.Entity<ExtensionCatalogItem>()
            .Property(e => e.Icon)
            .HasMaxLength(16);
        modelBuilder.Entity<ExtensionCatalogItem>()
            .Property(e => e.SupportedBrowsers)
            .HasMaxLength(120);
        modelBuilder.Entity<ExtensionCatalogItem>()
            .Property(e => e.ChromeStoreUrl)
            .HasMaxLength(1000);
        modelBuilder.Entity<ExtensionCatalogItem>()
            .Property(e => e.EdgeStoreUrl)
            .HasMaxLength(1000);
        modelBuilder.Entity<ExtensionCatalogItem>()
            .Property(e => e.DownloadApiPath)
            .HasMaxLength(256);
        modelBuilder.Entity<ExtensionCatalogItem>()
            .HasIndex(e => new { e.TenantId, e.Code })
            .IsUnique();
        modelBuilder.Entity<ExtensionCatalogItem>()
            .HasIndex(e => new { e.TenantId, e.SortOrder });

        modelBuilder.Entity<ChatGroup>()
            .HasOne(g => g.CreatedByUser)
            .WithMany()
            .HasForeignKey(g => g.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatGroup>()
            .HasIndex(g => new { g.TenantId, g.DirectPairKey })
            .IsUnique()
            .HasFilter("\"IsDirect\" = true AND \"DirectPairKey\" IS NOT NULL");

        modelBuilder.Entity<ChatGroupMember>()
            .HasKey(m => new { m.GroupId, m.UserId });

        modelBuilder.Entity<ChatGroupMember>()
            .HasOne(m => m.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatGroupMember>()
            .HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Group)
            .WithMany(g => g.Messages)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.SenderUser)
            .WithMany()
            .HasForeignKey(m => m.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(m => new { m.GroupId, m.SentAtUtc });

        modelBuilder.Entity<ChatMessage>()
            .Property(m => m.MentionedUserIds)
            .HasConversion(new JsonValueConverter<List<Guid>>());

        modelBuilder.Entity<ChatMessage>()
            .Property(m => m.ImageUrls)
            .HasConversion(new JsonValueConverter<List<string>>());

        modelBuilder.Entity<CallLog>()
            .ToTable("CallLogs");

        modelBuilder.Entity<CallLog>()
            .Property(l => l.Direction)
            .HasMaxLength(16);

        modelBuilder.Entity<CallLog>()
            .Property(l => l.Provider)
            .HasMaxLength(64);

        modelBuilder.Entity<CallLog>()
            .Property(l => l.ProviderCallId)
            .HasMaxLength(128);

        modelBuilder.Entity<CallLog>()
            .Property(l => l.FromNumber)
            .HasMaxLength(48);

        modelBuilder.Entity<CallLog>()
            .Property(l => l.ToNumber)
            .HasMaxLength(48);

        modelBuilder.Entity<CallLog>()
            .Property(l => l.Status)
            .HasMaxLength(64);

        modelBuilder.Entity<CallLog>()
            .HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CallLog>()
            .HasIndex(l => new { l.TenantId, l.CreatedAt });

        modelBuilder.Entity<CallLog>()
            .HasIndex(l => new { l.UserId, l.CreatedAt });

        modelBuilder.Entity<EmployeeLocationLog>()
            .ToTable("EmployeeLocationLogs");

        modelBuilder.Entity<EmployeeLocationLog>()
            .Property(l => l.Provider)
            .HasMaxLength(64);

        modelBuilder.Entity<EmployeeLocationLog>()
            .Property(l => l.ProviderPointId)
            .HasMaxLength(128);

        modelBuilder.Entity<EmployeeLocationLog>()
            .HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EmployeeLocationLog>()
            .HasIndex(l => new { l.TenantId, l.RecordedAtUtc });

        modelBuilder.Entity<EmployeeLocationLog>()
            .HasIndex(l => new { l.UserId, l.RecordedAtUtc });

        modelBuilder.Entity<EmployeeLocationLog>()
            .HasIndex(l => new { l.TenantId, l.Provider, l.ProviderPointId });

        // ---------- Multi-tenancy and soft-delete query filters ----------
        ConfigureTenantFilters(modelBuilder);
    }

    private void ConfigureTenantFilters(ModelBuilder modelBuilder)
    {
        // Note: EF will parameterize these values per DbContext instance.
        // Soft delete: exclude IsDeleted everywhere.
        modelBuilder.Entity<Tenant>().HasQueryFilter(t => !t.IsDeleted);
        modelBuilder.Entity<AppUser>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        // When Super Admin has NOT selected a tenant (no X-Tenant-Id), they can see data for all tenants.
        // When Super Admin HAS selected a tenant (X-Tenant-Id is set), they should only see that tenant's data.
        // Tenant users are always restricted to their own tenant id.
        modelBuilder.Entity<Lead>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<LeadFollowUp>().HasQueryFilter(f => _tenant.IsSuperAdmin || Set<Lead>().Any(l => l.Id == f.LeadId));

        modelBuilder.Entity<TenantLeadIntegration>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<InboundLeadEvent>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<Hotel>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<AccommodationRate>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<B2bAgent>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<TransportCompany>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<Vehicle>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<VehiclePricing>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<TourPackage>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<PackageLog>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<DayItinerary>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<ItineraryTemplate>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<PackageInclusionMaster>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<PackageLocationMaster>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<Payment>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Package)
            .WithMany()
            .HasForeignKey(r => r.PackageId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.AssignedToUser)
            .WithMany()
            .HasForeignKey(r => r.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.AssignedByUser)
            .WithMany()
            .HasForeignKey(r => r.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Reservation>()
            .Property(r => r.Status)
            .HasConversion<string>();
        modelBuilder.Entity<Reservation>()
            .HasMany(r => r.DayCompletions)
            .WithOne(d => d.Reservation)
            .HasForeignKey(d => d.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Reservation>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<ReservationDayCompletion>().HasQueryFilter(e => !e.IsDeleted);

        modelBuilder.Entity<ReservationPaymentScreenshot>()
            .HasOne(s => s.Reservation)
            .WithMany(r => r.PaymentScreenshots)
            .HasForeignKey(s => s.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TenantDocument>().HasQueryFilter(d =>
            !d.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || d.TenantId == _tenant.TenantId)
                : d.TenantId == _tenant.TenantId));

        modelBuilder.Entity<TenantQrCode>().HasQueryFilter(q =>
            !q.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || q.TenantId == _tenant.TenantId)
                : q.TenantId == _tenant.TenantId));

        modelBuilder.Entity<TenantBankAccount>().HasQueryFilter(b =>
            !b.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || b.TenantId == _tenant.TenantId)
                : b.TenantId == _tenant.TenantId));

        modelBuilder.Entity<EmployeeDailyTask>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<EmployeeCompensation>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<Attendance>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<Leave>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<UserActivityDailySummary>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<UserActivityPageVisit>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<ExtensionCatalogItem>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<Plan>().HasQueryFilter(p => !p.IsDeleted);
        modelBuilder.Entity<PdfTemplate>().HasQueryFilter(t => !t.IsDeleted);

        modelBuilder.Entity<CallLog>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<EmployeeLocationLog>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || e.TenantId == _tenant.TenantId)
                : e.TenantId == _tenant.TenantId));

        modelBuilder.Entity<ChatGroup>().HasQueryFilter(g =>
            !g.IsDeleted &&
            (_tenant.IsSuperAdmin
                ? (!_tenant.TenantId.HasValue || g.TenantId == _tenant.TenantId)
                : g.TenantId == _tenant.TenantId));

        modelBuilder.Entity<ChatGroupMember>().HasQueryFilter(m =>
            Set<ChatGroup>().Any(g => g.Id == m.GroupId));

        modelBuilder.Entity<ChatMessage>().HasQueryFilter(m =>
            !m.IsDeleted &&
            Set<ChatGroup>().Any(g => g.Id == m.GroupId));
    }

    public override int SaveChanges()
    {
        ApplyTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTimestamps()
    {
        NormalizeDateTimesToUtc();

        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }

    /// <summary>
    /// PostgreSQL timestamp with time zone requires UTC/Local DateTime kinds.
    /// Normalize all pending DateTime values before persisting to avoid Kind=Unspecified failures.
    /// </summary>
    private void NormalizeDateTimesToUtc()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;

            foreach (var property in entry.Properties)
            {
                var clrType = property.Metadata.ClrType;
                var isDateTimeProp = clrType == typeof(DateTime) || Nullable.GetUnderlyingType(clrType) == typeof(DateTime);
                if (!isDateTimeProp)
                    continue;

                if (property.CurrentValue is not DateTime dateTime)
                    continue;

                property.CurrentValue = dateTime.Kind switch
                {
                    DateTimeKind.Utc => dateTime,
                    DateTimeKind.Local => dateTime.ToUniversalTime(),
                    _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
                };
            }
        }
    }
}

