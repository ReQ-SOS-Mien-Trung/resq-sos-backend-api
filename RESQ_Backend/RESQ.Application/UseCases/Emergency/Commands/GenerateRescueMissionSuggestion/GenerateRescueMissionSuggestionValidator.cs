using FluentValidation;

namespace RESQ.Application.UseCases.Emergency.Commands.GenerateRescueMissionSuggestion;

public class GenerateRescueMissionSuggestionValidator : AbstractValidator<GenerateRescueMissionSuggestionCommand>
{
    public GenerateRescueMissionSuggestionValidator()
    {
        RuleFor(x => x.SosRequestIds)
            .NotEmpty().WithMessage("Danh sách SOS request không được trống")
            .Must(ids => ids.Count <= 20).WithMessage("Tối đa 20 SOS request mỗi lần đề xuất");

        RuleFor(x => x.RequestedByUserId)
            .NotEmpty().WithMessage("UserId không hợp lệ");
    }
}
