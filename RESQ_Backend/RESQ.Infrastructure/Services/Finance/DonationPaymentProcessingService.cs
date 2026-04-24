using RESQ.Application.Common.Constants;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Finance;
using RESQ.Application.Services;
using RESQ.Domain.Entities.Finance;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Infrastructure.Services.Finance;

public class DonationPaymentProcessingService(
    IUnitOfWork unitOfWork,
    IDonationRepository donationRepository,
    IFundCampaignRepository campaignRepository,
    IFundTransactionRepository fundTransactionRepository)
    : IDonationPaymentProcessingService
{
    public async Task<bool> TryProcessSuccessAsync(
        int donationId,
        string? paymentAuditInfo,
        DateTime? paidAtUtc,
        string? transactionId,
        bool preserveExistingTransactionId,
        CancellationToken cancellationToken = default)
    {
        var processed = false;

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var donation = await donationRepository.GetTrackedByIdAsync(donationId, cancellationToken);
            if (donation == null)
            {
                processed = false;
                return;
            }

            if (donation.Status != Status.Succeed)
            {
                donation.UpdatePaymentStatus(Status.Succeed);
            }

            if (!preserveExistingTransactionId && !string.IsNullOrWhiteSpace(transactionId))
            {
                donation.TransactionId = transactionId;
            }
            else if (string.IsNullOrWhiteSpace(donation.TransactionId) && !string.IsNullOrWhiteSpace(transactionId))
            {
                donation.TransactionId = transactionId;
            }

            donation.PaymentAuditInfo = MergeAuditInfo(donation.PaymentAuditInfo, paymentAuditInfo);
            donation.PaidAt = paidAtUtc ?? donation.PaidAt ?? DateTime.UtcNow;
            donation.ResponseDeadline ??= DonationPaymentConstants.CalculateResponseDeadlineUtc(
                donation.CreatedAt ?? DateTime.UtcNow);

            await donationRepository.UpdateAsync(donation, cancellationToken);

            var hasExistingTransaction = await fundTransactionRepository.ExistsByReferenceAsync(
                TransactionReferenceType.Donation,
                donation.Id,
                cancellationToken);

            if (!hasExistingTransaction && donation.FundCampaignId.HasValue)
            {
                var campaign = await campaignRepository.GetByIdAsync(donation.FundCampaignId.Value, cancellationToken);
                if (campaign != null && !campaign.IsDeleted)
                {
                    campaign.ReceiveDonation(donation.Amount?.Amount ?? 0);
                    await campaignRepository.UpdateAsync(campaign, cancellationToken);

                    var transaction = new FundTransactionModel
                    {
                        FundCampaignId = donation.FundCampaignId,
                        Type = TransactionType.Donation,
                        Direction = TransactionDirection.In,
                        Amount = donation.Amount?.Amount,
                        ReferenceType = TransactionReferenceType.Donation,
                        ReferenceId = donation.Id,
                        CreatedAt = DateTime.UtcNow
                    };
                    await fundTransactionRepository.CreateAsync(transaction, cancellationToken);
                }
            }

            await unitOfWork.SaveAsync();
            processed = true;
        });

        return processed;
    }

    public async Task<bool> TryProcessFailureAsync(
        int donationId,
        string? failureAuditInfo,
        CancellationToken cancellationToken = default)
    {
        var processed = false;

        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var donation = await donationRepository.GetTrackedByIdAsync(donationId, cancellationToken);
            if (donation == null)
            {
                processed = false;
                return;
            }

            if (donation.Status == Status.Succeed)
            {
                processed = true;
                return;
            }

            donation.UpdatePaymentStatus(Status.Failed);
            donation.PaymentAuditInfo = MergeAuditInfo(donation.PaymentAuditInfo, failureAuditInfo);
            donation.ResponseDeadline ??= DonationPaymentConstants.CalculateResponseDeadlineUtc(
                donation.CreatedAt ?? DateTime.UtcNow);

            await donationRepository.UpdateAsync(donation, cancellationToken);
            await unitOfWork.SaveAsync();
            processed = true;
        });

        return processed;
    }

    private static string? MergeAuditInfo(string? existing, string? next)
    {
        if (string.IsNullOrWhiteSpace(next))
        {
            return existing;
        }

        if (string.IsNullOrWhiteSpace(existing))
        {
            return next;
        }

        return $"{existing} {next}";
    }
}
