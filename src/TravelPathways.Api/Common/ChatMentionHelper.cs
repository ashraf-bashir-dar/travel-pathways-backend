namespace TravelPathways.Api.Common;

public static class ChatMentionHelper
{
    public static List<Guid> ParseAndValidate(IEnumerable<string>? requestedIds, IReadOnlyCollection<Guid> memberUserIds)
    {
        if (requestedIds is null) return [];
        var members = memberUserIds.ToHashSet();
        var result = new List<Guid>();
        foreach (var raw in requestedIds)
        {
            if (!Guid.TryParse(raw, out var id) || !members.Contains(id)) continue;
            if (result.Contains(id)) continue;
            result.Add(id);
        }
        return result;
    }

    public static List<string> ToPayloadIds(IReadOnlyList<Guid> ids) =>
        ids.Select(i => i.ToString("D")).ToList();
}
