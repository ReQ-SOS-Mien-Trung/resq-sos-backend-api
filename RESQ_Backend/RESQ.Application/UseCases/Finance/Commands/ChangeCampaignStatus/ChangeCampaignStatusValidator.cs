using FluentValidation;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.UseCases.Finance.Commands.ChangeCampaignStatus;

public class ChangeCampaignStatusValidator : AbstractValidator<ChangeCampaignStatusCommand>
{
    public ChangeCampaignStatusValidator()
    {
        RuleFor(x => x.CampaignId).GreaterThan(0);
        RuleFor(x => x.NewStatus).IsInEnum().WithMessage("Trạng thái không hợp lệ.");

        // Lý do bắt buộc khi tạm dừng chiến dịch
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Vui lòng cung cấp lý do tạm dừng chiến dịch.")
            .When(x => x.NewStatus == FundCampaignStatus.Suspended);
    }
}
