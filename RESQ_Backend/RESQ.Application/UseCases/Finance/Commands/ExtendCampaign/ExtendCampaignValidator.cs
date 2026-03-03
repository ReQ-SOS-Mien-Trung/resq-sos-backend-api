using FluentValidation;

namespace RESQ.Application.UseCases.Finance.Commands.ExtendCampaign;

public class ExtendCampaignValidator : AbstractValidator<ExtendCampaignCommand>
{
    public ExtendCampaignValidator()
    {
        RuleFor(x => x.CampaignId).GreaterThan(0);
        RuleFor(x => x.NewEndDate).GreaterThan(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Ngày kết thúc mới phải lớn hơn ngày hiện tại.");
    }
}