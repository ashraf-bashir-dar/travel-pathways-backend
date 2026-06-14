using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TravelPathways.Api.Auth;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Services;
using TravelPathways.Api.Storage;
using SubscriptionStatus = TravelPathways.Api.Common.SubscriptionStatus;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokens;
    private readonly ILogger<AuthController> _logger;
    private readonly IPasswordEncryption _passwordEncryption;
    private readonly FileStorage _storage;

    public AuthController(
        AppDbContext db,
        TokenService tokens,
        ILogger<AuthController> logger,
        IPasswordEncryption passwordEncryption,
        FileStorage storage)
    {
        _db = db;
        _tokens = tokens;
        _logger = logger;
        _passwordEncryption = passwordEncryption;
        _storage = storage;
    }

    public sealed class LoginRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? TenantId { get; set; }

        // Optional convenience (not used by current UI, but helpful)
        public string? TenantCode { get; set; }
    }

    public sealed class UserDto
    {
        public required string Id { get; init; }
        public required string Email { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string TenantId { get; init; }
        public required UserRole Role { get; init; }
        public UserDepartment? Department { get; init; }
        public List<AppModuleKey>? AllowedModules { get; init; }
        public required bool IsActive { get; init; }
        public required DateTime CreatedAt { get; init; }
        public bool CanViewCostBifurcation { get; init; }
        public bool CanPriceOverride { get; init; }
        public bool ActivityTrackingEnabled { get; init; } = true;
        public string? Phone { get; init; }
        public DateTime? DateOfBirth { get; init; }
        public DateTime? JoinDate { get; init; }
        public DateTime? LeaveDate { get; init; }
        public string? Designation { get; init; }
        public string? Address { get; init; }
        public string? EmergencyContactName { get; init; }
        public string? EmergencyContactPhone { get; init; }
        public string? ProfilePhotoUrl { get; init; }
        public string? ShiftStartTime { get; init; }
        public string? ShiftEndTime { get; init; }
    }

    public sealed class UserProfileDto
    {
        public required string Id { get; init; }
        public required string Email { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string TenantId { get; init; }
        public UserRole Role { get; init; }
        public UserDepartment? Department { get; init; }
        public string? Designation { get; init; }
        public string? Phone { get; init; }
        public DateTime? DateOfBirth { get; init; }
        public DateTime? JoinDate { get; init; }
        public DateTime? LeaveDate { get; init; }
        public string? Address { get; init; }
        public string? EmergencyContactName { get; init; }
        public string? EmergencyContactPhone { get; init; }
        public string? ProfilePhotoUrl { get; init; }
        public string? ShiftStartTime { get; init; }
        public string? ShiftEndTime { get; init; }
    }

    public sealed class TenantDocumentDto
    {
        public required string Id { get; init; }
        public required TenantDocumentType Type { get; init; }
        public required string FileName { get; init; }
        public required string Url { get; init; }
    }

    public sealed class TenantDto
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string Code { get; init; }
        public required string Email { get; init; }
        public required string Phone { get; init; }
        public required string Address { get; init; }
        public string? ContactPerson { get; init; }
        public string? LogoUrl { get; init; }
        public List<TenantDocumentDto>? Documents { get; init; }
        public List<AppModuleKey>? EnabledModules { get; init; }
        public List<string> TermsAndConditions { get; init; } = [];
        public List<string> CancellationPolicy { get; init; } = [];
        public List<string> SupplementCosts { get; init; } = [];
        public required bool IsActive { get; init; }
        public required DateTime CreatedAt { get; init; }
        public string? DefaultUserId { get; init; }
        public string? PlanId { get; init; }
        public string? BillingCycle { get; init; }
        public int SeatsPurchased { get; init; }
        public string? SubscriptionStatus { get; init; }
        public DateTime? SubscriptionStartUtc { get; init; }
        public DateTime? SubscriptionEndUtc { get; init; }
        public bool InboundLeadsFeatureEnabled { get; init; }
    }

    public sealed class SessionResponseDto
    {
        public required UserDto User { get; init; }
        public required TenantDto Tenant { get; init; }
    }

    public sealed class LoginResponseDto
    {
        public required string Token { get; init; }
        public required UserDto User { get; init; }
        public required TenantDto Tenant { get; init; }
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email and password are required." });
        }

        var email = request.Email.Trim();
        Guid? requestedTenantId = null;
        if (!string.IsNullOrWhiteSpace(request.TenantId) && Guid.TryParse(request.TenantId.Trim(), out var parsedTid))
            requestedTenantId = parsedTid;

        try
        {
            // Find user by email (and optionally by tenant when tenant ID is provided). Super Admin has no tenant so allow them when tenantId is in URL.
            var user = requestedTenantId.HasValue
                ? await _db.Users.AsNoTracking().IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => !u.IsDeleted && u.Email == email && (u.TenantId == requestedTenantId || u.Role == UserRole.SuperAdmin), ct)
                : await _db.Users.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(u => !u.IsDeleted && u.Email == email, ct);

            if (user is null || !user.IsActive || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            {
                // Log reason for 401 only (no credentials) to help diagnose live issues
                if (user is null)
                    _logger.LogWarning("Login failed: no user found for email {Email}", email);
                else if (!user.IsActive)
                    _logger.LogWarning("Login failed: user {UserId} is inactive", user.Id);
                else
                    _logger.LogWarning("Login failed: password mismatch for user {UserId}", user.Id);

                var msg = requestedTenantId.HasValue
                    ? "Invalid credentials, or this account is not for the selected tenant. Use an email and password for a user of this tenant."
                    : "Invalid credentials.";
                return Unauthorized(new { message = msg });
            }

            // Tenant checks for tenant users
            Data.Entities.Tenant? tenant = null;
            if (user.Role != UserRole.SuperAdmin)
            {
                // When tenantId was provided we already found the user in that tenant; no extra check needed

                // Optional: validate tenantCode if provided
                if (!string.IsNullOrWhiteSpace(request.TenantCode))
                {
                    tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Code == request.TenantCode, ct);
                    if (tenant is null || user.TenantId != tenant.Id)
                    {
                        return Unauthorized(new { message = "Invalid credentials." });
                    }
                }

                tenant ??= await _db.Tenants
                    .AsNoTracking()
                    .Include(t => t.Documents)
                    .FirstOrDefaultAsync(t => t.Id == user.TenantId, ct);

                if (tenant is null || !tenant.IsActive)
                {
                    return Unauthorized(new { message = "Tenant is inactive or not found." });
                }

                // When subscription is expired or past due, only the default user (agency admin) can log in
                if (tenant.SubscriptionStatus == SubscriptionStatus.Expired ||
                    tenant.SubscriptionStatus == SubscriptionStatus.PastDue)
                {
                    if (tenant.DefaultUserId != user.Id)
                    {
                        return Unauthorized(new { message = "Subscription has expired. Please contact your agency admin to renew." });
                    }
                }
            }

            var token = _tokens.CreateToken(user);

            // For Super Admin, do NOT load any agency/tenant record here.
            // Super Admin should only see an agency name when they explicitly scope context via X-Tenant-Id.

            var tenantDto = MapTenantDto(tenant, user.CreatedAt);
            var userDto = MapUserDto(user);

            return Ok(new LoginResponseDto { Token = token, User = userDto, Tenant = tenantDto });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for email {Email}", email);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "A server error occurred during sign-in. Please try again." });
        }
    }

    public sealed class ChangePasswordRequestDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    /// <summary>Refresh current user and tenant (including enabled modules) without re-login.</summary>
    [Authorize]
    [HttpGet("session")]
    public async Task<ActionResult<SessionResponseDto>> GetSession(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null || !user.IsActive)
            return Unauthorized();

        Data.Entities.Tenant? tenant = null;
        if (user.TenantId.HasValue)
        {
            tenant = await _db.Tenants.AsNoTracking()
                .Include(t => t.Documents)
                .FirstOrDefaultAsync(t => t.Id == user.TenantId.Value && !t.IsDeleted, ct);
        }

        return Ok(new SessionResponseDto
        {
            User = MapUserDto(user),
            Tenant = MapTenantDto(tenant, user.CreatedAt)
        });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return BadRequest(new { message = "New password must be at least 6 characters." });

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return Unauthorized();

        if (!PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Current password is incorrect." });

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        user.PasswordEncrypted = _passwordEncryption.Encrypt(request.NewPassword);
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "Password changed successfully." });
    }

    /// <summary>Current user's read-only profile (employee details).</summary>
    [Authorize]
    [HttpGet("profile")]
    public async Task<ActionResult<UserProfileDto>> GetProfile(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync(ct);
        if (user is null) return Unauthorized();
        return Ok(MapProfileDto(user));
    }

    /// <summary>Upload or replace the current user's profile photo.</summary>
    [Authorize]
    [HttpPost("profile/photo")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UserProfileDto>> UploadProfilePhoto(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Profile photo file is required." });

        var userId = await GetCurrentUserIdAsync(ct);
        if (!userId.HasValue) return Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null || !user.IsActive) return Unauthorized();

        try
        {
            user.ProfilePhotoUrl = await _storage.SaveUserProfilePhotoAsync(user.TenantId, user.Id, file, ct);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Profile photo upload failed for user {UserId}", user.Id);
            return BadRequest(new { message = "Could not save profile photo. Use a JPG or PNG image." });
        }

        return Ok(MapProfileDto(user));
    }

    private async Task<Guid?> GetCurrentUserIdAsync(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return null;
        return userId;
    }

    private async Task<Data.Entities.AppUser?> GetCurrentUserAsync(CancellationToken ct)
    {
        var userId = await GetCurrentUserIdAsync(ct);
        if (!userId.HasValue) return null;
        return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted && u.IsActive, ct);
    }

    private static UserProfileDto MapProfileDto(Data.Entities.AppUser user) =>
        new()
        {
            Id = user.Id.ToString("D"),
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            TenantId = user.TenantId?.ToString("D") ?? string.Empty,
            Role = user.Role,
            Department = user.Department,
            Designation = user.Designation,
            Phone = user.Phone,
            DateOfBirth = user.DateOfBirth,
            JoinDate = user.JoinDate,
            LeaveDate = user.LeaveDate,
            Address = user.Address,
            EmergencyContactName = user.EmergencyContactName,
            EmergencyContactPhone = user.EmergencyContactPhone,
            ProfilePhotoUrl = user.ProfilePhotoUrl,
            ShiftStartTime = UserShiftTimeHelper.Format(user.ShiftStartTime),
            ShiftEndTime = UserShiftTimeHelper.Format(user.ShiftEndTime)
        };

    private static UserDto MapUserDto(Data.Entities.AppUser user) =>
        new()
        {
            Id = user.Id.ToString("D"),
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            TenantId = user.TenantId?.ToString("D") ?? string.Empty,
            Role = user.Role,
            Department = user.Department,
            AllowedModules = user.AllowedModules?.ToList() ?? [],
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            CanViewCostBifurcation = user.CanViewCostBifurcation,
            CanPriceOverride = user.CanPriceOverride,
            ActivityTrackingEnabled = user.ActivityTrackingEnabled,
            Phone = user.Phone,
            DateOfBirth = user.DateOfBirth,
            JoinDate = user.JoinDate,
            LeaveDate = user.LeaveDate,
            Designation = user.Designation,
            Address = user.Address,
            EmergencyContactName = user.EmergencyContactName,
            EmergencyContactPhone = user.EmergencyContactPhone,
            ProfilePhotoUrl = user.ProfilePhotoUrl,
            ShiftStartTime = UserShiftTimeHelper.Format(user.ShiftStartTime),
            ShiftEndTime = UserShiftTimeHelper.Format(user.ShiftEndTime)
        };

    private static TenantDto MapTenantDto(Data.Entities.Tenant? tenant, DateTime fallbackCreatedAt) =>
        tenant is null
            ? new TenantDto
            {
                Id = string.Empty,
                Name = "Platform",
                Code = "PLATFORM",
                Email = string.Empty,
                Phone = string.Empty,
                Address = string.Empty,
                ContactPerson = null,
                LogoUrl = null,
                Documents = [],
                EnabledModules = [],
                TermsAndConditions = [],
                CancellationPolicy = [],
                SupplementCosts = [],
                IsActive = true,
                CreatedAt = fallbackCreatedAt,
                InboundLeadsFeatureEnabled = false
            }
            : new TenantDto
            {
                Id = tenant.Id.ToString("D"),
                Name = tenant.Name,
                Code = tenant.Code,
                Email = tenant.Email,
                Phone = tenant.Phone,
                Address = tenant.Address,
                ContactPerson = tenant.ContactPerson,
                LogoUrl = tenant.LogoUrl,
                Documents = (tenant.Documents ?? []).Select(d => new TenantDocumentDto
                {
                    Id = d.Id.ToString("D"),
                    Type = d.Type,
                    FileName = d.FileName,
                    Url = d.Url
                }).ToList(),
                EnabledModules = (tenant.EnabledModules ?? []).ToList(),
                TermsAndConditions = tenant.TermsAndConditions ?? [],
                CancellationPolicy = tenant.CancellationPolicy ?? [],
                SupplementCosts = tenant.SupplementCosts ?? [],
                IsActive = tenant.IsActive,
                CreatedAt = tenant.CreatedAt,
                DefaultUserId = tenant.DefaultUserId?.ToString("D"),
                PlanId = tenant.PlanId?.ToString("D"),
                BillingCycle = tenant.BillingCycle?.ToString(),
                SeatsPurchased = tenant.SeatsPurchased,
                SubscriptionStatus = tenant.SubscriptionStatus.ToString(),
                SubscriptionStartUtc = tenant.SubscriptionStartUtc,
                SubscriptionEndUtc = tenant.SubscriptionEndUtc,
                InboundLeadsFeatureEnabled = tenant.InboundLeadsFeatureEnabled
            };
}

