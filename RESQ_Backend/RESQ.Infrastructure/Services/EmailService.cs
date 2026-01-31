using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Services;
using System.Net;
using System.Net.Mail;

namespace RESQ.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendVerificationEmailAsync(string email, string verificationToken, CancellationToken cancellationToken = default)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:5000";
            var verificationUrl = $"{baseUrl}/api/auth/verify-email?token={verificationToken}";

            var subject = "Xác Minh Tài Khoản RESQ Của Bạn";
            var body = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 30px; background-color: #f9f9f9; }}
                        .button {{ display: inline-block; padding: 12px 30px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
                        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>RESQ - Hệ Thống Ứng Phó Khẩn Cấp</h1>
                        </div>
                        <div class='content'>
                            <h2>Chào mừng đến với RESQ!</h2>
                            <p>Cảm ơn bạn đã đăng ký làm nhân viên cứu hộ. Vui lòng xác minh địa chỉ email của bạn bằng cách nhấp vào nút bên dưới:</p>
                            <p style='text-align: center;'>
                                <a href='{verificationUrl}' class='button'>Xác Minh Email</a>
                            </p>
                            <p>Hoặc sao chép và dán liên kết này vào trình duyệt của bạn:</p>
                            <p style='word-break: break-all; color: #007bff;'>{verificationUrl}</p>
                            <p><strong>Liên kết này sẽ hết hạn sau 24 giờ.</strong></p>
                            <p>Nếu bạn không tạo tài khoản, vui lòng bỏ qua email này.</p>
                        </div>
                        <div class='footer'>
                            <p>&copy; 2026 RESQ. Bảo lưu mọi quyền.</p>
                        </div>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(email, subject, body, cancellationToken);
        }

        public async Task SendPasswordResetEmailAsync(string email, string resetToken, CancellationToken cancellationToken = default)
        {
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:5000";
            var resetUrl = $"{baseUrl}/api/auth/reset-password?token={resetToken}";

            var subject = "Đặt Lại Mật Khẩu RESQ Của Bạn";
            var body = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 30px; background-color: #f9f9f9; }}
                        .button {{ display: inline-block; padding: 12px 30px; background-color: #dc3545; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
                        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>RESQ - Hệ Thống Ứng Phó Khẩn Cấp</h1>
                        </div>
                        <div class='content'>
                            <h2>Yêu Cầu Đặt Lại Mật Khẩu</h2>
                            <p>Chúng tôi đã nhận được yêu cầu đặt lại mật khẩu của bạn. Nhấp vào nút bên dưới để đặt mật khẩu mới:</p>
                            <p style='text-align: center;'>
                                <a href='{resetUrl}' class='button'>Đặt Lại Mật Khẩu</a>
                            </p>
                            <p>Hoặc sao chép và dán liên kết này vào trình duyệt của bạn:</p>
                            <p style='word-break: break-all; color: #007bff;'>{resetUrl}</p>
                            <p><strong>Liên kết này sẽ hết hạn sau 1 giờ.</strong></p>
                            <p>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                        </div>
                        <div class='footer'>
                            <p>&copy; 2026 RESQ. Bảo lưu mọi quyền.</p>
                        </div>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(email, subject, body, cancellationToken);
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");
            
            var smtpHost = emailSettings["SmtpHost"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(emailSettings["SmtpPort"] ?? "587");
            var smtpUsername = emailSettings["SmtpUsername"];
            var smtpPassword = emailSettings["SmtpPassword"];
            var fromEmail = emailSettings["FromEmail"] ?? smtpUsername;
            var fromName = emailSettings["FromName"] ?? "RESQ System";

            if (string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
            {
                _logger.LogWarning("Email settings not configured. Skipping email send to {email}", toEmail);
                _logger.LogInformation("Would have sent email to {email} with subject: {subject}", toEmail, subject);
                return;
            }

            try
            {
                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail!, fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage, cancellationToken);
                _logger.LogInformation("Email sent successfully to {email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {email}", toEmail);
                throw;
            }
        }
    }
}
