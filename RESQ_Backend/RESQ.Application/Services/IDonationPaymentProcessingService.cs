namespace RESQ.Application.Services;

public interface IDonationPaymentProcessingService
{
    Task<bool> TryProcessSuccessAsync(
        int donationId,
        string? paymentAuditInfo,
        DateTime? paidAtUtc,
        string? transactionId,
        bool preserveExistingTransactionId,
        CancellationToken cancellationToken = default);

    Task<bool> TryProcessFailureAsync(
        int donationId,
        string? failureAuditInfo,
        CancellationToken cancellationToken = default);
}
