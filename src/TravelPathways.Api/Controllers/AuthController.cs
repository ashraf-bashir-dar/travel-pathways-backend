using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TravelPathways.Api.Auth;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Services;
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

    public AuthController(AppDbContext db, TokenService tokens, ILogger<AuthController> logger, IPasswordEncryption passwordEncryption)
    {
        _db = db;
        _tokens = tokens;
        _logger = logger;
        _passwordEncryption = passwordEncryption;
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
    }

    public sealed class TenantDocumentDto
    {
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
        public required bool IsActive { get; init; }
        public required DateTime CreatedAt { get; init; }
        public string? DefaultUserId { get; init; }
        public string? PlanId { get; init; }
        public string? BillingCycle { get; init; }
        public int SeatsPurchased { get; init; }
        public string? SubscriptionStatus { get; init; }
        public DateTime? SubscriptionStartUtc { get; init; }
        public DateTime? SubscriptionEndUtc { get; init; }
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

            var tenantDto = tenant is null
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
                    IsActive = true,
                    CreatedAt = user.CreatedAt
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
                        Type = d.Type,
                        FileName = d.FileName,
                        Url = d.Url
                    }).ToList(),
                    EnabledModules = (tenant.EnabledModules ?? []).ToList(),
                    IsActive = tenant.IsActive,
                    CreatedAt = tenant.CreatedAt,
                    DefaultUserId = tenant.DefaultUserId?.ToString("D"),
                    PlanId = tenant.PlanId?.ToString("D"),
                    BillingCycle = tenant.BillingCycle?.ToString(),
                    SeatsPurchased = tenant.SeatsPurchased,
                    SubscriptionStatus = tenant.SubscriptionStatus.ToString(),
                    SubscriptionStartUtc = tenant.SubscriptionStartUtc,
                    SubscriptionEndUtc = tenant.SubscriptionEndUtc
                };

            var userDto = new UserDto
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
                CanViewCostBifurcation = user.CanViewCostBifurcation
            };

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
}

