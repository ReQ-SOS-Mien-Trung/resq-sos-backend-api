using FluentValidation;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.AllocateFundToDepot;

public class AllocateFundToDepotValidator : AbstractValidator<AllocateFundToDepotCommand>
{
    public AllocateFundToDepotValidator()
    {
        RuleFor(x => x.SourceType)
            .IsInEnum().WithMessage("Loại nguồn quỹ không hợp lệ.");

        RuleFor(x => x.FundCampaignId)
            .NotNull().WithMessage("FundCampaignId là bắt buộc khi nguồn quỹ là Campaign.")
            .GreaterThan(0).WithMessage("Campaign không hợp lệ.")
            .When(x => x.SourceType == FundSourceType.Campaign);

        RuleFor(x => x.DepotId)
            .GreaterThan(0).WithMessage("Depot không hợp lệ.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền phải lớn hơn 0.");

        RuleFor(x => x.AllocatedBy)
            .NotEmpty().WithMessage("Người cấp quỹ không hợp lệ.");
    }
}
