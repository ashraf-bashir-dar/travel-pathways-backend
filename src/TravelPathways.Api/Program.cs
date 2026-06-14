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
builder.Services.AddScoped<RequireTenantActionFilter>();
builder.Services.AddScoped<TenantMiddleware>();
builder.Services.AddScoped<FileStorage>();
builder.Services.AddScoped<TravelPathways.Api.Services.IPdfTemplateHtmlCache, TravelPathways.Api.Services.PdfTemplateHtmlCache>();
builder.Services.AddSingleton<TravelPathways.Api.Services.IChromiumBrowserProvider, TravelPathways.Api.Services.ChromiumBrowserProvider>();
builder.Services.AddHostedService<TravelPathways.Api.Services.ChromiumBrowserHostedService>();
builder.Services.AddScoped<TravelPathways.Api.Services.IPackagePdfGenerator, TravelPathways.Api.Services.PackagePdfGenerator>();
builder.Services.AddScoped<TravelPathways.Api.Services.IPackageMasterDataService, TravelPathways.Api.Services.PackageMasterDataService>();
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
        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed. Continuing with compatibility schema checks.");
        }
        // Backward-safe hotfix: ensure newly introduced user permission column exists
        // even if migration history is out-of-sync in a local DB.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"CanPriceOverride\" boolean NOT NULL DEFAULT false;");
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"LeaveDate\" timestamp with time zone;");
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"ActivityTrackingEnabled\" boolean NOT NULL DEFAULT true;");
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"ShiftStartTime\" time without time zone;");
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"ShiftEndTime\" time without time zone;");
        // Same for package margin (PDF / price override); avoids 42703 if migrations were not applied to this DB.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Packages\" ADD COLUMN IF NOT EXISTS \"MarginAmount\" numeric(18,2) NOT NULL DEFAULT 0;");
        // Ledger payment fields; avoids 42703 if AddLedgerPaymentFields migration was not applied.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Payments\" ADD COLUMN IF NOT EXISTS \"PaymentMode\" text;");
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Payments\" ADD COLUMN IF NOT EXISTS \"RecordedByUserId\" uuid;");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_Payments_RecordedByUserId\" ON \"Payments\" (\"RecordedByUserId\");");
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "SalesConfirmedPackages" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "ClientName" character varying(200) NOT NULL,
                "ClientPhone" character varying(20) NOT NULL,
                "ArrivalDate" date NOT NULL,
                "DepartureDate" date NOT NULL,
                "ExpectedProfit" numeric(18,2) NOT NULL,
                "ActualProfit" numeric(18,2),
                "ConfirmationDate" date NOT NULL,
                "SourceType" text NOT NULL,
                "LeadId" uuid REFERENCES "Leads"("Id") ON DELETE SET NULL,
                "ReferenceName" character varying(200),
                "ReferenceContact" character varying(50),
                "RecordedByUserId" uuid NOT NULL REFERENCES "Users"("Id"),
                "TenantId" uuid NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false,
                "DeletedAtUtc" timestamp with time zone
            );
            CREATE INDEX IF NOT EXISTS "IX_SalesConfirmedPackages_LeadId" ON "SalesConfirmedPackages" ("LeadId");
            CREATE INDEX IF NOT EXISTS "IX_SalesConfirmedPackages_RecordedByUserId" ON "SalesConfirmedPackages" ("RecordedByUserId");
            CREATE INDEX IF NOT EXISTS "IX_SalesConfirmedPackages_TenantId_ConfirmationDate"
                ON "SalesConfirmedPackages" ("TenantId", "ConfirmationDate");
            """);
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"SalesConfirmedPackages\" ADD COLUMN IF NOT EXISTS \"ReferenceSourceType\" text;");
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE "SalesConfirmedPackages"
            SET "ReferenceSourceType" = 'OfficeReference'
            WHERE "ReferenceSourceType" IN ('InsideReference', 'TravelPathwaysReference');
            UPDATE "SalesConfirmedPackages"
            SET "ReferenceSourceType" = 'PersonalReference'
            WHERE "ReferenceSourceType" = 'OutsideReference';
            """);
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SalesConfirmedPackages_TenantId_LeadId_Active"
                    ON "SalesConfirmedPackages" ("TenantId", "LeadId")
                    WHERE "IsDeleted" = false AND "LeadId" IS NOT NULL;
                """);
        }
        catch (Exception indexEx)
        {
            logger.LogWarning(
                indexEx,
                "Could not create unique index on SalesConfirmedPackages (duplicate lead rows may exist). Application validation still blocks new duplicates.");
        }
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "UserActivityDailySummaries" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "UserId" uuid NOT NULL REFERENCES "Users"("Id"),
                "ActivityDate" timestamp with time zone NOT NULL,
                "ActiveSeconds" integer NOT NULL DEFAULT 0,
                "IdleSeconds" integer NOT NULL DEFAULT 0,
                "IsCurrentlyIdle" boolean NOT NULL DEFAULT false,
                "LastReportedAtUtc" timestamp with time zone NOT NULL,
                "TenantId" uuid NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false,
                "DeletedAtUtc" timestamp with time zone
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserActivityDailySummaries_TenantId_UserId_ActivityDate"
                ON "UserActivityDailySummaries" ("TenantId", "UserId", "ActivityDate");
            CREATE INDEX IF NOT EXISTS "IX_UserActivityDailySummaries_UserId"
                ON "UserActivityDailySummaries" ("UserId");
            """);
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "UserActivityPageVisits" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "UserId" uuid NOT NULL REFERENCES "Users"("Id"),
                "Path" character varying(500) NOT NULL,
                "Url" character varying(2000) NOT NULL,
                "PageTitle" character varying(500),
                "VisitedAtUtc" timestamp with time zone NOT NULL,
                "TenantId" uuid NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false,
                "DeletedAtUtc" timestamp with time zone
            );
            CREATE INDEX IF NOT EXISTS "IX_UserActivityPageVisits_TenantId_UserId_VisitedAtUtc"
                ON "UserActivityPageVisits" ("TenantId", "UserId", "VisitedAtUtc");
            CREATE INDEX IF NOT EXISTS "IX_UserActivityPageVisits_UserId"
                ON "UserActivityPageVisits" ("UserId");
            ALTER TABLE "UserActivityPageVisits" ADD COLUMN IF NOT EXISTS "Source" character varying(32) NOT NULL DEFAULT 'InApp';
            ALTER TABLE "UserActivityPageVisits" ADD COLUMN IF NOT EXISTS "DurationSeconds" integer;
            CREATE INDEX IF NOT EXISTS "IX_UserActivityPageVisits_TenantId_Source_VisitedAtUtc"
                ON "UserActivityPageVisits" ("TenantId", "Source", "VisitedAtUtc");
            """);
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "ExtensionCatalogItems" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "Code" character varying(64) NOT NULL,
                "Name" character varying(200) NOT NULL,
                "Summary" character varying(500) NOT NULL,
                "Details" text NOT NULL DEFAULT '',
                "Icon" character varying(16) NOT NULL DEFAULT '🧩',
                "SupportedBrowsers" character varying(120) NOT NULL DEFAULT 'Chrome, Edge',
                "ChromeStoreUrl" character varying(1000),
                "EdgeStoreUrl" character varying(1000),
                "DownloadApiPath" character varying(256),
                "InstallSteps" text,
                "SortOrder" integer NOT NULL DEFAULT 0,
                "IsPublished" boolean NOT NULL DEFAULT true,
                "TenantId" uuid NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false,
                "DeletedAtUtc" timestamp with time zone
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ExtensionCatalogItems_TenantId_Code"
                ON "ExtensionCatalogItems" ("TenantId", "Code");
            CREATE INDEX IF NOT EXISTS "IX_ExtensionCatalogItems_TenantId_SortOrder"
                ON "ExtensionCatalogItems" ("TenantId", "SortOrder");
            """);
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
            ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "IsLocked" boolean NOT NULL DEFAULT false;
            ALTER TABLE "Packages" ADD COLUMN IF NOT EXISTS "IsLocked" boolean NOT NULL DEFAULT false;
            ALTER TABLE "Packages" ADD COLUMN IF NOT EXISTS "ExclusionIds" text NOT NULL DEFAULT '[]';
            ALTER TABLE "Reservations" ADD COLUMN IF NOT EXISTS "IsLocked" boolean NOT NULL DEFAULT false;
            CREATE TABLE IF NOT EXISTS "ReservationHotelBookings" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false,
                "DeletedAtUtc" timestamp with time zone,
                "TenantId" uuid NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                "ReservationId" uuid NOT NULL,
                "DayNumber" integer NOT NULL,
                "BookingDate" timestamp with time zone NOT NULL,
                "CheckInDate" timestamp with time zone,
                "CheckOutDate" timestamp with time zone,
                "HotelId" uuid,
                "HotelName" text NOT NULL DEFAULT '',
                "IsHouseboat" boolean NOT NULL DEFAULT false,
                "RoomType" text,
                "NumberOfRooms" integer NOT NULL DEFAULT 0,
                "ExtraBedCount" integer NOT NULL DEFAULT 0,
                "CnbCount" integer NOT NULL DEFAULT 0,
                "NumberOfPersons" integer NOT NULL DEFAULT 0,
                "RatePerNight" numeric(18,2) NOT NULL DEFAULT 0,
                "TotalAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "AdvancePaid" numeric(18,2) NOT NULL DEFAULT 0,
                "BalanceAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "Status" text NOT NULL DEFAULT 'Pending',
                "IsLocked" boolean NOT NULL DEFAULT false,
                "ConfirmationNumber" text,
                "Notes" text,
                CONSTRAINT "PK_ReservationHotelBookings" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ReservationHotelBookings_Reservations_ReservationId" FOREIGN KEY ("ReservationId") REFERENCES "Reservations" ("Id") ON DELETE CASCADE
            );
            ALTER TABLE "ReservationHotelBookings" ADD COLUMN IF NOT EXISTS "IsLocked" boolean NOT NULL DEFAULT false;
            ALTER TABLE "ReservationHotelBookings" ADD COLUMN IF NOT EXISTS "ExtraBedRate" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "ReservationHotelBookings" ADD COLUMN IF NOT EXISTS "CnbRate" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "ReservationHotelBookings" ADD COLUMN IF NOT EXISTS "CancellationReason" text;
            ALTER TABLE "ReservationHotelBookings" ADD COLUMN IF NOT EXISTS "CancellationReasonDetail" text;
            CREATE INDEX IF NOT EXISTS "IX_ReservationHotelBookings_ReservationId_DayNumber" ON "ReservationHotelBookings" ("ReservationId", "DayNumber");
            CREATE TABLE IF NOT EXISTS "ReservationHotelBookingDocuments" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false,
                "DeletedAtUtc" timestamp with time zone,
                "ReservationHotelBookingId" uuid NOT NULL,
                "Type" text NOT NULL DEFAULT 'PaymentProof',
                "Amount" numeric(18,2),
                "PaymentDate" timestamp with time zone,
                "FileUrl" text NOT NULL,
                "FileName" text NOT NULL,
                CONSTRAINT "PK_ReservationHotelBookingDocuments" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ReservationHotelBookingDocuments_ReservationHotelBookings_ReservationHotelBookingId" FOREIGN KEY ("ReservationHotelBookingId") REFERENCES "ReservationHotelBookings" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_ReservationHotelBookingDocuments_ReservationHotelBookingId" ON "ReservationHotelBookingDocuments" ("ReservationHotelBookingId");
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
            CREATE TABLE IF NOT EXISTS "PackageLogs" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false,
                "DeletedAtUtc" timestamp with time zone,
                "TenantId" uuid NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                "LeadId" uuid NOT NULL,
                "PackageId" uuid NOT NULL,
                "Action" text NOT NULL DEFAULT 'Updated',
                "PackageName" text NOT NULL DEFAULT '',
                "FinalAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "MarginAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "Status" text NOT NULL DEFAULT 'New',
                "ChangedByUserId" uuid,
                "ChangedByDisplayName" text NOT NULL DEFAULT '',
                "SnapshotJson" text NOT NULL DEFAULT '{{}}',
                CONSTRAINT "PK_PackageLogs" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_PackageLogs_Leads_LeadId"
                    FOREIGN KEY ("LeadId") REFERENCES "Leads" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_PackageLogs_Packages_PackageId"
                    FOREIGN KEY ("PackageId") REFERENCES "Packages" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_PackageLogs_TenantId_LeadId"
                ON "PackageLogs" ("TenantId", "LeadId");
            CREATE INDEX IF NOT EXISTS "IX_PackageLogs_LeadId_CreatedAt"
                ON "PackageLogs" ("LeadId", "CreatedAt" DESC);
            ALTER TABLE "PackageLogs" ADD COLUMN IF NOT EXISTS "Action" text NOT NULL DEFAULT 'Updated';
            ALTER TABLE "PackageLogs" ADD COLUMN IF NOT EXISTS "Status" text NOT NULL DEFAULT 'New';
            ALTER TABLE "PackageLogs" ADD COLUMN IF NOT EXISTS "MarginAmount" numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE "PackageLogs" ADD COLUMN IF NOT EXISTS "SnapshotJson" text NOT NULL DEFAULT '{{}}';
            """);

        // Call logging (incoming/outgoing/missed) captured via provider webhooks.
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "CallLogs" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false,
                "DeletedAtUtc" timestamp with time zone,
                "TenantId" uuid NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                "UserId" uuid,
                "Direction" character varying(16) NOT NULL,
                "Status" character varying(64),
                "Provider" character varying(64),
                "ProviderCallId" character varying(128),
                "FromNumber" character varying(48),
                "ToNumber" character varying(48),
                "StartedAtUtc" timestamp with time zone,
                "EndedAtUtc" timestamp with time zone,
                "DurationSeconds" integer,
                "RawPayload" text NOT NULL DEFAULT '{{}}',
                CONSTRAINT "PK_CallLogs" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_CallLogs_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE SET NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_CallLogs_TenantId_CreatedAt" ON "CallLogs" ("TenantId", "CreatedAt" DESC);
            CREATE INDEX IF NOT EXISTS "IX_CallLogs_UserId_CreatedAt" ON "CallLogs" ("UserId", "CreatedAt" DESC);
            """);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "EmployeeLocationLogs" (
                "Id" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "IsDeleted" boolean NOT NULL DEFAULT false,
                "DeletedAtUtc" timestamp with time zone,
                "TenantId" uuid NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                "UserId" uuid NOT NULL,
                "Latitude" double precision NOT NULL,
                "Longitude" double precision NOT NULL,
                "AccuracyMeters" double precision,
                "Provider" character varying(64) NOT NULL DEFAULT 'android',
                "ProviderPointId" character varying(128),
                "RecordedAtUtc" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_EmployeeLocationLogs" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_EmployeeLocationLogs_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_EmployeeLocationLogs_TenantId_RecordedAtUtc" ON "EmployeeLocationLogs" ("TenantId", "RecordedAtUtc" DESC);
            CREATE INDEX IF NOT EXISTS "IX_EmployeeLocationLogs_UserId_RecordedAtUtc" ON "EmployeeLocationLogs" ("UserId", "RecordedAtUtc" DESC);
            CREATE INDEX IF NOT EXISTS "IX_EmployeeLocationLogs_TenantId_Provider_ProviderPointId" ON "EmployeeLocationLogs" ("TenantId", "Provider", "ProviderPointId");
            """);

        await TravelPathways.Api.Data.PackageMasterSchemaBootstrap.EnsureAsync(db);
        await TravelPathways.Api.Data.B2bAgentSchemaBootstrap.EnsureAsync(db);

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
