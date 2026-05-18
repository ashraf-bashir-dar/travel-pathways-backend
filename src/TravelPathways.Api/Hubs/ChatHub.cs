using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;

namespace TravelPathways.Api.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public ChatHub(AppDbContext db, TenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue && _tenant.TenantId.HasValue)
        {
            var groupIds = await _db.ChatGroupMembers.AsNoTracking()
                .Where(m => m.UserId == userId.Value && m.Group.TenantId == _tenant.TenantId.Value)
                .Select(m => m.GroupId)
                .ToListAsync(Context.ConnectionAborted);

            foreach (var gid in groupIds)
                await Groups.AddToGroupAsync(Context.ConnectionId, GroupChannel(gid));
        }

        await base.OnConnectedAsync();
    }

    /// <summary>Join SignalR group when user opens a chat thread.</summary>
    public async Task JoinGroup(string groupId)
    {
        if (!Guid.TryParse(groupId, out var gid))
            throw new HubException("Invalid group id.");

        await EnsureMemberAsync(gid, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupChannel(gid));
    }

    public async Task SendMessage(string groupId, string body, string[]? mentionedUserIds = null)
    {
        if (!Guid.TryParse(groupId, out var gid))
            throw new HubException("Invalid group id.");

        var trimmed = (body ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new HubException("Message cannot be empty.");
        if (trimmed.Length > 4000)
            throw new HubException("Message is too long (max 4000 characters).");

        var userId = GetUserId() ?? throw new HubException("Unauthorized.");
        await EnsureTeamChatModuleAsync(Context.ConnectionAborted);
        await EnsureMemberAsync(gid, Context.ConnectionAborted);
        await EnsureUserModuleAccessAsync(userId, Context.ConnectionAborted);

        var group = await _db.ChatGroups.AsNoTracking()
            .Where(g => g.Id == gid)
            .Select(g => new { g.IsDirect })
            .FirstOrDefaultAsync(Context.ConnectionAborted)
            ?? throw new HubException("Group not found.");

        var memberIds = await _db.ChatGroupMembers.AsNoTracking()
            .Where(m => m.GroupId == gid)
            .Select(m => m.UserId)
            .ToListAsync(Context.ConnectionAborted);

        var mentions = group.IsDirect
            ? []
            : ChatMentionHelper.ParseAndValidate(mentionedUserIds, memberIds);

        var message = new ChatMessage
        {
            GroupId = gid,
            SenderUserId = userId,
            Body = trimmed,
            MentionedUserIds = mentions,
            SentAtUtc = DateTime.UtcNow
        };

        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync(Context.ConnectionAborted);

        var sender = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.FirstName, u.LastName })
            .FirstAsync(Context.ConnectionAborted);

        var dto = new ChatMessagePayload
        {
            Id = message.Id.ToString("D"),
            GroupId = gid.ToString("D"),
            SenderUserId = userId.ToString("D"),
            SenderName = $"{sender.FirstName} {sender.LastName}".Trim(),
            Body = message.Body,
            SentAtUtc = message.SentAtUtc,
            MentionedUserIds = ChatMentionHelper.ToPayloadIds(mentions),
            ImageUrls = []
        };

        await Clients.Group(GroupChannel(gid)).SendAsync("MessageReceived", dto, Context.ConnectionAborted);
    }

    private async Task EnsureTeamChatModuleAsync(CancellationToken ct)
    {
        if (!_tenant.TenantId.HasValue)
            throw new HubException("Tenant context is required.");

        var enabled = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == _tenant.TenantId.Value)
            .Select(t => t.EnabledModules)
            .FirstOrDefaultAsync(ct) ?? [];

        if (!enabled.Contains(AppModuleKey.TeamChat))
            throw new HubException("Team chat is not enabled for this agency.");
    }

    private async Task EnsureMemberAsync(Guid groupId, CancellationToken ct)
    {
        var userId = GetUserId() ?? throw new HubException("Unauthorized.");
        if (!_tenant.TenantId.HasValue)
            throw new HubException("Tenant context is required.");

        var isMember = await _db.ChatGroupMembers.AsNoTracking()
            .AnyAsync(m => m.GroupId == groupId && m.UserId == userId && m.Group.TenantId == _tenant.TenantId.Value, ct);

        if (!isMember)
            throw new HubException("You are not a member of this group.");
    }

    private async Task EnsureUserModuleAccessAsync(Guid userId, CancellationToken ct)
    {
        var modules = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.AllowedModules)
            .FirstOrDefaultAsync(ct) ?? [];

        if (modules.Count > 0 && !modules.Contains(AppModuleKey.TeamChat))
            throw new HubException("You do not have access to team chat.");
    }

    private Guid? GetUserId()
    {
        var claim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    public static string GroupChannel(Guid groupId) => $"chat-{groupId:D}";
}

public sealed class ChatMessagePayload
{
    public required string Id { get; init; }
    public required string GroupId { get; init; }
    public required string SenderUserId { get; init; }
    public required string SenderName { get; init; }
    public required string Body { get; init; }
    public required DateTime SentAtUtc { get; init; }
    public List<string> MentionedUserIds { get; init; } = [];
    public List<string> ImageUrls { get; init; } = [];
}
