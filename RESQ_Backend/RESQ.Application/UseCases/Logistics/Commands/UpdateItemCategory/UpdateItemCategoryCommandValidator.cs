using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.UpdateItemCategory;

public class UpdateItemCategoryCommandValidator : AbstractValidator<UpdateItemCategoryCommand>
{
    public UpdateItemCategoryCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id không hợp lệ.");

        //RuleFor(x => x.Code)
        //    .IsInEnum().WithMessage("Mã danh mục không hợp lệ.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên danh mục không được để trống.")
            .MaximumLength(255).WithMessage("Tên danh mục tối đa 255 ký tự.");
    }
}
