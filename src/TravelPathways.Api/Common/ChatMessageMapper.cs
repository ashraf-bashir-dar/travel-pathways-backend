using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.Hubs;

namespace TravelPathways.Api.Common;

public static class ChatMessageMapper
{
    public const int MaxImagesPerMessage = 5;
    public const long MaxImageBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp"
    };

    public static bool IsAllowedChatImage(IFormFile file)
    {
        if (file.Length <= 0 || file.Length > MaxImageBytes) return false;
        if (!string.IsNullOrEmpty(file.ContentType) && AllowedContentTypes.Contains(file.ContentType))
            return true;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp";
    }

    public static List<string> NormalizeImageUrlsForClient(IConfiguration configuration, HttpContext? httpContext, IReadOnlyList<string>? urls)
    {
        if (urls is null || urls.Count == 0) return [];

        var baseUri = PublicApiBaseResolver.Resolve(configuration, httpContext);
        if (string.IsNullOrEmpty(baseUri))
        {
            return urls
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(static u => u.Trim())
                .ToList();
        }

        List<string> result = [];
        foreach (var u in urls)
        {
            if (string.IsNullOrWhiteSpace(u)) continue;
            var s = u.Trim();
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(s);
                continue;
            }

            var path = s.StartsWith('/') ? s : "/" + s;
            result.Add($"{baseUri}{path}");
        }

        return result;
    }

    public static ChatMessagePayload ToPayload(
        ChatMessage message,
        string senderName,
        IConfiguration configuration,
        HttpContext? httpContext) =>
        new()
        {
            Id = message.Id.ToString("D"),
            GroupId = message.GroupId.ToString("D"),
            SenderUserId = message.SenderUserId.ToString("D"),
            SenderName = senderName,
            Body = message.Body,
            SentAtUtc = message.SentAtUtc,
            MentionedUserIds = ChatMentionHelper.ToPayloadIds(message.MentionedUserIds),
            ImageUrls = NormalizeImageUrlsForClient(configuration, httpContext, message.ImageUrls)
        };
}
