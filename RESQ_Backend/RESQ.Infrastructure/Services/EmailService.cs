using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RESQ.Application.Services;
using System.Globalization;
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
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:8080";
            var verificationUrl = $"{baseUrl}/identity/auth/verify-email?token={verificationToken}";
            
            var subject = "Xác Minh Tài Khoản RESQ Của Bạn";
            var body = GetDefaultTemplate(
                "Xác Minh Email", 
                "Chào mừng đến với RESQ!", 
                "Cảm ơn bạn đã đăng ký. Vui lòng xác minh địa chỉ email của bạn bằng cách nhấp vào nút bên dưới:", 
                "Xác Minh Email", 
                verificationUrl
            );

            await SendEmailAsync(email, subject, body, cancellationToken);
        }

        public async Task SendPasswordResetEmailAsync(string email, string resetToken, CancellationToken cancellationToken = default)
        {
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:5000";
            var resetUrl = $"{baseUrl}/api/auth/reset-password?token={resetToken}";

            var subject = "Đặt Lại Mật Khẩu RESQ Của Bạn";
            var body = GetDefaultTemplate(
                "Đặt Lại Mật Khẩu", 
                "Yêu Cầu Đặt Lại Mật Khẩu", 
                "Chúng tôi đã nhận được yêu cầu đặt lại mật khẩu của bạn. Nhấp vào nút bên dưới để đặt mật khẩu mới:", 
                "Đặt Lại Mật Khẩu", 
                resetUrl, 
                isAlert: true
            );

            await SendEmailAsync(email, subject, body, cancellationToken);
        }

        public async Task SendDonationSuccessEmailAsync(
            string donorEmail,
            string donorName,
            decimal amount,
            string campaignName,
            string campaignCode,
            int donationId,
            CancellationToken cancellationToken = default)
        {
            var subject = $"Xác nhận ủng hộ #{donationId} - {campaignCode}";
            
            var cultureInfo = new CultureInfo("vi-VN");
            var formattedAmount = amount.ToString("N0", cultureInfo) + " VNĐ";
            var date = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            
            // Constructing the Donation Link (adjust base URL as needed)
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:8080";
            var donationUrl = $"{baseUrl}/donations/history/{donationId}";

            // Swiss Editorial Design Template
            var body = $@"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Xác nhận ủng hộ RESQ</title>
</head>
<body style='margin: 0; padding: 0; background-color: #ffffff; font-family: Helvetica, Arial, sans-serif; -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; color: #000000;'>
    
    <!-- Wrapper -->
    <table border='0' cellpadding='0' cellspacing='0' width='100%' style='border-collapse: collapse; background-color: #ffffff;'>
        <tr>
            <td align='center' style='padding: 20px 10px;'>
                
                <!-- Main Container (600px max) -->
                <table border='0' cellpadding='0' cellspacing='0' width='100%' style='max-width: 600px; border-collapse: collapse; text-align: left;'>
                    
                    <!-- A. HEADER -->
                    <tr>
                        <td style='padding-bottom: 20px; border-bottom: 1px solid #000000;'>
                            <table border='0' cellpadding='0' cellspacing='0' width='100%'>
                                <tr>
                                    <td align='left' style='vertical-align: middle;'>
                                        <img src='https://idzcpzdafiviohamtluh.supabase.co/storage/v1/object/public/RESQ/RESQ.jpg' alt='RESQ' width='100' style='display: block; border: 0; width: 100px; height: auto;' />
                                    </td>
                                    <td align='right' style='font-size: 12px; font-weight: bold; color: #000000; text-transform: uppercase; vertical-align: middle; letter-spacing: 1px;'>
                                        Xác nhận giao dịch
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- B. HEADLINE -->
                    <tr>
                        <td style='padding: 60px 0 40px 0; color: #000000;'>
                            <h1 style='margin: 0; font-size: 36px; line-height: 1.1; font-weight: 700; letter-spacing: -1px; text-transform: none;'>
                                Sự Ủng Hộ Của Bạn<br>Tạo Nên Thay Đổi.
                            </h1>
                            <p style='margin: 24px 0 0 0; font-size: 16px; line-height: 1.6; color: #333333; max-width: 480px;'>
                                Chào {donorName},<br><br>
                                Cảm ơn bạn đã chung tay cùng RESQ. Đóng góp của bạn đã được tiếp nhận an toàn và sẽ được chuyển trực tiếp đến các hoạt động cứu trợ khẩn cấp.
                            </p>
                        </td>
                    </tr>

                    <!-- C. DONATION CARD -->
                    <tr>
                        <td style='padding-bottom: 40px;'>
                            <!-- Receipt Box: Thin border, Left Accent Line -->
                            <table border='0' cellpadding='0' cellspacing='0' width='100%' style='border: 1px solid #000000; border-left: 4px solid #000000;'>
                                <tr>
                                    <td style='padding: 30px;'>
                                        <table border='0' cellpadding='0' cellspacing='0' width='100%'>
                                            <!-- Campaign -->
                                            <tr>
                                                <td style='padding-bottom: 8px; font-size: 11px; text-transform: uppercase; letter-spacing: 1px; color: #666666; font-weight: 600;'>
                                                    Chiến dịch
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style='padding-bottom: 30px; font-size: 20px; font-weight: 700; color: #000000; line-height: 1.3;'>
                                                    {campaignName}
                                                </td>
                                            </tr>
                                            <!-- IDs Grid -->
                                            <tr>
                                                <td>
                                                    <table border='0' cellpadding='0' cellspacing='0' width='100%'>
                                                        <tr>
                                                            <td width='50%' style='padding-bottom: 5px; font-size: 11px; text-transform: uppercase; color: #666666; font-weight: 600;'>
                                                                Mã chiến dịch
                                                            </td>
                                                            <td width='50%' style='padding-bottom: 5px; font-size: 11px; text-transform: uppercase; color: #666666; font-weight: 600;'>
                                                                Mã ủng hộ
                                                            </td>
                                                        </tr>
                                                        <tr>
                                                            <td style='padding-bottom: 30px; font-size: 14px; color: #000000; font-family: monospace;'>
                                                                {campaignCode}
                                                            </td>
                                                            <td style='padding-bottom: 30px; font-size: 14px; color: #000000; font-family: monospace;'>
                                                                #{donationId}
                                                            </td>
                                                        </tr>
                                                    </table>
                                                </td>
                                            </tr>
                                            <!-- Amount -->
                                            <tr>
                                                <td style='border-top: 1px dotted #000000; padding-top: 20px;'>
                                                    <span style='font-size: 11px; text-transform: uppercase; color: #666666; font-weight: 600; display: block; margin-bottom: 8px;'>
                                                        Tổng số tiền quyên góp
                                                    </span>
                                                    <span style='font-size: 42px; font-weight: 700; color: #000000; letter-spacing: -1.5px;'>
                                                        {formattedAmount}
                                                    </span>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- D. DIVIDER -->
                    <tr>
                        <td style='border-top: 1px solid #000000; font-size: 0; line-height: 0;'>&nbsp;</td>
                    </tr>

                    <!-- E. QUOTE SECTION (UPDATED) -->
                    <tr>
                        <td align='center' style='padding: 60px 20px; border-bottom: 1px solid #000000;'>
                            <p style='margin: 0; font-family: ""Times New Roman"", Times, serif; font-size: 28px; line-height: 1.4; font-weight: 400; font-style: italic; color: #000000; letter-spacing: -0.5px;'>
                                “Lòng nhân ái của bạn là nguồn sức mạnh to lớn cho cộng đồng.”
                            </p>
                        </td>
                    </tr>

                    <!-- F. CTA SECTION (UPDATED) -->
                    <tr>
                        <td align='center' style='padding: 60px 0;'>
                            <table border='0' cellspacing='0' cellpadding='0'>
                                <tr>
                                    <td align='center' bgcolor='#FF5722' style='background-color: #FF5722;'>
                                        <!-- Added thin black border (1px solid #000000) as requested -->
                                        <a href='{donationUrl}' target='_blank' style='font-size: 16px; font-family: Helvetica, Arial, sans-serif; font-weight: 700; color: #ffffff; text-decoration: none; padding: 20px 40px; border: 1px solid #000000; display: inline-block; text-transform: uppercase; letter-spacing: 1px;'>
                                            Xem Chi Tiết
                                        </a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- G. FOOTER -->
                    <tr>
                        <td bgcolor='#000000' style='background-color: #000000; padding: 40px; text-align: center;'>
                            <p style='margin: 0 0 10px 0; color: #ffffff; font-size: 12px; font-weight: bold; text-transform: uppercase; letter-spacing: 1px;'>
                                &copy; 2026 RESQ Foundation
                            </p>
                            <p style='margin: 0; color: #999999; font-size: 11px; line-height: 1.5;'>
                                RESQ hoạt động dựa trên nguyên tắc minh bạch.<br>
                                Bạn nhận được email này vì đã thực hiện quyên góp tại hệ thống RESQ.<br>
                                Thời gian giao dịch: {date}
                            </p>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

            await SendEmailAsync(donorEmail, subject, body, cancellationToken);
        }

        private string GetDefaultTemplate(string title, string heading, string message, string buttonText, string url, bool isAlert = false)
        {
            var btnColor = isAlert ? "#dc3545" : "#007bff";
            return $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: {btnColor}; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 30px; background-color: #f9f9f9; }}
                        .button {{ display: inline-block; padding: 12px 30px; background-color: {btnColor}; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
                        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>RESQ</h1>
                        </div>
                        <div class='content'>
                            <h2>{heading}</h2>
                            <p>{message}</p>
                            <p style='text-align: center;'>
                                <a href='{url}' class='button'>{buttonText}</a>
                            </p>
                            <p style='font-size: 12px; color: #999;'>{url}</p>
                        </div>
                        <div class='footer'>
                            <p>&copy; 2026 RESQ.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");
            
            var smtpHost = emailSettings["SmtpHost"] ?? "smtp.gmail.com";
            var smtpPortStr = emailSettings["SmtpPort"] ?? "587";
            var smtpUsername = emailSettings["SmtpUsername"];
            var smtpPassword = emailSettings["SmtpPassword"];
            var fromEmail = emailSettings["FromEmail"] ?? smtpUsername;
            var fromName = emailSettings["FromName"] ?? "RESQ System";

            if (string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
            {
                _logger.LogWarning("Email settings not configured. Skipping email send to {email}", toEmail);
                return;
            }

            if (!int.TryParse(smtpPortStr, out int smtpPort))
            {
                smtpPort = 587;
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
