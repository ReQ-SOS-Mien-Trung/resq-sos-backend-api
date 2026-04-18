namespace RESQ.Application.Services
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string email, string verificationToken, CancellationToken cancellationToken = default);
        Task SendPasswordResetEmailAsync(string email, string resetToken, CancellationToken cancellationToken = default);
        Task SendDonationSuccessEmailAsync(string donorEmail, string donorName, decimal amount, string campaignName, string campaignCode, int donationId, CancellationToken cancellationToken = default);
        Task SendTeamInvitationEmailAsync(string email, string name, string teamName, int teamId, Guid userId, CancellationToken cancellationToken = default);
    }
}
