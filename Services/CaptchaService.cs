using System.Text.Json;
using System.Text.Json.Serialization;

namespace CivicService.Services;

public class CaptchaService : ICaptchaService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<CaptchaService> _logger;
    private readonly string? _secretKey;

    public CaptchaService(HttpClient httpClient, IConfiguration config, ILogger<CaptchaService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _secretKey = _config["ReCaptcha:SecretKey"];
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_secretKey);

    public async Task<bool> VerifyAsync(string token, string expectedAction)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("reCAPTCHA not configured. Skipping verification.");
            return true; // Allow if not configured (development)
        }

        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Empty CAPTCHA token received");
            return false;
        }

        try
        {
            var response = await _httpClient.PostAsync(
                "https://www.google.com/recaptcha/api/siteverify",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["secret"] = _secretKey!,
                    ["response"] = token
                }));

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ReCaptchaResponse>(json);

            if (result == null)
            {
                _logger.LogError("Failed to parse reCAPTCHA response");
                return false;
            }

            if (!result.Success)
            {
                _logger.LogWarning("reCAPTCHA verification failed. Errors: {Errors}",
                    string.Join(", ", result.ErrorCodes ?? []));
                return false;
            }

            // For reCAPTCHA v3, check the score (0.0 - 1.0, higher is more likely human)
            var minScore = _config.GetValue<double>("ReCaptcha:MinScore", 0.5);
            if (result.Score < minScore)
            {
                _logger.LogWarning("reCAPTCHA score too low: {Score} (min: {MinScore})",
                    result.Score, minScore);
                return false;
            }

            // Verify the action matches what we expect
            if (!string.IsNullOrEmpty(expectedAction) && result.Action != expectedAction)
            {
                _logger.LogWarning("reCAPTCHA action mismatch. Expected: {Expected}, Got: {Actual}",
                    expectedAction, result.Action);
                return false;
            }

            _logger.LogInformation("reCAPTCHA verified successfully. Score: {Score}, Action: {Action}",
                result.Score, result.Action);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying reCAPTCHA");
            return false;
        }
    }

    private class ReCaptchaResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("challenge_ts")]
        public DateTime ChallengeTs { get; set; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; set; }

        [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}
