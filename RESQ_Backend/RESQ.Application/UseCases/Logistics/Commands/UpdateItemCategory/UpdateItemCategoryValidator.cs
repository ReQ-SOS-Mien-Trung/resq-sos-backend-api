using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.UpdateItemCategory;

public class UpdateItemCategoryValidator : AbstractValidator<UpdateItemCategoryCommand>
{
    public UpdateItemCategoryValidator()
    {
        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("Tên danh mục không được để trống.")
            .MaximumLength(100).WithMessage("Tên danh mục không được vượt quá 100 ký tự.");

        RuleFor(v => v.Description)
            .MaximumLength(500).WithMessage("Mô tả không được vượt quá 500 ký tự.");
    }
}
