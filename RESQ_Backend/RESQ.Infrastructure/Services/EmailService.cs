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

            var subject = "Verify Your RESQ Account";
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
                            <h1>RESQ - Emergency Response System</h1>
                        </div>
                        <div class='content'>
                            <h2>Welcome to RESQ!</h2>
                            <p>Thank you for registering as a rescuer. Please verify your email address by clicking the button below:</p>
                            <p style='text-align: center;'>
                                <a href='{verificationUrl}' class='button'>Verify Email</a>
                            </p>
                            <p>Or copy and paste this link into your browser:</p>
                            <p style='word-break: break-all; color: #007bff;'>{verificationUrl}</p>
                            <p><strong>This link will expire in 24 hours.</strong></p>
                            <p>If you did not create an account, please ignore this email.</p>
                        </div>
                        <div class='footer'>
                            <p>&copy; 2026 RESQ. All rights reserved.</p>
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

            var subject = "Reset Your RESQ Password";
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
                            <h1>RESQ - Emergency Response System</h1>
                        </div>
                        <div class='content'>
                            <h2>Password Reset Request</h2>
                            <p>We received a request to reset your password. Click the button below to set a new password:</p>
                            <p style='text-align: center;'>
                                <a href='{resetUrl}' class='button'>Reset Password</a>
                            </p>
                            <p>Or copy and paste this link into your browser:</p>
                            <p style='word-break: break-all; color: #007bff;'>{resetUrl}</p>
                            <p><strong>This link will expire in 1 hour.</strong></p>
                            <p>If you did not request a password reset, please ignore this email.</p>
                        </div>
                        <div class='footer'>
                            <p>&copy; 2026 RESQ. All rights reserved.</p>
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
