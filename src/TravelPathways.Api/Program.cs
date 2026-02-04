using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;
using TravelPathways.Api.Auth;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;
using TravelPathways.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
      o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
      o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
      o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
  c.SwaggerDoc("v1", new OpenApiInfo { Title = "TravelPathways API", Version = "v1" });
  c.CustomSchemaIds(type => type.FullName?.Replace("+", ".")); // avoid duplicate schema IDs
  c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First()); // avoid duplicate operation IDs
  c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
  {
    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
    Name = "Authorization",
    In = ParameterLocation.Header,
    Type = SecuritySchemeType.Http,
    Scheme = "bearer",
    BearerFormat = "JWT"
  });
  c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
  options.AddPolicy("frontend", policy =>
  {
    policy
          .WithOrigins("http://localhost:4200", "https://localhost:4200", "http://localhost:4201", "https://localhost:4201")
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials();
  });
});

builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<TenantMiddleware>();
builder.Services.AddScoped<FileStorage>();
builder.Services.AddScoped<TravelPathways.Api.Services.IEmailService, TravelPathways.Api.Services.EmailService>();

builder.Services.AddDbContext<AppDbContext>(opt =>
{
  opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<TokenService>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
      options.TokenValidationParameters = new TokenValidationParameters
      {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        ClockSkew = TimeSpan.FromMinutes(2)
      };
    });

builder.Services.AddAuthorization(options =>
{
  options.AddPolicy("SuperAdminOnly", policy => policy.RequireRole(UserRole.SuperAdmin.ToString()));
});

var app = builder.Build();

// Auto-create DB + SuperAdmin for dev/demo
using (var scope = app.Services.CreateScope())
{
  var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
  try
  {
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // Ensure LeadFollowUps table exists (fallback if migration was not applied)
    await EnsureLeadFollowUpsTableAsync(db, logger);

    var superEmail = app.Configuration["SuperAdmin:Email"] ?? "super@travelpathways.local";
    var superPassword = app.Configuration["SuperAdmin:Password"] ?? "Super@123";

    var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == superEmail);
    if (existing is null)
    {
      db.Users.Add(new AppUser
      {
        TenantId = null,
        Email = superEmail,
        FirstName = "Super",
        LastName = "Admin",
        Role = UserRole.SuperAdmin,
        IsActive = true,
        PasswordHash = PasswordHasher.Hash(superPassword)
      });
      await db.SaveChangesAsync();
    }

    // Seed a default tenant so Super Admin always has at least one tenant for X-Tenant-Id
    if (!await db.Tenants.AnyAsync())
    {
      db.Tenants.Add(new Tenant
      {
        Name = "Default Agency",
        Code = "DEFAULT",
        Email = "admin@default.local",
        Phone = "0000000000",
        Address = "Default Address",
        ContactPerson = "Admin",
        EnabledModules = Enum.GetValues<AppModuleKey>().ToList(),
        IsActive = true
      });
      await db.SaveChangesAsync();
    }

    await SeedStateCity.SeedAsync(db);
    await SeedAreas.SeedAsync(db);
  }
  catch (Exception ex)
  {
    // Don’t crash the API if SQL Server isn’t configured yet.
    logger.LogError(ex, "Database migration/seed failed. Configure ConnectionStrings:DefaultConnection and restart.");
  }
}

static async Task EnsureLeadFollowUpsTableAsync(AppDbContext db, ILogger logger)
{
  try
  {
    const string sql = """
            IF OBJECT_ID(N'dbo.LeadFollowUps', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[LeadFollowUps] (
                    [Id] uniqueidentifier NOT NULL,
                    [LeadId] uniqueidentifier NOT NULL,
                    [FollowUpDate] datetime2 NOT NULL,
                    [Status] nvarchar(max) NOT NULL,
                    [Notes] nvarchar(max) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] nvarchar(max) NULL,
                    CONSTRAINT [PK_LeadFollowUps] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_LeadFollowUps_Leads_LeadId] FOREIGN KEY ([LeadId]) REFERENCES [Leads] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_LeadFollowUps_LeadId] ON [dbo].[LeadFollowUps] ([LeadId]);
            END
            """;
    await db.Database.ExecuteSqlRawAsync(sql);
  }
  catch (Exception ex)
  {
    logger.LogWarning(ex, "Could not ensure LeadFollowUps table. Lead follow-ups may fail until migration is applied.");
  }
}

//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI(c =>
{
  // Absolute path so the definition loads at https://localhost:44396/swagger/index.html (IIS Express) or any port
  c.SwaggerEndpoint("/swagger/v1/swagger.json", "TravelPathways API v1");
  c.RoutePrefix = "swagger";
});
//}

// In development, skip HTTPS redirection so proxy (HTTP) requests keep the Authorization header
if (!app.Environment.IsDevelopment())
{
  app.UseHttpsRedirection();
}

var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");
if (!Directory.Exists(uploadsPath))
{
  Directory.CreateDirectory(uploadsPath);
}


app.UseCors("frontend");

app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();

app.UseStaticFiles();

app.MapControllers();

app.Run();
