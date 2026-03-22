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
          <tr>
            <td style='background:#000000;padding:0;height:6px;font-size:0;line-height:0;'>&nbsp;</td>
          </tr>
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
          <tr>
            <td style='padding:48px 40px 32px;border-bottom:2px solid #000000;'>
              <p style='margin:0 0 8px;font-size:11px;font-weight:700;letter-spacing:4px;text-transform:uppercase;color:#FF5722;'>— Bước 01 / Xác minh</p>
              <h1 style='margin:0 0 24px;font-size:42px;font-weight:900;line-height:1.05;letter-spacing:-1.5px;color:#000000;text-transform:uppercase;'>XÁC MINH<br/>TÀI KHOẢN</h1>
              <p style='margin:0;font-size:15px;line-height:1.7;color:#333333;max-width:420px;'>
                Cảm ơn bạn đã đăng ký trên RESQ. Xác nhận tài khoản của bạn để bắt đầu tham gia hệ thống ứng phó khẩn cấp.
              </p>
            </td>
          </tr>
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
            </td>
          </tr>
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
            var FEbaseUrl = _configuration["AppSettings:FEBaseUrl"] ?? "https://resq-sos-mientrung.vercel.app";
            var resetUrl = $"{FEbaseUrl}/auth/reset-pass?token={resetToken}";

            var subject = "Đặt Lại Mật Khẩu RESQ Của Bạn";
            var body = $@"<!DOCTYPE html>
<html lang='vi'>
<head>
  <meta charset='UTF-8' />
  <meta name='viewport' content='width=device-width, initial-scale=1.0' />
  <title>Đặt Lại Mật Khẩu — RESQ</title>
</head>
<body style='margin:0;padding:0;background:#ffffff;font-family:""Helvetica Neue"",Helvetica,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' border='0' style='background:#ffffff;'>
    <tr>
      <td align='center' style='padding:48px 16px;'>
        <table width='600' cellpadding='0' cellspacing='0' border='0' style='max-width:600px;width:100%;border:3px solid #000000;'>
          <tr>
            <td style='background:#000000;padding:0;height:6px;font-size:0;line-height:0;'>&nbsp;</td>
          </tr>
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
          <tr>
            <td style='padding:48px 40px 32px;border-bottom:2px solid #000000;'>
              <p style='margin:0 0 8px;font-size:11px;font-weight:700;letter-spacing:4px;text-transform:uppercase;color:#FF5722;'>— Yêu cầu bảo mật</p>
              <h1 style='margin:0 0 24px;font-size:42px;font-weight:900;line-height:1.05;letter-spacing:-1.5px;color:#000000;text-transform:uppercase;'>ĐẶT LẠI<br/>MẬT KHẨU</h1>
              <p style='margin:0;font-size:15px;line-height:1.7;color:#333333;max-width:420px;'>
                Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Nhấp vào nút bên dưới để tiến hành.
              </p>
            </td>
          </tr>
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
            </td>
          </tr>
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
            
            var FEbaseUrl = _configuration["AppSettings:FEBaseUrl"] ?? "https://resq-sos-mientrung.vercel.app";
            var donationUrl = $"{FEbaseUrl}/donations/success?orderCode={donationId}";

            var body = $@"<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Xác nhận ủng hộ RESQ</title>
</head>
<body style='margin: 0; padding: 0; background-color: #ffffff; font-family: Helvetica, Arial, sans-serif; -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; color: #000000;'>
    <table border='0' cellpadding='0' cellspacing='0' width='100%' style='border-collapse: collapse; background-color: #ffffff;'>
        <tr>
            <td align='center' style='padding: 48px 16px;'>
                <table border='0' cellpadding='0' cellspacing='0' width='600' style='max-width: 600px; width: 100%; border: 3px solid #000000;'>
                    <tr>
                        <td style='background:#000000;padding:0;height:6px;font-size:0;line-height:0;'>&nbsp;</td>
                    </tr>
                    <tr>
                        <td style='padding: 32px 40px 24px; border-bottom: 3px solid #000000;'>
                            <table border='0' cellpadding='0' cellspacing='0' width='100%'>
                                <tr>
                                    <td>
                                        <span style='font-size:11px;font-weight:700;letter-spacing:4px;text-transform:uppercase;color:#000000;'>RESQ DONATION</span>
                                    </td>
                                    <td align='right' style='font-size: 11px; letter-spacing: 2px; text-transform: uppercase; color: #999999;'>
                                        RECEIPT #{donationId}
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding: 60px 40px 40px; color: #000000;'>
                            <h1 style='margin: 0; font-size: 36px; line-height: 1.1; font-weight: 900; letter-spacing: -1px; text-transform: uppercase;'>
                                CẢM ƠN<br>BẠN ĐÃ ỦNG HỘ.
                            </h1>
                            <p style='margin: 24px 0 0 0; font-size: 16px; line-height: 1.6; color: #333333; max-width: 480px;'>
                                Chào {donorName},<br><br>
                                Đóng góp của bạn đã được tiếp nhận an toàn và sẽ được chuyển trực tiếp đến các hoạt động cứu trợ khẩn cấp.
                            </p>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding: 0 40px 40px;'>
                            <table border='0' cellpadding='0' cellspacing='0' width='100%' style='border: 1px solid #000000; border-left: 4px solid #FF5722;'>
                                <tr>
                                    <td style='padding: 30px;'>
                                        <table border='0' cellpadding='0' cellspacing='0' width='100%'>
                                            <tr>
                                                <td style='padding-bottom: 8px; font-size: 10px; text-transform: uppercase; letter-spacing: 2px; color: #999999; font-weight: 700;'>
                                                    CHIẾN DỊCH
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style='padding-bottom: 30px; font-size: 18px; font-weight: 700; color: #000000; line-height: 1.3;'>
                                                    {campaignName}
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style='border-top: 1px dotted #000000; padding-top: 20px;'>
                                                    <span style='font-size: 10px; text-transform: uppercase; letter-spacing: 2px; color: #999999; font-weight: 700; display: block; margin-bottom: 8px;'>
                                                        SỐ TIỀN
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
                    <tr>
                        <td align='center' style='padding: 40px; border-top: 1px solid #000000;'>
                            <a href='{donationUrl}' target='_blank' style='display:inline-block;padding:16px 40px;font-size:12px;font-weight:900;letter-spacing:3px;text-transform:uppercase;color:#ffffff;text-decoration:none;background-color: #FF5722;'>
                                XEM CHI TIẾT →
                            </a>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding: 20px 40px; background-color: #000000; color: #ffffff;'>
                            <table width='100%' cellpadding='0' cellspacing='0' border='0'>
                                <tr>
                                    <td>
                                        <span style='font-size:10px;font-weight:700;letter-spacing:3px;text-transform:uppercase;'>RESQ FOUNDATION</span>
                                    </td>
                                    <td align='right'>
                                        <span style='font-size:10px;letter-spacing:1px;color:#999999;'>{date}</span>
                                    </td>
                                </tr>
                            </table>
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

        public async Task SendTeamInvitationEmailAsync(string email, string name, string teamName, int teamId, Guid userId, CancellationToken cancellationToken = default)
        {
            var FEbaseUrl = _configuration["AppSettings:FEBaseUrl"] ?? "https://resq-sos-mientrung.vercel.app";
            var acceptUrl = $"{FEbaseUrl}/rescue-teams/invitations/accept?teamId={teamId}&userId={userId}";
            var declineUrl = $"{FEbaseUrl}/rescue-teams/invitations/decline?teamId={teamId}&userId={userId}";

            var subject = $"Lời mời tham gia Đội Cứu Hộ - {teamName}";
            var body = $@"<!DOCTYPE html>
<html lang='vi'>
<head>
  <meta charset='UTF-8' />
  <meta name='viewport' content='width=device-width, initial-scale=1.0' />
  <title>Lời mời tham gia đội — RESQ</title>
</head>
<body style='margin:0;padding:0;background:#ffffff;font-family:""Helvetica Neue"",Helvetica,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' border='0' style='background:#ffffff;'>
    <tr>
      <td align='center' style='padding:48px 16px;'>
        <table width='600' cellpadding='0' cellspacing='0' border='0' style='max-width:600px;width:100%;border:3px solid #000000;'>
          <tr>
            <td style='background:#000000;padding:0;height:6px;font-size:0;line-height:0;'>&nbsp;</td>
          </tr>
          <tr>
            <td style='padding:32px 40px 24px;border-bottom:3px solid #000000;'>
              <table width='100%' cellpadding='0' cellspacing='0' border='0'>
                <tr>
                  <td>
                    <span style='font-size:11px;font-weight:700;letter-spacing:4px;text-transform:uppercase;color:#000000;'>RESQ SYSTEM</span>
                  </td>
                  <td align='right'>
                    <span style='font-size:11px;letter-spacing:2px;text-transform:uppercase;color:#999999;'>TEAM INVITATION</span>
                  </td>
                </tr>
              </table>
            </td>
          </tr>
          <tr>
            <td style='padding:48px 40px 32px;border-bottom:2px solid #000000;'>
              <h1 style='margin:0 0 24px;font-size:32px;font-weight:900;line-height:1.2;letter-spacing:-1px;color:#000000;text-transform:uppercase;'>LỜI MỜI THAM GIA ĐỘI CỨU HỘ</h1>
              <p style='margin:0 0 16px;font-size:16px;line-height:1.6;color:#333333;'>
                Chào <strong>{name}</strong>,
              </p>
              <p style='margin:0 0 16px;font-size:15px;line-height:1.7;color:#333333;'>
                Bạn vừa nhận được lời mời tham gia vào đội cứu hộ <strong>{teamName}</strong> trên hệ thống RESQ. Hãy phản hồi lời mời để xác nhận vị trí của bạn trong đội.
              </p>
              <p style='margin:0;font-size:14px;line-height:1.5;color:#FF5722;font-weight:bold;'>
                Lưu ý: Lời mời này sẽ tự động hết hạn và bị từ chối sau 24 giờ.
              </p>
            </td>
          </tr>
          <tr>
            <td style='padding:40px;border-bottom:2px solid #000000;'>
              <table cellpadding='0' cellspacing='0' border='0' width='100%'>
                <tr>
                  <td align='center'>
                    <a href='{acceptUrl}' style='display:inline-block;padding:14px 30px;font-size:12px;font-weight:900;letter-spacing:2px;text-transform:uppercase;color:#ffffff;background:#4CAF50;text-decoration:none;margin-right:10px;'>
                      CHẤP NHẬN
                    </a>
                    <a href='{declineUrl}' style='display:inline-block;padding:14px 30px;font-size:12px;font-weight:900;letter-spacing:2px;text-transform:uppercase;color:#ffffff;background:#F44336;text-decoration:none;'>
                      TỪ CHỐI
                    </a>
                  </td>
                </tr>
              </table>
            </td>
          </tr>
          <tr>
            <td style='padding:20px 40px;'>
              <table width='100%' cellpadding='0' cellspacing='0' border='0'>
                <tr>
                  <td>
                    <span style='font-size:10px;font-weight:700;letter-spacing:3px;text-transform:uppercase;color:#000000;'>RESQ</span>
                  </td>
                  <td align='right'>
                    <span style='font-size:10px;letter-spacing:1px;color:#999999;'>© {DateTime.Now.Year}</span>
                  </td>
                </tr>
              </table>
            </td>
          </tr>
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
                // Do not throw to interrupt flow if email fails
            }
        }
    }
}
