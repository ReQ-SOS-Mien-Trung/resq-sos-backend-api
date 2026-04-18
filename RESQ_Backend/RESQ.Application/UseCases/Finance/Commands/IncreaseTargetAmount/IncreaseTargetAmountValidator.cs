using FluentValidation;

namespace RESQ.Application.UseCases.Finance.Commands.IncreaseTargetAmount;

public class IncreaseTargetAmountValidator : AbstractValidator<IncreaseTargetAmountCommand>
{
    public IncreaseTargetAmountValidator()
    {
        RuleFor(x => x.CampaignId).GreaterThan(0);
        RuleFor(x => x.NewTarget).GreaterThan(0).WithMessage("Số tiền mục tiêu phải lớn hơn 0.");
    }
}
