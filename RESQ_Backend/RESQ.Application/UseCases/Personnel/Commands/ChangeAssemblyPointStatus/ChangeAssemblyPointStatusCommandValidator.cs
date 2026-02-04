using FluentValidation;

namespace RESQ.Application.UseCases.Personnel.Commands.ChangeAssemblyPointStatus;

public class ChangeAssemblyPointStatusCommandValidator : AbstractValidator<ChangeAssemblyPointStatusCommand>
{
    public ChangeAssemblyPointStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id điểm tập kết phải lớn hơn 0.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Trạng thái không hợp lệ.");
    }
}
