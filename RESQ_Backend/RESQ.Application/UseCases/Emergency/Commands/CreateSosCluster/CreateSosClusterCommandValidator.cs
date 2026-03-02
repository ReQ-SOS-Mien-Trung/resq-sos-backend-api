using FluentValidation;

namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosCluster;

public class CreateSosClusterCommandValidator : AbstractValidator<CreateSosClusterCommand>
{
    public CreateSosClusterCommandValidator()
    {
        RuleFor(x => x.SosRequestIds)
            .NotEmpty().WithMessage("Phải chọn ít nhất một SOS request để tạo cluster")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Danh sách SOS request không được có ID trùng lặp");
    }
}
