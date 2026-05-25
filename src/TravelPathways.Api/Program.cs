using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;
using TravelPathways.Api.Auth;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;
using TravelPathways.Api.Storage;
using TravelPathways.Api.Hubs;
using TravelPathways.Api.Swagger;

var builder = WebApplication.CreateBuilder(args);

/* -------------------- Controllers & JSON -------------------- */
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.Converters.Add(new UserRoleJsonConverter());
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddSignalR();

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
  : corsOriginsConfig
      .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
      .Select(s => s.Trim().TrimEnd('/'))
      .Where(s => s.Length > 0)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToArray();
var allowedOrigins = parsed.Length > 0
  ? parsed
  : new[]
  {
      "http://localhost:4200",
      "https://localhost:4200"
  }; // Production: set Cors__AllowedOrigins (e.g. your CloudFront https://... URL).

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
builder.Services.AddMemoryCache();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<TenantMiddleware>();
builder.Services.AddScoped<FileStorage>();
builder.Services.AddScoped<TravelPathways.Api.Services.IPdfTemplateHtmlCache, TravelPathways.Api.Services.PdfTemplateHtmlCache>();
builder.Services.AddSingleton<TravelPathways.Api.Services.IChromiumBrowserProvider, TravelPathways.Api.Services.ChromiumBrowserProvider>();
builder.Services.AddHostedService<TravelPathways.Api.Services.ChromiumBrowserHostedService>();
builder.Services.AddScoped<TravelPathways.Api.Services.IPackagePdfGenerator, TravelPathways.Api.Services.PackagePdfGenerator>();
builder.Services.AddScoped<TravelPathways.Api.Services.ILeadExcelImportService,
    TravelPathways.Api.Services.LeadExcelImportService>();
builder.Services.AddScoped<TravelPathways.Api.Services.ILeadExcelExportService,
    TravelPathways.Api.Services.LeadExcelExportService>();
builder.Services.Configure<TravelPathways.Api.Services.Inbound.MetaOptions>(
    builder.Configuration.GetSection("Meta"));
builder.Services.AddHttpClient();
builder.Services.AddScoped<TravelPathways.Api.Services.Inbound.ITenantLeadIntegrationResolver,
    TravelPathways.Api.Services.Inbound.TenantLeadIntegrationResolver>();
builder.Services.AddScoped<TravelPathways.Api.Services.Inbound.ILeadAutoAssignmentService,
    TravelPathways.Api.Services.Inbound.LeadAutoAssignmentService>();
builder.Services.AddScoped<TravelPathways.Api.Services.Inbound.IInboundLeadProcessor,
    TravelPathways.Api.Services.Inbound.InboundLeadProcessor>();
builder.Services.AddScoped<TravelPathways.Api.Services.Inbound.IMetaLeadAdsService,
    TravelPathways.Api.Services.Inbound.MetaLeadAdsService>();
builder.Services.AddScoped<TravelPathways.Api.Services.IEmailService,
                          TravelPathways.Api.Services.EmailService>();
builder.Services.AddSingleton<TravelPathways.Api.Services.IPasswordEncryption,
                              TravelPathways.Api.Services.PasswordEncryptionService>();

/* -------------------- Database (PostgreSQL) -------------------- */
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
    });
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

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
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

/* -------------------- Forwarded headers FIRST (ALB / reverse proxy: correct Scheme/Host for uploads + client-environment) -------------------- */
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    KnownNetworks = { },
    KnownProxies = { }
});

if (allowedOrigins.Length > 0)
{
  app.Logger.LogInformation(
      "CORS: {Count} allowed origin(s): {Origins}",
      allowedOrigins.Length,
      string.Join(", ", allowedOrigins));
}

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

/* -------------------- CORS: handle preflight and add headers to all responses -------------------- */
app.Use(async (context, next) =>
{
    var origin = context.Request.Headers["Origin"].FirstOrDefault() ?? "";
    var originAllowed = !string.IsNullOrEmpty(origin) &&
      allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);

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
        // Backward-safe hotfix: ensure newly introduced user permission column exists
        // even if migration history is out-of-sync in a local DB.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"CanPriceOverride\" boolean NOT NULL DEFAULT false;");
        // Same for package margin (PDF / price override); avoids 42703 if migrations were not applied to this DB.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Packages\" ADD COLUMN IF NOT EXISTS \"MarginAmount\" numeric(18,2) NOT NULL DEFAULT 0;");
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "ChatGroups" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                "Name" character varying(200) NOT NULL,
                "Description" character varying(500),
                "CreatedByUserId" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false,
                "DeletedAtUtc" timestamp with time zone,
                CONSTRAINT "PK_ChatGroups" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ChatGroups_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_ChatGroups_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS "ChatGroupMembers" (
                "GroupId" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "JoinedAt" timestamp with time zone NOT NULL,
                "AddedByUserId" uuid,
                "LastReadAtUtc" timestamp with time zone,
                CONSTRAINT "PK_ChatGroupMembers" PRIMARY KEY ("GroupId", "UserId"),
                CONSTRAINT "FK_ChatGroupMembers_ChatGroups_GroupId" FOREIGN KEY ("GroupId") REFERENCES "ChatGroups" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_ChatGroupMembers_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
            );
            CREATE TABLE IF NOT EXISTS "ChatMessages" (
                "Id" uuid NOT NULL,
                "GroupId" uuid NOT NULL,
                "SenderUserId" uuid NOT NULL,
                "Body" character varying(4000) NOT NULL,
                "SentAtUtc" timestamp with time zone NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false,
                "DeletedAtUtc" timestamp with time zone,
                CONSTRAINT "PK_ChatMessages" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ChatMessages_ChatGroups_GroupId" FOREIGN KEY ("GroupId") REFERENCES "ChatGroups" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_ChatMessages_Users_SenderUserId" FOREIGN KEY ("SenderUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS "IX_ChatGroups_TenantId" ON "ChatGroups" ("TenantId");
            CREATE INDEX IF NOT EXISTS "IX_ChatMessages_GroupId_SentAtUtc" ON "ChatMessages" ("GroupId", "SentAtUtc");
            ALTER TABLE "ChatGroups" ADD COLUMN IF NOT EXISTS "IsDirect" boolean NOT NULL DEFAULT false;
            ALTER TABLE "ChatGroups" ADD COLUMN IF NOT EXISTS "DirectPairKey" character varying(100);
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ChatGroups_TenantId_DirectPairKey"
                ON "ChatGroups" ("TenantId", "DirectPairKey")
                WHERE "IsDirect" = true AND "DirectPairKey" IS NOT NULL;
            ALTER TABLE "ChatMessages" ADD COLUMN IF NOT EXISTS "MentionedUserIds" text NOT NULL DEFAULT '[]';
            ALTER TABLE "ChatMessages" ADD COLUMN IF NOT EXISTS "ImageUrls" text NOT NULL DEFAULT '[]';
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "InboundLeadsFeatureEnabled" boolean NOT NULL DEFAULT false;
            ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "ParticipateInInboundAutoAssign" boolean NOT NULL DEFAULT false;
            ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "InboundDailyLeadQuota" integer NOT NULL DEFAULT 0;
            ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "InboundAllowedLeadSources" text NOT NULL DEFAULT '[]';
            ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "InboundProvider" text;
            ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "InboundExternalId" text;
            CREATE TABLE IF NOT EXISTS "TenantLeadIntegrations" (
                "Id" uuid NOT NULL,
                "InboundKey" text NOT NULL,
                "IsInboundEnabled" boolean NOT NULL DEFAULT false,
                "AutoAssignEnabled" boolean NOT NULL DEFAULT false,
                "MetaPageId" text,
                "MetaPageAccessTokenEncrypted" text,
                "MetaConnectionVerified" boolean NOT NULL DEFAULT false,
                "MetaLastWebhookAtUtc" timestamp with time zone,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false,
                "DeletedAtUtc" timestamp with time zone,
                "TenantId" uuid NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                CONSTRAINT "PK_TenantLeadIntegrations" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_TenantLeadIntegrations_InboundKey" ON "TenantLeadIntegrations" ("InboundKey");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_TenantLeadIntegrations_TenantId" ON "TenantLeadIntegrations" ("TenantId");
            CREATE TABLE IF NOT EXISTS "InboundLeadEvents" (
                "Id" uuid NOT NULL,
                "Provider" text NOT NULL,
                "ExternalId" text,
                "Status" text NOT NULL,
                "RawPayload" text,
                "ErrorMessage" text,
                "LeadId" uuid,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false,
                "DeletedAtUtc" timestamp with time zone,
                "TenantId" uuid NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                CONSTRAINT "PK_InboundLeadEvents" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_InboundLeadEvents_TenantId_CreatedAt" ON "InboundLeadEvents" ("TenantId", "CreatedAt");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Leads_TenantId_InboundProvider_InboundExternalId"
                ON "Leads" ("TenantId", "InboundProvider", "InboundExternalId")
                WHERE "InboundExternalId" IS NOT NULL;
            """);

        var superAdminEnabled = app.Configuration.GetValue<bool>("SuperAdmin:Enabled", true);
        if (superAdminEnabled)
        {
            var superEmail = app.Configuration["SuperAdmin:Email"] ?? "super@travelpathways.local";
            var superPassword = app.Configuration["SuperAdmin:Password"] ?? "Super@123";

            // IgnoreQueryFilters so we find Super Admin even when tenant context is set (e.g. startup)
            var existing = await db.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == superEmail && u.TenantId == null);
            var passwordEncryption = scope.ServiceProvider.GetRequiredService<TravelPathways.Api.Services.IPasswordEncryption>();

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
                    PasswordHash = PasswordHasher.Hash(superPassword),
                    PasswordEncrypted = passwordEncryption.Encrypt(superPassword)
                });
                await db.SaveChangesAsync();
                logger.LogInformation("Super Admin user created: {Email}", superEmail);
            }
            else if (string.IsNullOrWhiteSpace(existing.PasswordHash) || !PasswordHasher.Verify(superPassword, existing.PasswordHash))
            {
                // Re-seed password (e.g. after DB restore or manual fix)
                existing.PasswordHash = PasswordHasher.Hash(superPassword);
                existing.PasswordEncrypted = passwordEncryption.Encrypt(superPassword);
                existing.IsActive = true;
                existing.IsDeleted = false;
                existing.DeletedAtUtc = null;
                await db.SaveChangesAsync();
                logger.LogInformation("Super Admin password reset: {Email}", superEmail);
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
                EnabledModules = Enum.GetValues<AppModuleKey>()
                    .Where(m => m != AppModuleKey.LeadIntegrations)
                    .ToList(),
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
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = uploadsProvider,
    RequestPath = "/uploads",
    OnPrepareResponse = ctx =>
    {
        // Allow the SPA on another origin to load uploaded images (<img>, PDF generation fallbacks).
        ctx.Context.Response.Headers.Append("Cross-Origin-Resource-Policy", "cross-origin");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
    }
});

/* -------------------- Global exception handler (returns JSON so frontend gets CORS + body) -------------------- */
app.UseExceptionHandler(a => a.Run(async context =>
{
    var ex = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";

    // Get the real error message (unwrap DB/SQL exceptions)
    string detailMessage = GetExceptionDetailMessage(ex);

    // Always log the full error on the server (CloudWatch, container logs, etc.)
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
app.MapHub<ChatHub>("/hubs/chat");

/* -------------------- Root URL (avoid 404 on base URL) -------------------- */
app.MapGet("/", () => Results.Ok(new
{
    name = "TravelPathways API",
    status = "running",
    docs = "swagger",
    swaggerUrl = "/swagger"
}));

app.Run();
