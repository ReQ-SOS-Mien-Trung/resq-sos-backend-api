using FluentValidation;

namespace RESQ.Application.UseCases.Finance.Commands.CreateCampaign;

public class CreateCampaignValidator : AbstractValidator<CreateCampaignCommand>
{
    public CreateCampaignValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên chiến dịch không được để trống.")
            .MaximumLength(255).WithMessage("Tên chiến dịch không được vượt quá 255 ký tự.");

        RuleFor(x => x.Region)
            .NotEmpty().WithMessage("Khu vực không được để trống.")
            .MaximumLength(255).WithMessage("Khu vực không được vượt quá 255 ký tự.");

        RuleFor(x => x.TargetAmount)
            .GreaterThan(0).WithMessage("Số tiền mục tiêu phải lớn hơn 0.");

        RuleFor(x => x.CampaignEndDate)
            .GreaterThan(x => x.CampaignStartDate).WithMessage("Ngày kết thúc phải sau ngày bắt đầu.");
            
        RuleFor(x => x.CreatedBy)
            .NotEmpty().WithMessage("Người tạo không hợp lệ.");
    }
}
