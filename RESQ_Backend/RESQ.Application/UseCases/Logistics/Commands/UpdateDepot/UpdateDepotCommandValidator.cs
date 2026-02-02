using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.UpdateDepot;

public class UpdateDepotCommandValidator : AbstractValidator<UpdateDepotCommand>
{
    public UpdateDepotCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id kho không hợp lệ.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên kho không được để trống.")
            .MaximumLength(200).WithMessage("Tên kho không được vượt quá 200 ký tự.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Địa chỉ không được để trống.")
            .MaximumLength(300).WithMessage("Địa chỉ không được vượt quá 300 ký tự.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .WithMessage("Vĩ độ (Latitude) phải nằm trong khoảng từ -90 đến 90.");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .WithMessage("Kinh độ (Longitude) phải nằm trong khoảng từ -180 đến 180.");

        RuleFor(x => x.Capacity)
            .GreaterThan(0)
            .WithMessage("Sức chứa kho phải lớn hơn 0.");
    }
}
