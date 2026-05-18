namespace TravelPathways.Api.Services;

public interface IEmailService
{
    bool IsConfigured { get; }

    /// <summary>
    /// Sends a welcome email to a new travel agent user with their login email and temporary password.
    /// </summary>
    Task<bool> SendWelcomeEmailAsync(string toEmail, string firstName, string tenantName, string loginEmail, string temporaryPassword, CancellationToken ct = default);

    /// <summary>Sends a plain-text email to one recipient.</summary>
    Task<bool> SendEmailAsync(string toEmail, string subject, string body, CancellationToken ct = default);
}
