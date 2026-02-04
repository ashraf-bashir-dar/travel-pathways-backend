namespace TravelPathways.Api.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "TravelPathways";
    public string Audience { get; set; } = "TravelPathways";
    public string SigningKey { get; set; } = "CHANGE_ME_TO_A_LONG_RANDOM_SECRET";
    public int ExpiresMinutes { get; set; } = 720;
}

