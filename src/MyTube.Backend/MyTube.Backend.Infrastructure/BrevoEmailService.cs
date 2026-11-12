using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application;
using MyTube.Backend.Application.Security;

namespace MyTube.Backend.Infrastructure;

public class BrevoEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ISecretManagementService _secretService;
    private readonly ILogger<BrevoEmailService> _logger;

    public BrevoEmailService(
        HttpClient httpClient, 
        IConfiguration configuration, 
        ISecretManagementService secretService,
        ILogger<BrevoEmailService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _secretService = secretService;
        _logger = logger;
    }

    public async Task<(bool Success, string ErrorMessage)> SendOtpEmailAsync(string toEmail, string recipientName, string otpCode)
    {
        Console.WriteLine($"\n=======================================================");
        Console.WriteLine($"[BREVO OTP CODE] Email: {toEmail} | Code: {otpCode}");
        Console.WriteLine($"=======================================================\n");

        var apiKey = await _secretService.GetSecretAsync("Brevo:ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            const string errorMsg = "Brevo API key is not configured.";
            _logger.LogError(errorMsg);
            return (false, errorMsg);
        }

        var senderEmail = _configuration["Brevo:SenderEmail"] ?? "noreply@smartbox.linkpc.net";
        var senderName = _configuration["Brevo:SenderName"] ?? "MyTube";

        var requestUri = "https://api.brevo.com/v3/smtp/email";

        var payload = new
        {
            sender = new
            {
                name = senderName,
                email = senderEmail
            },
            replyTo = new
            {
                name = senderName,
                email = senderEmail
            },
            to = new[]
            {
                new
                {
                    email = toEmail,
                    name = string.IsNullOrWhiteSpace(recipientName) ? toEmail : recipientName
                }
            },
            subject = $"{otpCode} is your MyTube verification code",
            htmlContent = $@"
            <!DOCTYPE html>
            <html>
            <head>
              <style>
                body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #0c0d10; color: #f1f1f1; margin: 0; padding: 20px; }}
                .card {{ max-width: 480px; margin: 0 auto; background: #16181d; border: 1px solid #262932; border-radius: 16px; padding: 32px; box-shadow: 0 20px 40px rgba(0,0,0,0.5); }}
                .brand {{ font-size: 24px; font-weight: bold; color: #3cd69b; text-align: center; margin-bottom: 8px; }}
                .subtitle {{ text-align: center; color: #9e9e9e; font-size: 14px; margin-bottom: 24px; }}
                .otp-box {{ background: #0c0d10; border: 1px solid #3cd69b; border-radius: 12px; padding: 16px; text-align: center; font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #3cd69b; margin: 24px 0; }}
                .footer {{ text-align: center; color: #666; font-size: 12px; margin-top: 24px; }}
              </style>
            </head>
            <body>
              <div class='card'>
                <div class='brand'>MyTube</div>
                <div class='subtitle'>Email Verification Code</div>
                <p>Hello {recipientName},</p>
                <p>Use the following 6-digit code to complete your registration on MyTube:</p>
                <div class='otp-box'>{otpCode}</div>
                <p>This code will expire in <strong>10 minutes</strong>. If you did not request this email, please ignore it.</p>
                <div class='footer'>&copy; {DateTime.UtcNow.Year} MyTube Personal HomeLab</div>
              </div>
            </body>
            </html>"
        };

        var json = JsonSerializer.Serialize(payload);
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("api-key", apiKey);
        request.Headers.Add("accept", "application/json");

        try
        {
            var response = await _httpClient.SendAsync(request);
            var resBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully sent OTP email via Brevo to {Email}", toEmail);
                return (true, string.Empty);
            }

            _logger.LogError("Failed to send OTP via Brevo to {Email}. Status: {Status}, Body: {Body}", toEmail, response.StatusCode, resBody);
            
            string friendlyMsg = resBody;
            try
            {
                using var doc = JsonDocument.Parse(resBody);
                if (doc.RootElement.TryGetProperty("message", out var msgProp))
                {
                    friendlyMsg = msgProp.GetString() ?? friendlyMsg;
                }
            }
            catch { }

            if (resBody.Contains("unrecognised IP address") || resBody.Contains("authorised_ips"))
            {
                friendlyMsg = "Brevo IP Security Restriction: Please authorize your IP in Brevo at https://app.brevo.com/security/authorised_ips";
            }

            return (false, friendlyMsg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while sending OTP email via Brevo to {Email}", toEmail);
            return (false, ex.Message);
        }
    }
}
