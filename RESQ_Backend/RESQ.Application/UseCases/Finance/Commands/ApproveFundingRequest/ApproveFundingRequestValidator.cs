using FluentValidation;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.ApproveFundingRequest;

public class ApproveFundingRequestValidator : AbstractValidator<ApproveFundingRequestCommand>
{
    public ApproveFundingRequestValidator()
    {
        RuleFor(x => x.FundingRequestId)
            .GreaterThan(0).WithMessage("Yêu cầu cấp quỹ không hợp lệ.");

        RuleFor(x => x.SourceType)
            .IsInEnum().WithMessage("Loại nguồn quỹ không hợp lệ.");

        RuleFor(x => x.CampaignId)
            .NotNull().WithMessage("CampaignId là bắt buộc khi nguồn quỹ là Campaign.")
            .GreaterThan(0).WithMessage("Campaign không hợp lệ.")
            .When(x => x.SourceType == FundSourceType.Campaign);

        RuleFor(x => x.ReviewedBy)
            .NotEmpty().WithMessage("Người duyệt không hợp lệ.");
    }
}
