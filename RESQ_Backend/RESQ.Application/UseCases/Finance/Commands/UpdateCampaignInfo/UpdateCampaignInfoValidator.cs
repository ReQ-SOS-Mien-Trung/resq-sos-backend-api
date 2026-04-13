using FluentValidation;

namespace RESQ.Application.UseCases.Finance.Commands.UpdateCampaignInfo;

public class UpdateCampaignInfoValidator : AbstractValidator<UpdateCampaignInfoCommand>
{
    public UpdateCampaignInfoValidator()
    {
        RuleFor(x => x.CampaignId).GreaterThan(0).WithMessage("ID chiến dịch không hợp lệ.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255).WithMessage("Tên chiến dịch không hợp lệ.");
        RuleFor(x => x.Region).NotEmpty().MaximumLength(255).WithMessage("Khu vực không hợp lệ.");
        RuleFor(x => x.ModifiedBy).NotEmpty().WithMessage("Người thực hiện không hợp lệ.");
    }
}
