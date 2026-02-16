using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Auth;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using SubscriptionStatus = TravelPathways.Api.Common.SubscriptionStatus;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokens;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext db, TokenService tokens, ILogger<AuthController> logger)
    {
        _db = db;
        _tokens = tokens;
        _logger = logger;
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
        public List<AppModuleKey>? AllowedModules { get; init; }
        public required bool IsActive { get; init; }
        public required DateTime CreatedAt { get; init; }
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

        try
        {
            // Find user by email. Email is unique in DB.
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);
            if (user is null || !user.IsActive || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid credentials." });
            }

            // Tenant checks for tenant users
            Data.Entities.Tenant? tenant = null;
            if (user.Role != UserRole.SuperAdmin)
            {
                // Optional: validate explicit tenantId if provided
                if (!string.IsNullOrWhiteSpace(request.TenantId) &&
                    Guid.TryParse(request.TenantId, out var reqTid) &&
                    user.TenantId != reqTid)
                {
                    return Unauthorized(new { message = "Invalid credentials." });
                }

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

            // For Super Admin, tenant is null; use first tenant from Tenants table so UI shows agency name
            if (tenant is null)
            {
                tenant = await _db.Tenants.AsNoTracking()
                    .Include(t => t.Documents)
                    .OrderBy(t => t.Name)
                    .FirstOrDefaultAsync(ct);
            }

            var tenantDto = tenant is null
                ? new TenantDto
                {
                    Id = string.Empty,
                    Name = "Travel Pathways",
                    Code = "SUPER",
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
                AllowedModules = user.AllowedModules?.ToList() ?? [],
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
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
        await _db.SaveChangesAsync(ct);

        return Ok(new { message = "Password changed successfully." });
    }
}

