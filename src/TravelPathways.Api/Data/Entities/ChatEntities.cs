namespace TravelPathways.Api.Data.Entities;

/// <summary>Tenant-scoped chat group (team channel or 1:1 direct message).</summary>
public sealed class ChatGroup : TenantEntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>When true, exactly two members; private 1:1 conversation.</summary>
    public bool IsDirect { get; set; }
    /// <summary>Normalized pair of user ids (smaller_guid_larger_guid) for direct chat lookup.</summary>
    public string? DirectPairKey { get; set; }
    public Guid CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }

    public ICollection<ChatGroupMember> Members { get; set; } = [];
    public ICollection<ChatMessage> Messages { get; set; } = [];
}

public sealed class ChatGroupMember
{
    public Guid GroupId { get; set; }
    public ChatGroup? Group { get; set; }

    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public Guid? AddedByUserId { get; set; }
    public DateTime? LastReadAtUtc { get; set; }
}

public sealed class ChatMessage : EntityBase
{
    public Guid GroupId { get; set; }
    public ChatGroup? Group { get; set; }

    public Guid SenderUserId { get; set; }
    public AppUser? SenderUser { get; set; }

    public string Body { get; set; } = string.Empty;
    /// <summary>Relative paths under /uploads/... for images attached to this message.</summary>
    public List<string> ImageUrls { get; set; } = [];
    /// <summary>Users @mentioned in this message (group chats only).</summary>
    public List<Guid> MentionedUserIds { get; set; } = [];
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}
