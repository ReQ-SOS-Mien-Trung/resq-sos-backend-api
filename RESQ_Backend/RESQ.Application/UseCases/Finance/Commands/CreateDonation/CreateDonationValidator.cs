using FluentValidation;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.CreateDonation;

public class CreateDonationValidator : AbstractValidator<CreateDonationCommand>
{
    public CreateDonationValidator()
    {
        RuleFor(v => v.FundCampaignId)
            .GreaterThan(0).WithMessage("ID chiến dịch không hợp lệ.");

        RuleFor(v => v.DonorName)
            .NotEmpty().WithMessage("Tên người ủng hộ không được để trống.")
            .Length(2, 100).WithMessage("Tên người ủng hộ phải từ 2 đến 100 ký tự.")
            .Matches(@"^[\p{L}\s0-9]*$").WithMessage("Tên người ủng hộ không được chứa ký tự đặc biệt.");

        RuleFor(v => v.DonorEmail)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.")
            .MaximumLength(255).WithMessage("Email không được vượt quá 255 ký tự.");

        RuleFor(v => v.Amount)
            .GreaterThan(0).WithMessage("Số tiền ủng hộ phải lớn hơn 0.")
            .GreaterThanOrEqualTo(10000).WithMessage("Số tiền ủng hộ tối thiểu là 10.000 VNĐ.")
            .LessThanOrEqualTo(10000000000).WithMessage("Số tiền ủng hộ vượt quá giới hạn cho phép của hệ thống (10 tỷ VNĐ).");

        RuleFor(v => v.Note)
            .MaximumLength(500).WithMessage("Lời nhắn không được vượt quá 500 ký tự.");

        RuleFor(v => v.PaymentMethodCode)
            .NotEmpty().WithMessage("Phương thức thanh toán không được để trống.")
            .Must(code => Enum.TryParse<PaymentMethodCode>(code, ignoreCase: true, out _))
            .WithMessage("Phương thức thanh toán không hợp lệ.");
    }
}
