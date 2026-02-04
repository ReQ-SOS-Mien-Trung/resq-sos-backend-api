using FluentValidation;

namespace RESQ.Application.UseCases.Personnel.Commands.CreateAssemblyPoint;

public class CreateAssemblyPointCommandValidator : AbstractValidator<CreateAssemblyPointCommand>
{
    public CreateAssemblyPointCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên điểm tập kết không được để trống.")
            .MaximumLength(200).WithMessage("Tên điểm tập kết không được vượt quá 200 ký tự.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .WithMessage("Vĩ độ (Latitude) phải nằm trong khoảng từ -90 đến 90.");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .WithMessage("Kinh độ (Longitude) phải nằm trong khoảng từ -180 đến 180.");

        RuleFor(x => x.CapacityTeams)
            .GreaterThan(0)
            .WithMessage("Sức chứa đội cứu hộ phải lớn hơn 0.");
    }
}
