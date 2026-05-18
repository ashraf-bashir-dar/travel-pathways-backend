using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.Hubs;
using TravelPathways.Api.MultiTenancy;
using TravelPathways.Api.Storage;
using Microsoft.Extensions.Configuration;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/chat")]
public sealed class ChatController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hub;
    private readonly FileStorage _storage;
    private readonly IConfiguration _configuration;

    public ChatController(
        AppDbContext db,
        TenantContext tenant,
        IHubContext<ChatHub> hub,
        FileStorage storage,
        IConfiguration configuration) : base(tenant)
    {
        _db = db;
        _hub = hub;
        _storage = storage;
        _configuration = configuration;
    }

    public sealed class ChatGroupDto
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public string? Description { get; init; }
        public bool IsDirect { get; init; }
        public string? OtherUserId { get; init; }
        public required int MemberCount { get; init; }
        public int UnreadCount { get; init; }
        public ChatMessagePayload? LastMessage { get; init; }
        public required DateTime CreatedAt { get; init; }
    }

    public sealed class StartDirectRequest
    {
        public string OtherUserId { get; set; } = string.Empty;
    }

    public sealed class ChatGroupMemberDto
    {
        public required string UserId { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public string? Email { get; init; }
        public string? Designation { get; init; }
        public required DateTime JoinedAt { get; init; }
    }

    public sealed class CreateChatGroupRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<string> MemberUserIds { get; set; } = [];
    }

    public sealed class UpdateChatGroupRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public sealed class SetMembersRequest
    {
        public List<string> UserIds { get; set; } = [];
    }

    public sealed class MarkReadRequest
    {
        public DateTime? ReadAtUtc { get; set; }
    }

    [HttpGet("groups")]
    public async Task<ActionResult<ApiResponse<List<ChatGroupDto>>>> ListGroups(CancellationToken ct)
    {
        var gate = await EnsureChatAccessAsync(ct);
        if (gate is not null) return gate;

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(ApiResponse<List<ChatGroupDto>>.Fail("Unauthorized."));
        var uid = userId.Value;

        var groups = await _db.ChatGroups.AsNoTracking()
            .Where(g => g.TenantId == TenantId && g.Members.Any(m => m.UserId == uid))
            .OrderByDescending(g => g.UpdatedAt)
            .Select(g => new GroupListRow
            {
                Id = g.Id,
                IsDirect = g.IsDirect,
                Name = g.Name,
                Description = g.Description,
                CreatedAt = g.CreatedAt,
                MemberCount = g.Members.Count,
                MyLastRead = g.Members.Where(m => m.UserId == uid).Select(m => m.LastReadAtUtc).FirstOrDefault(),
                OtherUserId = g.IsDirect
                    ? g.Members.Where(m => m.UserId != uid).Select(m => (Guid?)m.UserId).FirstOrDefault()
                    : null,
                OtherFirstName = g.IsDirect
                    ? g.Members.Where(m => m.UserId != uid).Select(m => m.User!.FirstName).FirstOrDefault()
                    : null,
                OtherLastName = g.IsDirect
                    ? g.Members.Where(m => m.UserId != uid).Select(m => m.User!.LastName).FirstOrDefault()
                    : null,
                LastMsg = g.Messages.Where(m => !m.IsDeleted).OrderByDescending(m => m.SentAtUtc).Select(m => new LastMessageRow
                {
                    Id = m.Id,
                    GroupId = m.GroupId,
                    SenderUserId = m.SenderUserId,
                    Body = m.Body,
                    SentAtUtc = m.SentAtUtc,
                    SenderFirst = m.SenderUser!.FirstName,
                    SenderLast = m.SenderUser!.LastName,
                    MentionedUserIds = m.MentionedUserIds,
                    ImageUrls = m.ImageUrls
                }).FirstOrDefault()
            })
            .ToListAsync(ct);

        var dtos = new List<ChatGroupDto>();
        foreach (var g in groups)
            dtos.Add(await MapGroupDtoAsync(g, uid, ct));

        return ApiResponse<List<ChatGroupDto>>.Ok(dtos);
    }

    [HttpGet("groups/{id:guid}")]
    public async Task<ActionResult<ApiResponse<ChatGroupDto>>> GetGroup([FromRoute] Guid id, CancellationToken ct)
    {
        var gate = await EnsureChatAccessAsync(ct);
        if (gate is not null) return gate;

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(ApiResponse<ChatGroupDto>.Fail("Unauthorized."));
        var uid = userId.Value;
        if (!await IsMemberAsync(id, uid, ct))
            return NotFound(ApiResponse<ChatGroupDto>.Fail("Group not found."));

        var g = await _db.ChatGroups.AsNoTracking()
            .Where(x => x.Id == id && x.TenantId == TenantId)
            .Select(x => new GroupListRow
            {
                Id = x.Id,
                IsDirect = x.IsDirect,
                Name = x.Name,
                Description = x.Description,
                CreatedAt = x.CreatedAt,
                MemberCount = x.Members.Count,
                MyLastRead = x.Members.Where(m => m.UserId == uid).Select(m => m.LastReadAtUtc).FirstOrDefault(),
                OtherUserId = x.IsDirect
                    ? x.Members.Where(m => m.UserId != uid).Select(m => (Guid?)m.UserId).FirstOrDefault()
                    : null,
                OtherFirstName = x.IsDirect
                    ? x.Members.Where(m => m.UserId != uid).Select(m => m.User!.FirstName).FirstOrDefault()
                    : null,
                OtherLastName = x.IsDirect
                    ? x.Members.Where(m => m.UserId != uid).Select(m => m.User!.LastName).FirstOrDefault()
                    : null,
                LastMsg = x.Messages.Where(m => !m.IsDeleted).OrderByDescending(m => m.SentAtUtc).Select(m => new LastMessageRow
                {
                    Id = m.Id,
                    GroupId = m.GroupId,
                    SenderUserId = m.SenderUserId,
                    Body = m.Body,
                    SentAtUtc = m.SentAtUtc,
                    SenderFirst = m.SenderUser!.FirstName,
                    SenderLast = m.SenderUser!.LastName,
                    MentionedUserIds = m.MentionedUserIds,
                    ImageUrls = m.ImageUrls
                }).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (g is null) return NotFound(ApiResponse<ChatGroupDto>.Fail("Group not found."));

        return ApiResponse<ChatGroupDto>.Ok(await MapGroupDtoAsync(g, uid, ct));
    }

    [HttpPost("direct")]
    public async Task<ActionResult<ApiResponse<ChatGroupDto>>> StartDirect([FromBody] StartDirectRequest request, CancellationToken ct)
    {
        var gate = await EnsureChatAccessAsync(ct);
        if (gate is not null) return gate;

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(ApiResponse<ChatGroupDto>.Fail("Unauthorized."));
        var uid = userId.Value;

        if (!Guid.TryParse(request.OtherUserId, out var otherId))
            return BadRequest(ApiResponse<ChatGroupDto>.Fail("Invalid user id."));
        if (otherId == uid)
            return BadRequest(ApiResponse<ChatGroupDto>.Fail("You cannot start a direct chat with yourself."));

        var other = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == TenantId && u.Id == otherId && u.IsActive && !u.IsDeleted, ct);
        if (other is null)
            return NotFound(ApiResponse<ChatGroupDto>.Fail("User not found in this agency."));

        var pairKey = ChatDirectHelper.BuildPairKey(uid, otherId);
        var existing = await _db.ChatGroups.AsNoTracking()
            .Where(g => g.TenantId == TenantId && g.IsDirect && g.DirectPairKey == pairKey && !g.IsDeleted)
            .Select(g => g.Id)
            .FirstOrDefaultAsync(ct);

        if (existing != Guid.Empty)
            return await GetGroup(existing, ct);

        var displayName = $"{other.FirstName} {other.LastName}".Trim();
        var group = new ChatGroup
        {
            TenantId = TenantId,
            Name = displayName,
            IsDirect = true,
            DirectPairKey = pairKey,
            CreatedByUserId = uid,
            IsActive = true
        };

        _db.ChatGroups.Add(group);
        _db.ChatGroupMembers.Add(new ChatGroupMember { GroupId = group.Id, UserId = uid, AddedByUserId = uid, JoinedAt = DateTime.UtcNow });
        _db.ChatGroupMembers.Add(new ChatGroupMember { GroupId = group.Id, UserId = otherId, AddedByUserId = uid, JoinedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(ct);

        return await GetGroup(group.Id, ct);
    }

    [HttpGet("colleagues")]
    public async Task<ActionResult<ApiResponse<List<ChatGroupMemberDto>>>> ListColleagues(CancellationToken ct)
    {
        var gate = await EnsureChatAccessAsync(ct);
        if (gate is not null) return gate;

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(ApiResponse<List<ChatGroupMemberDto>>.Fail("Unauthorized."));

        var users = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == TenantId && u.IsActive && !u.IsDeleted && u.Id != userId.Value)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Select(u => new ChatGroupMemberDto
            {
                UserId = u.Id.ToString("D"),
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                Designation = u.Designation,
                JoinedAt = u.CreatedAt
            })
            .ToListAsync(ct);

        return ApiResponse<List<ChatGroupMemberDto>>.Ok(users);
    }

    [HttpPost("groups")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<ChatGroupDto>>> CreateGroup([FromBody] CreateChatGroupRequest request, CancellationToken ct)
    {
        var gate = await EnsureChatAccessAsync(ct);
        if (gate is not null) return gate;

        var adminId = GetCurrentUserId();
        if (!adminId.HasValue)
            return Unauthorized(ApiResponse<ChatGroupDto>.Fail("Unauthorized."));
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
            return BadRequest(ApiResponse<ChatGroupDto>.Fail("Group name is required."));

        var memberIds = await ResolveMemberUserIdsAsync(request.MemberUserIds, ct);
        if (!memberIds.Contains(adminId.Value))
            memberIds.Add(adminId.Value);

        var group = new ChatGroup
        {
            TenantId = TenantId,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedByUserId = adminId.Value,
            IsDirect = false,
            DirectPairKey = null,
            IsActive = true
        };

        _db.ChatGroups.Add(group);
        foreach (var uid in memberIds)
        {
            _db.ChatGroupMembers.Add(new ChatGroupMember
            {
                GroupId = group.Id,
                UserId = uid,
                AddedByUserId = adminId.Value,
                JoinedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetGroup), new { id = group.Id }, ApiResponse<ChatGroupDto>.Ok(new ChatGroupDto
        {
            Id = group.Id.ToString("D"),
            Name = group.Name,
            Description = group.Description,
            IsDirect = false,
            OtherUserId = null,
            MemberCount = memberIds.Count,
            UnreadCount = 0,
            LastMessage = null,
            CreatedAt = group.CreatedAt
        }));
    }

    [HttpPut("groups/{id:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<ChatGroupDto>>> UpdateGroup([FromRoute] Guid id, [FromBody] UpdateChatGroupRequest request, CancellationToken ct)
    {
        var gate = await EnsureChatAccessAsync(ct);
        if (gate is not null) return gate;

        var group = await _db.ChatGroups.FirstOrDefaultAsync(g => g.Id == id && g.TenantId == TenantId, ct);
        if (group is null) return NotFound(ApiResponse<ChatGroupDto>.Fail("Group not found."));
        if (group.IsDirect)
            return BadRequest(ApiResponse<ChatGroupDto>.Fail("Direct conversations cannot be renamed."));

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
            return BadRequest(ApiResponse<ChatGroupDto>.Fail("Group name is required."));

        group.Name = name;
        group.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        await _db.SaveChangesAsync(ct);

        return await GetGroup(id, ct);
    }

    [HttpDelete("groups/{id:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteGroup([FromRoute] Guid id, CancellationToken ct)
    {
        var gate = await EnsureChatAccessAsync(ct);
        if (gate is not null) return gate;

        var group = await _db.ChatGroups.FirstOrDefaultAsync(g => g.Id == id && g.TenantId == TenantId, ct);
        if (group is null) return NotFound(ApiResponse<object>.Fail("Group not found."));
        if (group.IsDirect)
            return BadRequest(ApiResponse<object>.Fail("Direct conversations cannot be deleted from the admin panel."));

        group.IsDeleted = true;
        group.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    [HttpGet("groups/{id:guid}/members")]
    public async Task<ActionResult<ApiResponse<List<ChatGroupMemberDto>>>> GetMembers([FromRoute] Guid id, CancellationToken ct)
    {
        var gate = await EnsureChatAccessAsync(ct);
        if (gate is not null) return gate;

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(ApiResponse<List<ChatGroupMemberDto>>.Fail("Unauthorized."));
        if (!await IsMemberAsync(id, userId.Value, ct))
            return NotFound(ApiResponse<List<ChatGroupMemberDto>>.Fail("Group not found."));

        var members = await _db.ChatGroupMembers.AsNoTracking()
            .Where(m => m.GroupId == id)
            .OrderBy(m => m.User!.FirstName)
            .Select(m => new ChatGroupMemberDto
            {
                UserId = m.UserId.ToString("D"),
                FirstName = m.User!.FirstName,
                LastName = m.User!.LastName,
                Email = m.User.Email,
                Designation = m.User.Designation,
                JoinedAt = m.JoinedAt
            })
            .ToListAsync(ct);

        return ApiResponse<List<ChatGroupMemberDto>>.Ok(members);
    }

    [HttpPut("groups/{id:guid}/members")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<List<ChatGroupMemberDto>>>> SetMembers([FromRoute] Guid id, [FromBody] SetMembersRequest request, CancellationToken ct)
    {
        var gate = await EnsureChatAccessAsync(ct);
        if (gate is not null) return gate;

        var adminId = GetCurrentUserId();
        if (!adminId.HasValue)
            return Unauthorized(ApiResponse<List<ChatGroupMemberDto>>.Fail("Unauthorized."));
        var group = await _db.ChatGroups.FirstOrDefaultAsync(g => g.Id == id && g.TenantId == TenantId, ct);
        if (group is null) return NotFound(ApiResponse<List<ChatGroupMemberDto>>.Fail("Group not found."));
        if (group.IsDirect)
            return BadRequest(ApiResponse<List<ChatGroupMemberDto>>.Fail("Direct conversation membership is fixed (two participants)."));

        var desired = await ResolveMemberUserIdsAsync(request.UserIds, ct);
        if (!desired.Contains(adminId.Value))
            desired.Add(adminId.Value);

        var existing = await _db.ChatGroupMembers.Where(m => m.GroupId == id).ToListAsync(ct);
        var existingIds = existing.Select(m => m.UserId).ToHashSet();
        var desiredSet = desired.ToHashSet();

        foreach (var row in existing.Where(m => !desiredSet.Contains(m.UserId)))
            _db.ChatGroupMembers.Remove(row);

        foreach (var uid in desired.Where(uid => !existingIds.Contains(uid)))
        {
            _db.ChatGroupMembers.Add(new ChatGroupMember
            {
                GroupId = id,
                UserId = uid,
                AddedByUserId = adminId.Value,
                JoinedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        return await GetMembers(id, ct);
    }

    [HttpGet("groups/{id:guid}/messages")]
    public async Task<ActionResult<ApiResponse<List<ChatMessagePayload>>>> GetMessages(
        [FromRoute] Guid id,
        [FromQuery] DateTime? beforeUtc,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var gate = await EnsureChatAccessAsync(ct);
        if (gate is not null) return gate;

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(ApiResponse<List<ChatMessagePayload>>.Fail("Unauthorized."));
        if (!await IsMemberAsync(id, userId.Value, ct))
            return NotFound(ApiResponse<List<ChatMessagePayload>>.Fail("Group not found."));

        limit = Math.Clamp(limit, 1, 100);

        var query = _db.ChatMessages.AsNoTracking()
            .Where(m => m.GroupId == id && !m.IsDeleted);

        if (beforeUtc.HasValue)
            query = query.Where(m => m.SentAtUtc < beforeUtc.Value);

        var rows = await query
            .OrderByDescending(m => m.SentAtUtc)
            .Take(limit)
            .Select(m => new
            {
                m.Id,
                m.GroupId,
                m.SenderUserId,
                SenderFirst = m.SenderUser!.FirstName,
                SenderLast = m.SenderUser.LastName,
                m.Body,
                m.SentAtUtc,
                m.MentionedUserIds,
                m.ImageUrls
            })
            .ToListAsync(ct);

        var messages = rows.Select(m => ChatMessageMapper.ToPayload(
                new ChatMessage
                {
                    Id = m.Id,
                    GroupId = m.GroupId,
                    SenderUserId = m.SenderUserId,
                    Body = m.Body,
                    SentAtUtc = m.SentAtUtc,
                    MentionedUserIds = m.MentionedUserIds,
                    ImageUrls = m.ImageUrls
                },
                (m.SenderFirst + " " + m.SenderLast).Trim(),
                _configuration,
                HttpContext))
            .ToList();

        messages.Reverse();
        return ApiResponse<List<ChatMessagePayload>>.Ok(messages);
    }

    [HttpPost("groups/{id:guid}/messages")]
    public async Task<ActionResult<ApiResponse<ChatMessagePayload>>> PostMessage(
        [FromRoute] Guid id,
        [FromBody] PostMessageRequest request,
        CancellationToken ct)
    {
        var gate = await EnsureChatAccessAsync(ct);
        if (gate is not null) return gate;

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(ApiResponse<ChatMessagePayload>.Fail("Unauthorized."));
        var uid = userId.Value;
        if (!await IsMemberAsync(id, uid, ct))
            return NotFound(ApiResponse<ChatMessagePayload>.Fail("Group not found."));

        var trimmed = (request.Body ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            return BadRequest(ApiResponse<ChatMessagePayload>.Fail("Message cannot be empty."));
        if (trimmed.Length > 4000)
            return BadRequest(ApiResponse<ChatMessagePayload>.Fail("Message is too long."));

        var groupMeta = await _db.ChatGroups.AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new { g.IsDirect })
            .FirstOrDefaultAsync(ct);
        if (groupMeta is null)
            return NotFound(ApiResponse<ChatMessagePayload>.Fail("Group not found."));

        var memberIds = await _db.ChatGroupMembers.AsNoTracking()
            .Where(m => m.GroupId == id)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        var mentions = groupMeta.IsDirect
            ? []
            : ChatMentionHelper.ParseAndValidate(request.MentionedUserIds, memberIds);

        var sender = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == uid, ct);
        var message = new ChatMessage
        {
            GroupId = id,
            SenderUserId = uid,
            Body = trimmed,
            MentionedUserIds = mentions,
            SentAtUtc = DateTime.UtcNow
        };
        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync(ct);

        var dto = ChatMessageMapper.ToPayload(
            message,
            $"{sender.FirstName} {sender.LastName}".Trim(),
            _configuration,
            HttpContext);

        await _hub.Clients.Group(ChatHub.GroupChannel(id)).SendAsync("MessageReceived", dto, ct);
        return ApiResponse<ChatMessagePayload>.Ok(dto);
    }

    /// <summary>Send a message with one or more images (optional caption in body). Form: body, mentionedUserIds (JSON array), files.</summary>
    [HttpPost("groups/{id:guid}/messages/media")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(32 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ChatMessagePayload>>> PostMessageWithMedia(
        [FromRoute] Guid id,
        [FromForm] string? body,
        [FromForm] string? mentionedUserIds,
        [FromForm(Name = "files")] IFormFile[]? files,
        CancellationToken ct)
    {
        var gate = await EnsureChatAccessAsync(ct);
        if (gate is not null) return gate;

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(ApiResponse<ChatMessagePayload>.Fail("Unauthorized."));
        var uid = userId.Value;
        if (!await IsMemberAsync(id, uid, ct))
            return NotFound(ApiResponse<ChatMessagePayload>.Fail("Group not found."));

        var uploads = (files ?? []).Where(f => f.Length > 0).ToList();
        if (uploads.Count == 0)
            return BadRequest(ApiResponse<ChatMessagePayload>.Fail("At least one image is required."));
        if (uploads.Count > ChatMessageMapper.MaxImagesPerMessage)
            return BadRequest(ApiResponse<ChatMessagePayload>.Fail($"You can attach up to {ChatMessageMapper.MaxImagesPerMessage} images per message."));

        foreach (var file in uploads)
        {
            if (!ChatMessageMapper.IsAllowedChatImage(file))
                return BadRequest(ApiResponse<ChatMessagePayload>.Fail("Images must be JPEG, PNG, GIF, or WebP and at most 5 MB each."));
        }

        var trimmed = (body ?? string.Empty).Trim();
        if (trimmed.Length > 4000)
            return BadRequest(ApiResponse<ChatMessagePayload>.Fail("Caption is too long."));

        var groupMeta = await _db.ChatGroups.AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new { g.IsDirect })
            .FirstOrDefaultAsync(ct);
        if (groupMeta is null)
            return NotFound(ApiResponse<ChatMessagePayload>.Fail("Group not found."));

        var memberIds = await _db.ChatGroupMembers.AsNoTracking()
            .Where(m => m.GroupId == id)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        List<string> mentionIds = [];
        if (!string.IsNullOrWhiteSpace(mentionedUserIds))
        {
            try
            {
                mentionIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(mentionedUserIds) ?? [];
            }
            catch
            {
                return BadRequest(ApiResponse<ChatMessagePayload>.Fail("Invalid mentionedUserIds."));
            }
        }

        var mentions = groupMeta.IsDirect
            ? []
            : ChatMentionHelper.ParseAndValidate(mentionIds, memberIds);

        var sender = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == uid, ct);
        var message = new ChatMessage
        {
            GroupId = id,
            SenderUserId = uid,
            Body = trimmed,
            MentionedUserIds = mentions,
            ImageUrls = [],
            SentAtUtc = DateTime.UtcNow
        };
        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync(ct);

        foreach (var file in uploads)
        {
            var url = await _storage.SaveChatImageAsync(TenantId, id, message.Id, file, ct);
            message.ImageUrls.Add(url);
        }

        await _db.SaveChangesAsync(ct);

        var dto = ChatMessageMapper.ToPayload(
            message,
            $"{sender.FirstName} {sender.LastName}".Trim(),
            _configuration,
            HttpContext);

        await _hub.Clients.Group(ChatHub.GroupChannel(id)).SendAsync("MessageReceived", dto, ct);
        return ApiResponse<ChatMessagePayload>.Ok(dto);
    }

    public sealed class PostMessageRequest
    {
        public string Body { get; set; } = string.Empty;
        public List<string> MentionedUserIds { get; set; } = [];
    }

    [HttpPost("groups/{id:guid}/read")]
    public async Task<ActionResult<ApiResponse<object>>> MarkRead([FromRoute] Guid id, [FromBody] MarkReadRequest? request, CancellationToken ct)
    {
        var gate = await EnsureChatAccessAsync(ct);
        if (gate is not null) return gate;

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));
        var member = await _db.ChatGroupMembers.FirstOrDefaultAsync(m => m.GroupId == id && m.UserId == userId.Value, ct);
        if (member is null) return NotFound(ApiResponse<object>.Fail("Group not found."));

        member.LastReadAtUtc = request?.ReadAtUtc ?? DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    [HttpGet("tenant-users")]
    public async Task<ActionResult<ApiResponse<List<ChatGroupMemberDto>>>> ListTenantUsersForPicker(CancellationToken ct)
    {
        var gate = await EnsureChatAccessAsync(ct);
        if (gate is not null) return gate;

        if (!IsTenantAdmin())
            return Forbid();

        var users = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == TenantId && u.IsActive && !u.IsDeleted)
            .OrderBy(u => u.FirstName)
            .Select(u => new ChatGroupMemberDto
            {
                UserId = u.Id.ToString("D"),
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                Designation = u.Designation,
                JoinedAt = u.CreatedAt
            })
            .ToListAsync(ct);

        return ApiResponse<List<ChatGroupMemberDto>>.Ok(users);
    }

    private async Task<ActionResult?> EnsureChatAccessAsync(CancellationToken ct)
    {
        if (!HasTenantId)
            return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));

        var tenant = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == TenantId)
            .Select(t => t.EnabledModules)
            .FirstOrDefaultAsync(ct) ?? [];

        if (!tenant.Contains(AppModuleKey.TeamChat))
            return StatusCode(403, ApiResponse<object>.Fail("Team chat is not enabled for this agency."));

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized."));

        var userModules = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId.Value)
            .Select(u => u.AllowedModules)
            .FirstOrDefaultAsync(ct) ?? [];

        if (userModules.Count > 0 && !userModules.Contains(AppModuleKey.TeamChat))
            return StatusCode(403, ApiResponse<object>.Fail("You do not have access to team chat."));

        return null;
    }

    private async Task<List<Guid>> ResolveMemberUserIdsAsync(IEnumerable<string> ids, CancellationToken ct)
    {
        var parsed = ids
            .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .Distinct()
            .ToList();

        if (parsed.Count == 0) return [];

        return await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == TenantId && u.IsActive && !u.IsDeleted && parsed.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(ct);
    }

    private async Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        await _db.ChatGroupMembers.AsNoTracking()
            .AnyAsync(m => m.GroupId == groupId && m.UserId == userId && m.Group.TenantId == TenantId, ct);

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private bool IsTenantAdmin()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return string.Equals(role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class GroupListRow
    {
        public Guid Id { get; init; }
        public bool IsDirect { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public DateTime CreatedAt { get; init; }
        public int MemberCount { get; init; }
        public DateTime? MyLastRead { get; init; }
        public Guid? OtherUserId { get; init; }
        public string? OtherFirstName { get; init; }
        public string? OtherLastName { get; init; }
        public LastMessageRow? LastMsg { get; init; }
    }

    private sealed class LastMessageRow
    {
        public Guid Id { get; init; }
        public Guid GroupId { get; init; }
        public Guid SenderUserId { get; init; }
        public string Body { get; init; } = string.Empty;
        public DateTime SentAtUtc { get; init; }
        public string SenderFirst { get; init; } = string.Empty;
        public string SenderLast { get; init; } = string.Empty;
        public List<Guid> MentionedUserIds { get; init; } = [];
        public List<string> ImageUrls { get; init; } = [];
    }

    private async Task<ChatGroupDto> MapGroupDtoAsync(GroupListRow g, Guid uid, CancellationToken ct)
    {
        var unread = 0;
        if (g.LastMsg is not null)
        {
            var since = g.MyLastRead ?? DateTime.MinValue;
            unread = await _db.ChatMessages.AsNoTracking()
                .CountAsync(m => m.GroupId == g.Id && !m.IsDeleted && m.SentAtUtc > since && m.SenderUserId != uid, ct);
        }

        ChatMessagePayload? last = null;
        if (g.LastMsg is not null)
        {
            last = ChatMessageMapper.ToPayload(
                new ChatMessage
                {
                    Id = g.LastMsg.Id,
                    GroupId = g.LastMsg.GroupId,
                    SenderUserId = g.LastMsg.SenderUserId,
                    Body = g.LastMsg.Body,
                    SentAtUtc = g.LastMsg.SentAtUtc,
                    MentionedUserIds = g.LastMsg.MentionedUserIds,
                    ImageUrls = g.LastMsg.ImageUrls
                },
                $"{g.LastMsg.SenderFirst} {g.LastMsg.SenderLast}".Trim(),
                _configuration,
                HttpContext);
        }

        var displayName = g.IsDirect && !string.IsNullOrWhiteSpace(g.OtherFirstName)
            ? $"{g.OtherFirstName} {g.OtherLastName}".Trim()
            : g.Name;

        return new ChatGroupDto
        {
            Id = g.Id.ToString("D"),
            Name = displayName,
            Description = g.IsDirect ? null : g.Description,
            IsDirect = g.IsDirect,
            OtherUserId = g.OtherUserId?.ToString("D"),
            MemberCount = g.MemberCount,
            UnreadCount = unread,
            LastMessage = last,
            CreatedAt = g.CreatedAt
        };
    }
}
