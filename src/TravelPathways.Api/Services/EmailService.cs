using System.Net;
using System.Net.Mail;

namespace TravelPathways.Api.Services;

public sealed class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<bool> SendWelcomeEmailAsync(string toEmail, string firstName, string tenantName, string loginEmail, string temporaryPassword, CancellationToken ct = default)
    {
        var host = _config["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogInformation("Email not configured (Email:SmtpHost). Welcome email skipped for {Email}. Login: {Login}, Password: [sent to user]", toEmail, loginEmail);
            return true; // so user creation still succeeds
        }

        var from = _config["Email:From"] ?? "noreply@travelpathways.local";
        var port = _config.GetValue<int>("Email:SmtpPort", 587);
        var user = _config["Email:UserName"];
        var password = _config["Email:Password"];
        var enableSsl = _config.GetValue<bool>("Email:EnableSsl", true);

        var subject = "Your Travel Pathways account";
        var body = $@"
Hello {firstName},

Your account has been created for {tenantName} on Travel Pathways.

Login details:
- Email (username): {loginEmail}
- Temporary password: {temporaryPassword}

Please sign in and change your password after your first login.

Best regards,
Travel Pathways Team
".Trim();

        try
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                Credentials = string.IsNullOrWhiteSpace(user) ? null : new NetworkCredential(user, password)
            };
            using var message = new MailMessage(from, toEmail, subject, body);
            await client.SendMailAsync(message, ct);
            _logger.LogInformation("Welcome email sent to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to {Email}", toEmail);
            return false;
        }
    }
}
