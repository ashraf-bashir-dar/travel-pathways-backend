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
using TravelPathways.Api.Swagger;

var builder = WebApplication.CreateBuilder(args);

/* -------------------- Controllers & JSON -------------------- */
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
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

    // Schema ID must be non-null and unique (null can cause 500 when generating swagger.json)
    c.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

    // Fix file upload endpoints so Swagger document generation does not fail with 500
    c.OperationFilter<FileUploadOperationFilter>();
    c.MapType<IFormFile>(() => new OpenApiSchema { Type = "string", Format = "binary", Description = "File upload" });
    c.MapType<Stream>(() => new OpenApiSchema { Type = "string", Format = "binary" });

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
builder.Services.AddSingleton<TravelPathways.Api.Services.IChromiumBrowserProvider, TravelPathways.Api.Services.ChromiumBrowserProvider>();
builder.Services.AddHostedService<TravelPathways.Api.Services.ChromiumBrowserHostedService>();
builder.Services.AddScoped<TravelPathways.Api.Services.IPackagePdfGenerator, TravelPathways.Api.Services.PackagePdfGenerator>();
builder.Services.AddScoped<TravelPathways.Api.Services.IEmailService,
                          TravelPathways.Api.Services.EmailService>();
builder.Services.AddSingleton<TravelPathways.Api.Services.IPasswordEncryption,
                              TravelPathways.Api.Services.PasswordEncryptionService>();

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

builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, SuperAdminAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly",
        policy => policy.Requirements.Add(new SuperAdminRequirement()));
    options.AddPolicy("TenantAdminOnly",
        policy => policy.RequireRole(UserRole.Admin.ToString(), UserRole.SuperAdmin.ToString()));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

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
        await EnsureAttendanceTableAsync(db, logger);
        await EnsureLeavesTableAsync(db, logger);
        await EnsureTasksTimeColumnsAsync(db, logger);

        var superAdminEnabled = app.Configuration.GetValue<bool>("SuperAdmin:Enabled", true);
        if (superAdminEnabled)
        {
            var superEmail = app.Configuration["SuperAdmin:Email"] ?? "super@travelpathways.local";
            var superPassword = app.Configuration["SuperAdmin:Password"] ?? "Super@123";

            var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == superEmail);
            if (existing == null)
            {
                var passwordEncryption = scope.ServiceProvider.GetRequiredService<TravelPathways.Api.Services.IPasswordEncryption>();
                db.Users.Add(new AppUser
                {
                    TenantId = null,
                    Email = superEmail,
                    FirstName = "Super",
                    LastName = "Admin",
                    Role = UserRole.SuperAdmin,
                    IsActive = true,
                    PasswordHash = PasswordHasher.Hash(superPassword),
                    PasswordEncrypted = passwordEncryption.Encrypt(superPassword)
                });

                await db.SaveChangesAsync();
            }
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
var uploadsPath = !string.IsNullOrEmpty(customUploadsPath)
    ? customUploadsPath
    : Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");
if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);
var uploadsProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath);
app.UseStaticFiles(new StaticFileOptions { FileProvider = uploadsProvider, RequestPath = "/uploads" });

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

static async Task EnsureAttendanceTableAsync(AppDbContext db, ILogger logger)
{
    try
    {
        const string sql = """
        IF OBJECT_ID(N'dbo.Attendance', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[Attendance] (
                [Id] uniqueidentifier NOT NULL,
                [TenantId] uniqueidentifier NOT NULL,
                [IsActive] bit NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [UpdatedAt] datetime2 NOT NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAtUtc] datetime2 NULL,
                [UserId] uniqueidentifier NOT NULL,
                [AttendanceDate] date NOT NULL,
                [TimeInUtc] datetime2 NULL,
                [TimeOutUtc] datetime2 NULL,
                CONSTRAINT [PK_Attendance] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_Attendance_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_Attendance_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION,
                CONSTRAINT [UQ_Attendance_TenantUserDate] UNIQUE ([TenantId], [UserId], [AttendanceDate])
            );
            CREATE INDEX [IX_Attendance_TenantId] ON [dbo].[Attendance] ([TenantId]);
            CREATE INDEX [IX_Attendance_UserId] ON [dbo].[Attendance] ([UserId]);
            CREATE INDEX [IX_Attendance_AttendanceDate] ON [dbo].[Attendance] ([AttendanceDate]);
        END
        """;
        await db.Database.ExecuteSqlRawAsync(sql);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not ensure Attendance table.");
    }
}

static async Task EnsureLeavesTableAsync(AppDbContext db, ILogger logger)
{
    try
    {
        const string sql = """
        IF OBJECT_ID(N'dbo.Leaves', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[Leaves] (
                [Id] uniqueidentifier NOT NULL,
                [TenantId] uniqueidentifier NOT NULL,
                [IsActive] bit NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [UpdatedAt] datetime2 NOT NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAtUtc] datetime2 NULL,
                [UserId] uniqueidentifier NOT NULL,
                [LeaveType] int NOT NULL,
                [StartDate] date NOT NULL,
                [EndDate] date NOT NULL,
                [Reason] nvarchar(max) NOT NULL,
                [Status] int NOT NULL,
                CONSTRAINT [PK_Leaves] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_Leaves_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_Leaves_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
            );
            CREATE INDEX [IX_Leaves_TenantId] ON [dbo].[Leaves] ([TenantId]);
            CREATE INDEX [IX_Leaves_UserId] ON [dbo].[Leaves] ([UserId]);
            CREATE INDEX [IX_Leaves_Status] ON [dbo].[Leaves] ([Status]);
            CREATE INDEX [IX_Leaves_StartDate] ON [dbo].[Leaves] ([StartDate]);
        END
        """;
        await db.Database.ExecuteSqlRawAsync(sql);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not ensure Leaves table.");
    }
}

static async Task EnsureTasksTimeColumnsAsync(AppDbContext db, ILogger logger)
{
    try
    {
        const string sql = """
        IF NOT EXISTS (SELECT 1 FROM sys.columns c INNER JOIN sys.tables t ON c.object_id = t.object_id WHERE t.name = 'Tasks' AND c.name = 'StartTimeUtc')
        ALTER TABLE [dbo].[Tasks] ADD [StartTimeUtc] datetime2 NULL;
        IF NOT EXISTS (SELECT 1 FROM sys.columns c INNER JOIN sys.tables t ON c.object_id = t.object_id WHERE t.name = 'Tasks' AND c.name = 'EndTimeUtc')
        ALTER TABLE [dbo].[Tasks] ADD [EndTimeUtc] datetime2 NULL;
        """;
        await db.Database.ExecuteSqlRawAsync(sql);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not ensure Tasks StartTimeUtc/EndTimeUtc columns.");
    }
}
