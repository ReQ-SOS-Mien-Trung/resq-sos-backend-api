using System;
using System.Threading;
using System.Threading.Tasks;

namespace RESQ.Application.Services
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string email, string verificationToken, CancellationToken cancellationToken = default);
        Task SendPasswordResetEmailAsync(string email, string resetToken, CancellationToken cancellationToken = default);
        
        // Requirement 4: Send confirmation email
        Task SendDonationSuccessEmailAsync(
            string donorEmail,
            string donorName,
            decimal amount,
            string campaignName,
            string campaignCode,
            int donationId,
            CancellationToken cancellationToken = default
        );
    }
}
