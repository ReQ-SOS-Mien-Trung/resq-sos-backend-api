using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportInventory;

public class ImportReliefItemsCommandValidator : AbstractValidator<ImportReliefItemsCommand>
{
    public ImportReliefItemsCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.OrganizationId.HasValue || !string.IsNullOrEmpty(x.OrganizationName))
            .WithMessage("Phải cung cấp ID tổ chức hoặc tên tổ chức.");

        RuleFor(x => x.OrganizationId)
            .GreaterThan(0)
            .When(x => x.OrganizationId.HasValue)
            .WithMessage("Id tổ chức không hợp lệ.");

        RuleFor(x => x.OrganizationName)
            .NotEmpty()
            .When(x => !x.OrganizationId.HasValue)
            .WithMessage("Tên tổ chức không được để trống.")
            .MaximumLength(255)
            .When(x => !string.IsNullOrEmpty(x.OrganizationName))
            .WithMessage("Tên tổ chức không được vượt quá 255 ký tự.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Danh sách vật phẩm nhập không được để trống.");

        RuleForEach(x => x.Items).SetValidator(new ImportReliefItemDtoValidator());
    }
}
