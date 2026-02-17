using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Auth;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.Services;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize(Policy = "SuperAdminOnly")]
[Route("api/admin/tenants/{tenantId:guid}/users")]
public sealed class AdminTenantUsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;

    public AdminTenantUsersController(AppDbContext db, IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    public sealed class TenantUserDto
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
    }

    public sealed class CreateTenantUserRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Admin;
        public UserDepartment? Department { get; set; }
        public string Password { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public List<AppModuleKey>? AllowedModules { get; set; }
    }

    public sealed class UpdateTenantUserRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Agent;
        public UserDepartment? Department { get; set; }
        public string? Password { get; set; }
        public bool IsActive { get; set; } = true;
        public List<AppModuleKey>? AllowedModules { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<TenantUserDto>>>> GetUsers([FromRoute] Guid tenantId, CancellationToken ct)
    {
        var users = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(ct);

        return ApiResponse<List<TenantUserDto>>.Ok(users.Select(ToDto).ToList());
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<ApiResponse<TenantUserDto>>> GetUserById([FromRoute] Guid tenantId, [FromRoute] Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, ct);
        if (user is null) return NotFound(ApiResponse<TenantUserDto>.Fail("User not found"));
        return ApiResponse<TenantUserDto>.Ok(ToDto(user));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<TenantUserDto>>> CreateUser([FromRoute] Guid tenantId, [FromBody] CreateTenantUserRequestDto request, CancellationToken ct)
    {
        if (request.Role == UserRole.SuperAdmin) return BadRequest(ApiResponse<TenantUserDto>.Fail("Role not allowed."));
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(ApiResponse<TenantUserDto>.Fail("Email and password are required."));
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return BadRequest(ApiResponse<TenantUserDto>.Fail("Tenant not found."));

        if (tenant.SeatsPurchased > 0)
        {
            var activeCount = await _db.Users.CountAsync(u => u.TenantId == tenantId && u.IsActive, ct);
            if (activeCount >= tenant.SeatsPurchased)
            {
                return BadRequest(ApiResponse<TenantUserDto>.Fail($"Seat limit reached ({tenant.SeatsPurchased} seats). Upgrade the subscription to add more users."));
            }
        }

        var exists = await _db.Users.AnyAsync(u => u.Email == request.Email, ct);
        if (exists) return BadRequest(ApiResponse<TenantUserDto>.Fail("Email already exists."));

        var user = new AppUser
        {
            TenantId = tenantId,
            Email = request.Email.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = request.Role,
            Department = request.Department,
            IsActive = request.IsActive,
            AllowedModules = request.AllowedModules?.ToList() ?? [],
            PasswordHash = PasswordHasher.Hash(request.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        if (tenant.DefaultUserId is null)
        {
            tenant.DefaultUserId = user.Id;
            await _db.SaveChangesAsync(ct);
        }

        var emailSent = await _emailService.SendWelcomeEmailAsync(
            user.Email,
            user.FirstName,
            tenant.Name,
            user.Email,
            request.Password,
            ct);

        var response = ApiResponse<TenantUserDto>.Ok(ToDto(user));
        if (!emailSent)
            response = ApiResponse<TenantUserDto>.Ok(ToDto(user), "User created. Could not send welcome email; please share the login details with the user manually.");

        return CreatedAtAction(nameof(GetUserById), new { tenantId, userId = user.Id }, response);
    }

    [HttpPut("{userId:guid}")]
    public async Task<ActionResult<ApiResponse<TenantUserDto>>> UpdateUser([FromRoute] Guid tenantId, [FromRoute] Guid userId, [FromBody] UpdateTenantUserRequestDto request, CancellationToken ct)
    {
        if (request.Role == UserRole.SuperAdmin) return BadRequest(ApiResponse<TenantUserDto>.Fail("Role not allowed."));

        var user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, ct);
        if (user is null) return NotFound(ApiResponse<TenantUserDto>.Fail("User not found"));

        user.Email = request.Email.Trim();
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Role = request.Role;
        user.Department = request.Department;
        user.IsActive = request.IsActive;
        user.AllowedModules = request.AllowedModules?.ToList() ?? user.AllowedModules ?? [];

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = PasswordHasher.Hash(request.Password);
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse<TenantUserDto>.Ok(ToDto(user));
    }

    [HttpDelete("{userId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteUser([FromRoute] Guid tenantId, [FromRoute] Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, ct);
        if (user is null) return NotFound(ApiResponse<object>.Fail("User not found"));

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    private static TenantUserDto ToDto(AppUser u) =>
        new()
        {
            Id = u.Id.ToString("D"),
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            TenantId = u.TenantId?.ToString("D") ?? string.Empty,
            Role = u.Role,
            Department = u.Department,
            AllowedModules = u.AllowedModules?.ToList(),
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt
        };
}

