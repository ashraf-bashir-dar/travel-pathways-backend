using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
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

/* -------------------- Controllers & JSON -------------------- */
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
      o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
      o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
      o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

/* -------------------- Swagger -------------------- */
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
  c.SwaggerDoc("v1", new OpenApiInfo
  {
    Title = "TravelPathways API",
    Version = "v1"
  });

  c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
  c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

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
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

/* -------------------- CORS -------------------- */
var corsOriginsConfig = builder.Configuration["Cors:AllowedOrigins"] ?? builder.Configuration["Cors__AllowedOrigins"];
var parsed = string.IsNullOrWhiteSpace(corsOriginsConfig)
  ? Array.Empty<string>()
  : corsOriginsConfig.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
var allowedOrigins = parsed.Length > 0
  ? parsed
  : new[] { "https://wonderful-grass-09269371e.1.azurestaticapps.net", "http://localhost:4200" };

builder.Services.AddCors(options =>
{
  options.AddPolicy("frontend", policy =>
  {
    policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders("Content-Disposition"); // so frontend can read PDF download filename
  });
});


/* -------------------- Dependency Injection -------------------- */
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<TenantMiddleware>();
builder.Services.AddScoped<FileStorage>();
builder.Services.AddScoped<TravelPathways.Api.Services.IPackagePdfGenerator, TravelPathways.Api.Services.PackagePdfGenerator>();
builder.Services.AddScoped<TravelPathways.Api.Services.IEmailService,
                          TravelPathways.Api.Services.EmailService>();

/* -------------------- Database -------------------- */
builder.Services.AddDbContext<AppDbContext>(options =>
{
  options.UseSqlServer(
    builder.Configuration.GetConnectionString("DefaultConnection"),
    sql => sql.EnableRetryOnFailure(
      maxRetryCount: 5,
      maxRetryDelay: TimeSpan.FromSeconds(30),
      errorNumbersToAdd: null));
});

/* -------------------- JWT Authentication -------------------- */
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
        IssuerSigningKey = new SymmetricSecurityKey(
              Encoding.UTF8.GetBytes(jwt.SigningKey)
          ),
        ClockSkew = TimeSpan.FromMinutes(2)
      };
    });

builder.Services.AddAuthorization(options =>
{
  options.AddPolicy("SuperAdminOnly",
      policy => policy.RequireRole(UserRole.SuperAdmin.ToString()));
});

var app = builder.Build();

/* -------------------- Forwarded headers (so Request.Scheme/Host are correct behind Azure proxy for PDF image URLs) -------------------- */
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
  ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
  KnownNetworks = { },
  KnownProxies = { }
});

/* -------------------- CORS: handle preflight and add headers to all responses -------------------- */
const string allowedOriginStatic = "https://wonderful-grass-09269371e.1.azurestaticapps.net";
app.Use(async (context, next) =>
{
  var origin = context.Request.Headers["Origin"].FirstOrDefault() ?? "";
  var originAllowed = !string.IsNullOrEmpty(origin) && (
    allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase) ||
    string.Equals(origin, allowedOriginStatic, StringComparison.OrdinalIgnoreCase));

  if (context.Request.Method == "OPTIONS")
  {
    if (originAllowed)
    {
      context.Response.Headers["Access-Control-Allow-Origin"] = origin;
      context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, PATCH, OPTIONS";
      context.Response.Headers["Access-Control-Allow-Headers"] = context.Request.Headers["Access-Control-Request-Headers"].FirstOrDefault() ?? "Content-Type, Authorization, X-Tenant-Id";
      context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
      context.Response.Headers["Access-Control-Max-Age"] = "86400";
      context.Response.StatusCode = StatusCodes.Status204NoContent;
      return;
    }
  }

  if (originAllowed)
  {
    context.Response.OnStarting(() =>
    {
      if (!context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
      {
        context.Response.Headers["Access-Control-Allow-Origin"] = origin;
        context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        context.Response.Headers["Access-Control-Expose-Headers"] = "Content-Disposition";
      }
      return Task.CompletedTask;
    });
  }

  await next();
});

app.UseCors("frontend");

/* -------------------- DB Migration & Seeding -------------------- */
using (var scope = app.Services.CreateScope())
{
  var logger = scope.ServiceProvider
      .GetRequiredService<ILoggerFactory>()
      .CreateLogger("Startup");

  try
  {
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    await EnsureLeadFollowUpsTableAsync(db, logger);

    var superEmail = app.Configuration["SuperAdmin:Email"] ?? "super@travelpathways.local";
    var superPassword = app.Configuration["SuperAdmin:Password"] ?? "Super@123";

    var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == superEmail);
    if (existing == null)
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
    logger.LogError(ex, "Database migration/seed failed.");
  }
}

/* -------------------- Swagger Middleware -------------------- */
app.UseSwagger();
app.UseSwaggerUI(c =>
{
  c.SwaggerEndpoint("/swagger/v1/swagger.json", "TravelPathways API v1");
  c.RoutePrefix = "swagger";
});

/* -------------------- HTTPS (Production Only) -------------------- */
if (!app.Environment.IsDevelopment())
{
  app.UseHttpsRedirection();
}

/* -------------------- Static Files & Uploads -------------------- */
var customUploadsPath = app.Configuration["Uploads:Path"]?.Trim() ?? app.Configuration["Uploads__Path"]?.Trim();
if (!string.IsNullOrEmpty(customUploadsPath))
{
  var provider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(customUploadsPath);
  app.UseStaticFiles(new StaticFileOptions { FileProvider = provider, RequestPath = "/uploads" });
}
else
{
  var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");
  if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);
}

/* -------------------- Global exception handler (returns JSON so frontend gets CORS + body) -------------------- */
app.UseExceptionHandler(a => a.Run(async context =>
{
  var ex = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
  context.Response.StatusCode = StatusCodes.Status500InternalServerError;
  context.Response.ContentType = "application/json";

  // Get the real error message (unwrap DB/SQL exceptions)
  string detailMessage = GetExceptionDetailMessage(ex);

  // Always log the full error on the server so you can see it in Azure Log stream / App Insights
  var logger = context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("ExceptionHandler");
  if (ex != null)
    logger?.LogError(ex, "Unhandled exception: {Detail}", detailMessage);
  else
    logger?.LogError("Unhandled exception (no exception object). Detail: {Detail}", detailMessage);

  // In Development, or when IncludeExceptionDetailsInResponse is true, return the actual message to the client
  bool includeDetails = app.Environment.IsDevelopment()
    || string.Equals(app.Configuration["IncludeExceptionDetailsInResponse"], "true", StringComparison.OrdinalIgnoreCase);
  string message = includeDetails && !string.IsNullOrEmpty(detailMessage) ? detailMessage : "An error occurred.";
  await context.Response.WriteAsJsonAsync(new { message });
}));

static string GetExceptionDetailMessage(Exception? ex)
{
  if (ex == null) return "";
  if (ex is DbUpdateException dbEx && dbEx.InnerException != null)
    return dbEx.InnerException.Message;
  return ex.Message;
}

/* -------------------- Middleware Order (CRITICAL) -------------------- */
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();

app.UseStaticFiles();
app.MapControllers();

/* -------------------- Root URL (avoid 404 on base URL) -------------------- */
app.MapGet("/", () => Results.Ok(new
{
  name = "TravelPathways API",
  status = "running",
  docs = "swagger",
  swaggerUrl = "/swagger"
}));

app.Run();

/* -------------------- Helpers -------------------- */
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
                CONSTRAINT [FK_LeadFollowUps_Leads_LeadId]
                    FOREIGN KEY ([LeadId]) REFERENCES [Leads] ([Id])
                    ON DELETE CASCADE
            );
            CREATE INDEX [IX_LeadFollowUps_LeadId]
                ON [dbo].[LeadFollowUps] ([LeadId]);
        END
        """;

    await db.Database.ExecuteSqlRawAsync(sql);
  }
  catch (Exception ex)
  {
    logger.LogWarning(ex, "Could not ensure LeadFollowUps table.");
  }
}
