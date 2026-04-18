using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.CreateItemCategory;

public class CreateItemCategoryCommandValidator : AbstractValidator<CreateItemCategoryCommand>
{
    public CreateItemCategoryCommandValidator()
    {
        RuleFor(x => x.Code)
            .IsInEnum().WithMessage("Mã danh mục không hợp lệ.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên danh mục không được để trống.")
            .MaximumLength(255).WithMessage("Tên danh mục tối đa 255 ký tự.");
    }
}
