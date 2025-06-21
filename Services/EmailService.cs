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
        private readonly ILogger<EmailService> _logger;

        public EmailService(ILogger<EmailService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> SendVerificationEmailAsync(string email, string code)
        {
            try
            {
                var username = "fit.api@mail.ru";
                var password = "1epwTm8IkJRjmDtQBBDB"; // App Password

                _logger.LogInformation($"📧 Attempting to send email from {username} to {email}");
                _logger.LogInformation($"🔐 Using password: {password[..4]}****");

                var client = new SmtpClient("smtp.mail.ru", 587)
                {
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Credentials = new NetworkCredential(username, password),
                    Timeout = 20000
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(username, "Fitness Tracker"),
                    Subject = "🏃‍♂️ Verification Code - Fitness Tracker",
                    Body = $@"
                        Welcome to Fitness Tracker!
                        
                        Your verification code is: {code}
                        
                        This code expires in 5 minutes.
                        
                        If you didn't request this code, please ignore this email.
                        
                        Best regards,
                        Fitness Tracker Team
                    ",
                    IsBodyHtml = false
                };

                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
                client.Dispose();

                _logger.LogInformation($"✅ Email sent successfully to {email}!");
                Console.WriteLine($"✅ Email sent to {email} with code {code}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ SMTP Error: {ex.Message}");

                // Fallback - выводим код для тестирования
                Console.WriteLine("==================================================");
                Console.WriteLine($"📧 EMAIL TO: {email}");
                Console.WriteLine($"🔐 VERIFICATION CODE: {code}");
                Console.WriteLine($"⏰ Valid for 5 minutes");
                Console.WriteLine($"❌ Email failed: {ex.Message}");
                Console.WriteLine($"✅ Use this code in /api/auth/confirm-email");
                Console.WriteLine("==================================================");

                return true; // Возвращаем success для продолжения работы
            }
        }
    }
}
