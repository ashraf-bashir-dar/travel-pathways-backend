namespace TravelPathways.Api.Services;

public interface IEmailService
{
    /// <summary>
    /// Sends a welcome email to a new travel agent user with their login email and temporary password.
    /// </summary>
    Task<bool> SendWelcomeEmailAsync(string toEmail, string firstName, string tenantName, string loginEmail, string temporaryPassword, CancellationToken ct = default);
}
