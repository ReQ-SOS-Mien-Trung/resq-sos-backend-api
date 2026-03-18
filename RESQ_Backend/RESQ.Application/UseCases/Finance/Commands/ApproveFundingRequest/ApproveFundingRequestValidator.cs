using FluentValidation;

namespace RESQ.Application.UseCases.Finance.Commands.ApproveFundingRequest;

public class ApproveFundingRequestValidator : AbstractValidator<ApproveFundingRequestCommand>
{
    public ApproveFundingRequestValidator()
    {
        RuleFor(x => x.FundingRequestId)
            .GreaterThan(0).WithMessage("Yêu cầu cấp quỹ không hợp lệ.");

        RuleFor(x => x.CampaignId)
            .GreaterThan(0).WithMessage("Campaign không hợp lệ.");

        RuleFor(x => x.ReviewedBy)
            .NotEmpty().WithMessage("Người duyệt không hợp lệ.");
    }
}
