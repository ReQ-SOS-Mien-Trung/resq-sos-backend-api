using FluentValidation;

namespace RESQ.Application.UseCases.Finance.Commands.AllocateFundToDepot;

public class AllocateFundToDepotValidator : AbstractValidator<AllocateFundToDepotCommand>
{
    public AllocateFundToDepotValidator()
    {
        RuleFor(x => x.FundCampaignId)
            .GreaterThan(0).WithMessage("Campaign không hợp lệ.");

        RuleFor(x => x.DepotId)
            .GreaterThan(0).WithMessage("Depot không hợp lệ.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền phải lớn hơn 0.");

        RuleFor(x => x.AllocatedBy)
            .NotEmpty().WithMessage("Người cấp quỹ không hợp lệ.");
    }
}
