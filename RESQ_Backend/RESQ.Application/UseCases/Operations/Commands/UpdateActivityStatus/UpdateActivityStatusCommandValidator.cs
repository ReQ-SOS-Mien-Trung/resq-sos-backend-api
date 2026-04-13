using FluentValidation;
using RESQ.Domain.Enum.Operations;

namespace RESQ.Application.UseCases.Operations.Commands.UpdateActivityStatus;

public class UpdateActivityStatusCommandValidator : AbstractValidator<UpdateActivityStatusCommand>
{
    public UpdateActivityStatusCommandValidator()
    {
        RuleFor(x => x.MissionId)
            .GreaterThan(0).WithMessage("MissionId pháº£i lá»›n hÆ¡n 0");

        RuleFor(x => x.ActivityId)
            .GreaterThan(0).WithMessage("ActivityId pháº£i lá»›n hÆ¡n 0");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Status pháº£i lÃ  má»™t trong: Planned, OnGoing, Succeed, Failed, Cancelled");

        RuleFor(x => x.DecisionBy)
            .NotEmpty().WithMessage("DecisionBy khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng");

        RuleFor(x => x.ImageUrl)
            .Must(url => string.IsNullOrWhiteSpace(url) || Uri.TryCreate(url.Trim(), UriKind.Absolute, out _))
            .WithMessage("ImageUrl pháº£i lÃ  má»™t URL tuyá»‡t Ä‘á»‘i há»£p lá»‡.");
    }
}
