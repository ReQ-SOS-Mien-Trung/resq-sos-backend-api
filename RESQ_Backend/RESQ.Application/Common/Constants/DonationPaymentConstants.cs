namespace RESQ.Application.Common.Constants;

public static class DonationPaymentConstants
{
    public const int PaymentTimeoutMinutes = 15;

    public static DateTime CalculateResponseDeadlineUtc(DateTime createdAtUtc)
    {
        return createdAtUtc.AddMinutes(PaymentTimeoutMinutes);
    }
}
