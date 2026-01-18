namespace CivicService.Services;

public interface ICaptchaService
{
    Task<bool> VerifyAsync(string token, string expectedAction);
    bool IsConfigured { get; }
}
