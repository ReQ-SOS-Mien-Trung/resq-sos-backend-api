using FluentValidation;

namespace RESQ.Application.UseCases.Finance.Commands.ChangeCampaignStatus;

public class ChangeCampaignStatusValidator : AbstractValidator<ChangeCampaignStatusCommand>
{
    public ChangeCampaignStatusValidator()
    {
        RuleFor(x => x.CampaignId).GreaterThan(0);
        RuleFor(x => x.NewStatus).IsInEnum().WithMessage("Trạng thái không hợp lệ.");
    }
}
