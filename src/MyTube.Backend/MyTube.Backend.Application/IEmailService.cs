using System.Threading.Tasks;

namespace MyTube.Backend.Application;

public interface IEmailService
{
    Task<(bool Success, string ErrorMessage)> SendOtpEmailAsync(string toEmail, string recipientName, string otpCode);
}
