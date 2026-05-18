namespace TravelPathways.Api.Common;

public static class ChatDirectHelper
{
    public static string BuildPairKey(Guid userIdA, Guid userIdB)
    {
        var (a, b) = userIdA.CompareTo(userIdB) < 0 ? (userIdA, userIdB) : (userIdB, userIdA);
        return $"{a:D}_{b:D}";
    }
}
