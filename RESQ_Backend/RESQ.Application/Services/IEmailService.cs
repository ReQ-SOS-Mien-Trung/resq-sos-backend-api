namespace RESQ.Application.Services
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string email, string verificationToken, CancellationToken cancellationToken = default);
        Task SendPasswordResetEmailAsync(string email, string resetToken, CancellationToken cancellationToken = default);
    }
}
