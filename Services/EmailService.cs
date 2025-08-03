using System.Net;
using System.Net.Mail;

namespace FitnessTracker.API.Services
{
    public interface IEmailService
    {
        Task<bool> SendVerificationEmailAsync(string email, string code);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendVerificationEmailAsync(string email, string code)
        {
            try
            {
                var smtpHost = _configuration["Email:Host"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["Email:Port"] ?? "587");
                var username = _configuration["Email:Username"] ?? "noreply@lightweightfit.com";
                var password = _configuration["Email:Password"] ?? "B46mGUX5hV3u6aQ=";
                var fromName = _configuration["Email:FromName"] ?? "Fitness Tracker";
                var fromEmail = _configuration["Email:FromEmail"] ?? "noreply@lightweightfit.com";

                _logger.LogInformation($"📧 Sending email from {username} to {email}");

                var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Credentials = new NetworkCredential(username, password),
                    Timeout = 30000
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = "🏃‍♂️ Verification Code - Fitness Tracker",
                    Body = $@"
Welcome to Fitness Tracker!

Your verification code is: {code}

This code expires in 5 minutes.

If you didn't request this code, please ignore this email.

Best regards,
Fitness Tracker Team

---
This email was sent from lightweightfit.com
                    ",
                    IsBodyHtml = false
                };

                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
                client.Dispose();

                _logger.LogInformation($"✅ Email sent successfully from {fromEmail} to {email}!");
                Console.WriteLine($"✅ Professional email sent to {email} with code {code}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ SMTP Error: {ex.Message}");

                Console.WriteLine("==================================================");
                Console.WriteLine($"📧 EMAIL TO: {email}");
                Console.WriteLine($"🔐 VERIFICATION CODE: {code}");
                Console.WriteLine($"⏰ Valid for 5 minutes");
                Console.WriteLine($"❌ Email failed: {ex.Message}");
                Console.WriteLine($"✅ Use this code in /api/auth/confirm-email");
                Console.WriteLine("==================================================");

                return true; 
            }
        }
    }
}