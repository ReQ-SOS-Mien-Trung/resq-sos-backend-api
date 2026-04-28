using System.Globalization;
using RESQ.Domain.Entities.Finance;

namespace RESQ.Application.Common.Formatting;

public static class DonationDisplayFormatter
{
    private static readonly CultureInfo VietnamCulture = CultureInfo.GetCultureInfo("vi-VN");

    public static string PublicDonorName(DonationModel donation)
        => string.IsNullOrWhiteSpace(donation.Donor?.Name)
            ? $"Nhà hảo tâm #{donation.Id}"
            : donation.Donor.Name;

    public static string PrivacyAwareDonorName(DonationModel donation)
        => donation.IsPrivate
            ? $"Nhà hảo tâm ẩn danh #{donation.Id}"
            : PublicDonorName(donation);

    public static string PublicDonationText(DonationModel donation)
    {
        var amount = donation.Amount?.Amount ?? 0;
        var donorName = donation.IsPrivate
            ? "Nhà hảo tâm ẩn danh"
            : string.IsNullOrWhiteSpace(donation.Donor?.Name)
                ? "Nhà hảo tâm"
                : donation.Donor.Name;

        return $"{donorName} #{donation.Id} đã quyên góp {amount.ToString("N0", VietnamCulture)} VND";
    }
}
