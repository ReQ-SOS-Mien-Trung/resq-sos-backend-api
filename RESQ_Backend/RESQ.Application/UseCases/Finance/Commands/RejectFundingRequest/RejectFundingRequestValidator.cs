using FluentValidation;

namespace RESQ.Application.UseCases.Finance.Commands.RejectFundingRequest;

public class RejectFundingRequestValidator : AbstractValidator<RejectFundingRequestCommand>
{
    public RejectFundingRequestValidator()
    {
        RuleFor(x => x.FundingRequestId)
            .GreaterThan(0).WithMessage("Yêu cầu cấp quỹ không hợp lệ.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do từ chối không được để trống.")
            .MaximumLength(1000).WithMessage("Lý do từ chối không được vượt quá 1000 ký tự.");

        RuleFor(x => x.ReviewedBy)
            .NotEmpty().WithMessage("Người duyệt không hợp lệ.");
    }
}
