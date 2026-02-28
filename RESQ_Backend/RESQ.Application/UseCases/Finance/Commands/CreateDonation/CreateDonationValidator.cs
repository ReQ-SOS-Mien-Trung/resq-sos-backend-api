using FluentValidation;

namespace RESQ.Application.UseCases.Finance.Commands.CreateDonation;

public class CreateDonationValidator : AbstractValidator<CreateDonationCommand>
{
    public CreateDonationValidator()
    {
        RuleFor(v => v.FundCampaignId)
            .GreaterThan(0).WithMessage("Chiến dịch không hợp lệ.");

        RuleFor(v => v.DonorName)
            .NotEmpty().WithMessage("Tên người ủng hộ không được để trống.")
            .MaximumLength(255).WithMessage("Tên người ủng hộ không được vượt quá 255 ký tự.");

        RuleFor(v => v.DonorEmail)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không hợp lệ.")
            .MaximumLength(255).WithMessage("Email không được vượt quá 255 ký tự.");

        RuleFor(v => v.Amount)
            .GreaterThanOrEqualTo(10000).WithMessage("Số tiền ủng hộ phải ít nhất 10.000 VNĐ.");
    }
}
