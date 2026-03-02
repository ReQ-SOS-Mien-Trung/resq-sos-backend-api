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
            var body = $@"<!DOCTYPE html>
<html lang='vi'>
<head>
  <meta charset='UTF-8' />
  <meta name='viewport' content='width=device-width, initial-scale=1.0' />
  <title>Xác Minh Email — RESQ</title>
</head>
<body style='margin:0;padding:0;background:#ffffff;font-family:""Helvetica Neue"",Helvetica,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' border='0' style='background:#ffffff;'>
    <tr>
      <td align='center' style='padding:48px 16px;'>
        <table width='600' cellpadding='0' cellspacing='0' border='0' style='max-width:600px;width:100%;border:3px solid #000000;'>

          <!-- HEADER RULE -->
          <tr>
            <td style='background:#000000;padding:0;height:6px;font-size:0;line-height:0;'>&nbsp;</td>
          </tr>

          <!-- MASTHEAD -->
          <tr>
            <td style='padding:32px 40px 24px;border-bottom:3px solid #000000;'>
              <table width='100%' cellpadding='0' cellspacing='0' border='0'>
                <tr>
                  <td>
                    <span style='font-size:11px;font-weight:700;letter-spacing:4px;text-transform:uppercase;color:#000000;'>RESQ SYSTEM</span>
                  </td>
                  <td align='right'>
                    <span style='font-size:11px;letter-spacing:2px;text-transform:uppercase;color:#999999;'>EMAIL VERIFICATION</span>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- HERO BLOCK -->
          <tr>
            <td style='padding:48px 40px 32px;border-bottom:2px solid #000000;'>
              <p style='margin:0 0 8px;font-size:11px;font-weight:700;letter-spacing:4px;text-transform:uppercase;color:#FF5722;'>— Bước 01 / Xác minh</p>
              <h1 style='margin:0 0 24px;font-size:42px;font-weight:900;line-height:1.05;letter-spacing:-1.5px;color:#000000;text-transform:uppercase;'>XÁC MINH<br/>TÀI KHOẢN</h1>
              <p style='margin:0;font-size:15px;line-height:1.7;color:#333333;max-width:420px;'>
                Cảm ơn bạn đã đăng ký trên RESQ. Xác nhận tài khoản của bạn để bắt đầu tham gia hệ thống ứng phó khẩn cấp.
              </p>
            </td>
          </tr>

          <!-- CTA BLOCK -->
          <tr>
            <td style='padding:40px;border-bottom:2px solid #000000;'>
              <table cellpadding='0' cellspacing='0' border='0'>
                <tr>
                  <td style='background:#FF5722;'>
                    <a href='{verificationUrl}'
                       style='display:inline-block;padding:16px 40px;font-size:12px;font-weight:900;letter-spacing:3px;text-transform:uppercase;color:#ffffff;text-decoration:none;'>
                      XÁC MINH EMAIL →
                    </a>
                  </td>
                </tr>
              </table>
              <p style='margin:24px 0 0;font-size:11px;color:#999999;letter-spacing:1px;'>
                HOẶC DÁN ĐƯỜNG DẪN VÀO TRÌNH DUYỆT:
              </p>
              <p style='margin:8px 0 0;font-size:12px;line-height:1.6;word-break:break-all;color:#000000;border-left:3px solid #FF5722;padding-left:12px;'>
                {verificationUrl}
              </p>
            </td>
          </tr>

          <!-- META GRID -->
          <tr>
            <td style='padding:0;'>
              <table width='100%' cellpadding='0' cellspacing='0' border='0'>
                <tr>
                  <td width='50%' style='padding:24px 40px;border-right:2px solid #000000;border-bottom:2px solid #000000;'>
                    <p style='margin:0 0 4px;font-size:10px;font-weight:700;letter-spacing:3px;text-transform:uppercase;color:#999999;'>HIỆU LỰC</p>
                    <p style='margin:0;font-size:14px;font-weight:700;color:#000000;'>24 GIỜ</p>
                  </td>
                  <td width='50%' style='padding:24px 40px;border-bottom:2px solid #000000;'>
                    <p style='margin:0 0 4px;font-size:10px;font-weight:700;letter-spacing:3px;text-transform:uppercase;color:#999999;'>DỊCH VỤ</p>
                    <p style='margin:0;font-size:14px;font-weight:700;color:#000000;'>RESQ EMERGENCY</p>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- DISCLAIMER -->
          <tr>
            <td style='padding:20px 40px;border-bottom:3px solid #000000;background:#f5f5f5;'>
              <p style='margin:0;font-size:11px;line-height:1.6;color:#666666;'>
                Nếu bạn không tạo tài khoản này, vui lòng bỏ qua email này. Liên kết sẽ tự động hết hạn.
              </p>
            </td>
          </tr>

          <!-- FOOTER -->
          <tr>
            <td style='padding:20px 40px;'>
              <table width='100%' cellpadding='0' cellspacing='0' border='0'>
                <tr>
                  <td>
                    <span style='font-size:10px;font-weight:700;letter-spacing:3px;text-transform:uppercase;color:#000000;'>RESQ</span>
                  </td>
                  <td align='right'>
                    <span style='font-size:10px;letter-spacing:1px;color:#999999;'>© 2026</span>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- BOTTOM ACCENT -->
          <tr>
            <td style='background:#FF5722;padding:0;height:4px;font-size:0;line-height:0;'>&nbsp;</td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

            await SendEmailAsync(email, subject, body, cancellationToken);
        }

        public async Task SendPasswordResetEmailAsync(string email, string resetToken, CancellationToken cancellationToken = default)
        {
            var FEbaseUrl = _configuration["AppSettings:FEBaseUrl"] ?? "http://localhost:5173";
            var resetUrl = $"{FEbaseUrl}/auth/reset-pass?token={resetToken}";

            var subject = "Đặt Lại Mật Khẩu RESQ Của Bạn";
            var body = $@"<!DOCTYPE html>
<html lang='vi'>
<head>
  <meta charset='UTF-8' />
  <meta name='viewport' content='width=device-width, initial-scale=1.0' />
  <title>Đặt Lại Mật Khẩu — RESQ</title>
</head>
<body style='margin:0;padding:0;background:#ffffff;font-family:""Be Vietnam Pro"",""Segoe UI"",Roboto,""Noto Sans"",sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' border='0' style='background:#ffffff;'>
    <tr>
      <td align='center' style='padding:48px 16px;'>
        <table width='600' cellpadding='0' cellspacing='0' border='0' style='max-width:600px;width:100%;border:3px solid #000000;'>

          <!-- HEADER RULE -->
          <tr>
            <td style='background:#000000;padding:0;height:6px;font-size:0;line-height:0;'>&nbsp;</td>
          </tr>

          <!-- MASTHEAD -->
          <tr>
            <td style='padding:32px 40px 24px;border-bottom:3px solid #000000;'>
              <table width='100%' cellpadding='0' cellspacing='0' border='0'>
                <tr>
                  <td>
                    <span style='font-size:11px;font-weight:700;letter-spacing:4px;text-transform:uppercase;color:#000000;'>RESQ SYSTEM</span>
                  </td>
                  <td align='right'>
                    <span style='font-size:11px;letter-spacing:2px;text-transform:uppercase;color:#999999;'>PASSWORD RESET</span>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- HERO BLOCK -->
          <tr>
            <td style='padding:48px 40px 32px;border-bottom:2px solid #000000;'>
              <p style='margin:0 0 8px;font-size:11px;font-weight:700;letter-spacing:4px;text-transform:uppercase;color:#FF5722;'>— Yêu cầu bảo mật</p>
              <h1 style='margin:0 0 24px;font-size:42px;font-weight:900;line-height:1.05;letter-spacing:-1.5px;color:#000000;text-transform:uppercase;'>ĐẶT LẠI<br/>MẬT KHẨU</h1>
              <p style='margin:0;font-size:15px;line-height:1.7;color:#333333;max-width:420px;'>
                Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Nhấp vào nút bên dưới để tiến hành.
              </p>
            </td>
          </tr>

          <!-- CTA BLOCK -->
          <tr>
            <td style='padding:40px;border-bottom:2px solid #000000;'>
              <table cellpadding='0' cellspacing='0' border='0'>
                <tr>
                  <td style='background:#FF5722;'>
                    <a href='{resetUrl}'
                       style='display:inline-block;padding:16px 40px;font-size:12px;font-weight:900;letter-spacing:3px;text-transform:uppercase;color:#ffffff;text-decoration:none;'>
                      ĐẶT LẠI MẬT KHẨU →
                    </a>
                  </td>
                </tr>
              </table>
              <p style='margin:24px 0 0;font-size:11px;color:#999999;letter-spacing:1px;'>
                HOẶC DÁN ĐƯỜNG DẪN VÀO TRÌNH DUYỆT:
              </p>
              <p style='margin:8px 0 0;font-size:12px;line-height:1.6;word-break:break-all;color:#000000;border-left:3px solid #FF5722;padding-left:12px;'>
                {resetUrl}
              </p>
            </td>
          </tr>

          <!-- META GRID -->
          <tr>
            <td style='padding:0;'>
              <table width='100%' cellpadding='0' cellspacing='0' border='0'>
                <tr>
                  <td width='50%' style='padding:24px 40px;border-right:2px solid #000000;border-bottom:2px solid #000000;'>
                    <p style='margin:0 0 4px;font-size:10px;font-weight:700;letter-spacing:3px;text-transform:uppercase;color:#999999;'>HIỆU LỰC</p>
                    <p style='margin:0;font-size:14px;font-weight:700;color:#000000;'>01 GIỜ</p>
                  </td>
                  <td width='50%' style='padding:24px 40px;border-bottom:2px solid #000000;'>
                    <p style='margin:0 0 4px;font-size:10px;font-weight:700;letter-spacing:3px;text-transform:uppercase;color:#999999;'>MỨC ĐỘ</p>
                    <p style='margin:0;font-size:14px;font-weight:700;color:#000000;'>BẢO MẬT CAO</p>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- DISCLAIMER -->
          <tr>
            <td style='padding:20px 40px;border-bottom:3px solid #000000;background:#f5f5f5;'>
              <p style='margin:0;font-size:11px;line-height:1.6;color:#666666;'>
                Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này. Tài khoản của bạn vẫn an toàn và không có thay đổi nào được thực hiện.
              </p>
            </td>
          </tr>

          <!-- FOOTER -->
          <tr>
            <td style='padding:20px 40px;'>
              <table width='100%' cellpadding='0' cellspacing='0' border='0'>
                <tr>
                  <td>
                    <span style='font-size:10px;font-weight:700;letter-spacing:3px;text-transform:uppercase;color:#000000;'>RESQ</span>
                  </td>
                  <td align='right'>
                    <span style='font-size:10px;letter-spacing:1px;color:#999999;'>© 2026</span>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- BOTTOM ACCENT -->
          <tr>
            <td style='background:#FF5722;padding:0;height:4px;font-size:0;line-height:0;'>&nbsp;</td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

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
