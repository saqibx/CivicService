namespace CivicService.Services;

public interface IEmailService
{
    Task SendStatusUpdateEmailAsync(string toEmail, string userName, string requestId, string category, string oldStatus, string newStatus);
}
