using FluentValidation;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportInventory;

public class ImportReliefItemsCommandValidator : AbstractValidator<ImportReliefItemsCommand>
{
    public ImportReliefItemsCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .GreaterThan(0).WithMessage("Id tổ chức không hợp lệ.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Danh sách vật phẩm nhập không được để trống.");

        RuleForEach(x => x.Items).SetValidator(new ImportReliefItemDtoValidator());
    }
}