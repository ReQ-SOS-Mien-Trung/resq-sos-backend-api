using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportInventory;

public class ImportReliefItemDtoValidator : AbstractValidator<ImportReliefItemDto>
{
    public ImportReliefItemDtoValidator()
    {
        RuleFor(x => x.Row)
            .GreaterThan(0).WithMessage("Số dòng phải lớn hơn 0.");

        RuleFor(x => x.ItemName)
            .NotEmpty().WithMessage("Tên vật phẩm không được để trống.")
            .MaximumLength(255).WithMessage("Tên vật phẩm không được vượt quá 255 ký tự.");

        RuleFor(x => x.CategoryCode)
            .NotEmpty().WithMessage("Mã danh mục không được để trống.");
    }
}