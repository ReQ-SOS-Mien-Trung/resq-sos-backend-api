using System.Text.RegularExpressions;
using RESQ.Application.Exceptions;

namespace RESQ.Application.UseCases.Finance.Common;

public static class ContributorIdentityNormalizer
{
    private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);

    public static string NormalizeName(string? contributorName)
    {
        if (string.IsNullOrWhiteSpace(contributorName))
        {
            throw new BadRequestException("Bắt buộc nhập tên người ứng.");
        }

        return MultiSpaceRegex.Replace(contributorName.Trim(), " ");
    }

    public static string NormalizePhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new BadRequestException("Bắt buộc nhập số điện thoại người ứng.");
        }

        var digitsOnly = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length is < 9 or > 15)
        {
            throw new BadRequestException("Số điện thoại phải có từ 9 đến 15 chữ số.");
        }

        return digitsOnly;
    }
}
