using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace CivicService.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendStatusUpdateEmailAsync(string toEmail, string userName, string requestId, string category, string oldStatus, string newStatus)
    {
        var smtpSettings = _config.GetSection("Smtp");

        if (!smtpSettings.Exists() || string.IsNullOrEmpty(smtpSettings["Host"]))
        {
            _logger.LogWarning("SMTP not configured. Skipping email notification.");
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                smtpSettings["FromName"] ?? "Civic Service Portal",
                smtpSettings["FromEmail"] ?? "noreply@civicservice.local"
            ));
            message.To.Add(new MailboxAddress(userName, toEmail));
            message.Subject = $"Service Request Update - {FormatCategory(category)}";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                        <div style='background-color: #550C18; color: white; padding: 20px; text-align: center;'>
                            <h1 style='margin: 0;'>Civic Service Portal</h1>
                        </div>
                        <div style='padding: 30px; background-color: #f9f9f9;'>
                            <h2 style='color: #550C18;'>Hello {userName},</h2>
                            <p>Your service request has been updated.</p>
                            <div style='background-color: white; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                                <p><strong>Request ID:</strong> {requestId[..8]}...</p>
                                <p><strong>Category:</strong> {FormatCategory(category)}</p>
                                <p><strong>Previous Status:</strong> <span style='color: #666;'>{FormatStatus(oldStatus)}</span></p>
                                <p><strong>New Status:</strong> <span style='color: #E63946; font-weight: bold;'>{FormatStatus(newStatus)}</span></p>
                            </div>
                            <p>You can view your request details by logging into the <a href='https://civicservice.local' style='color: #A37C40;'>Civic Service Portal</a>.</p>
                            <p style='color: #666; font-size: 14px; margin-top: 30px;'>
                                Thank you for helping improve our community.
                            </p>
                        </div>
                        <div style='background-color: #EDF0DA; padding: 15px; text-align: center; color: #666; font-size: 12px;'>
                            <p>&copy; 2025 Civic Service Portal. All rights reserved.</p>
                        </div>
                    </div>
                ",
                TextBody = $@"
Hello {userName},

Your service request has been updated.

Request ID: {requestId[..8]}...
Category: {FormatCategory(category)}
Previous Status: {FormatStatus(oldStatus)}
New Status: {FormatStatus(newStatus)}

You can view your request details by logging into the Civic Service Portal.

Thank you for helping improve our community.

--
Civic Service Portal
"
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();

            var port = int.Parse(smtpSettings["Port"] ?? "587");
            var useSsl = bool.Parse(smtpSettings["UseSsl"] ?? "false");

            await client.ConnectAsync(
                smtpSettings["Host"],
                port,
                useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls
            );

            var username = smtpSettings["Username"];
            var password = smtpSettings["Password"];

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                await client.AuthenticateAsync(username, password);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Status update email sent to {Email} for request {RequestId}", toEmail, requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send status update email to {Email}", toEmail);
        }
    }

    private static string FormatCategory(string category) => category switch
    {
        "StreetLight" => "Street Light",
        "IllegalDumping" => "Illegal Dumping",
        "SidewalkRepair" => "Sidewalk Repair",
        "TreeMaintenance" => "Tree Maintenance",
        "WaterLeak" => "Water Leak",
        _ => category
    };

    private static string FormatStatus(string status) => status switch
    {
        "InProgress" => "In Progress",
        _ => status
    };
}
