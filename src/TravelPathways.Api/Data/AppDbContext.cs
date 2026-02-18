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
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanPrice> PlanPrices => Set<PlanPrice>();

    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<LeadFollowUp> LeadFollowUps => Set<LeadFollowUp>();

    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<AccommodationRate> AccommodationRates => Set<AccommodationRate>();

    public DbSet<TransportCompany> TransportCompanies => Set<TransportCompany>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehiclePricing> VehiclePricing => Set<VehiclePricing>();

    public DbSet<TourPackage> Packages => Set<TourPackage>();
    public DbSet<DayItinerary> DayItineraries => Set<DayItinerary>();
    public DbSet<ItineraryTemplate> ItineraryTemplates => Set<ItineraryTemplate>();

    public DbSet<State> States => Set<State>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Area> Areas => Set<Area>();

    public DbSet<Payment> Payments => Set<Payment>();

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

        modelBuilder.Entity<ItineraryTemplate>()
            .ToTable("DestinationMaster");

        modelBuilder.Entity<Tenant>()
            .Property(t => t.EnabledModules)
            .HasConversion(new JsonValueConverter<List<AppModuleKey>>());
        modelBuilder.Entity<Tenant>()
            .Property(t => t.EnabledModules)
            .Metadata.SetValueComparer(new JsonValueComparer<List<AppModuleKey>, AppModuleKey>());

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
            .Property(p => p.Amount)
            .HasColumnType("decimal(18,2)");

        // ---------- Multi-tenancy query filters ----------
        ConfigureTenantFilters(modelBuilder);
    }

    private void ConfigureTenantFilters(ModelBuilder modelBuilder)
    {
        // Note: EF will parameterize these values per DbContext instance.
        modelBuilder.Entity<Lead>().HasQueryFilter(e => _tenant.IsSuperAdmin || e.TenantId == _tenant.TenantId);
        modelBuilder.Entity<LeadFollowUp>().HasQueryFilter(f => _tenant.IsSuperAdmin || Set<Lead>().Any(l => l.Id == f.LeadId));
        modelBuilder.Entity<Hotel>().HasQueryFilter(e => _tenant.IsSuperAdmin || e.TenantId == _tenant.TenantId);
        modelBuilder.Entity<AccommodationRate>().HasQueryFilter(e => _tenant.IsSuperAdmin || e.TenantId == _tenant.TenantId);
        modelBuilder.Entity<TransportCompany>().HasQueryFilter(e => _tenant.IsSuperAdmin || e.TenantId == _tenant.TenantId);
        modelBuilder.Entity<Vehicle>().HasQueryFilter(e => _tenant.IsSuperAdmin || e.TenantId == _tenant.TenantId);
        modelBuilder.Entity<VehiclePricing>().HasQueryFilter(e => _tenant.IsSuperAdmin || e.TenantId == _tenant.TenantId);
        modelBuilder.Entity<TourPackage>().HasQueryFilter(e => _tenant.IsSuperAdmin || e.TenantId == _tenant.TenantId);
        modelBuilder.Entity<DayItinerary>().HasQueryFilter(e => _tenant.IsSuperAdmin || e.TenantId == _tenant.TenantId);
        modelBuilder.Entity<ItineraryTemplate>().HasQueryFilter(e => _tenant.IsSuperAdmin || e.TenantId == _tenant.TenantId);
        modelBuilder.Entity<Payment>().HasQueryFilter(e => _tenant.IsSuperAdmin || e.TenantId == _tenant.TenantId);
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
}

