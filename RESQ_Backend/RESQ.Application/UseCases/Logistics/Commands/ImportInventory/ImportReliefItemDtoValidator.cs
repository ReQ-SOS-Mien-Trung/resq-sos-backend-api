using FluentValidation;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.ImportInventory;

public class ImportReliefItemDtoValidator : AbstractValidator<ImportReliefItemDto>
{
    private static readonly string[] ValidCategoryCodes = Enum.GetNames<ItemCategoryCode>();
    private static readonly string[] ValidItemTypes = Enum.GetNames<ItemType>();
    private static readonly string[] ValidTargetGroups = Enum.GetNames<TargetGroup>();

    public ImportReliefItemDtoValidator()
    {
        RuleFor(x => x.Row)
            .GreaterThan(0).WithMessage("Số dòng phải lớn hơn 0.");

        RuleFor(x => x.ItemName)
            .NotEmpty().WithMessage("Tên vật phẩm không được để trống.")
            .MaximumLength(255).WithMessage("Tên vật phẩm không được vượt quá 255 ký tự.");

        RuleFor(x => x.CategoryCode)
            .NotEmpty().WithMessage("Mã danh mục không được để trống.")
            .Must(code => ValidCategoryCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
            .WithMessage(x => $"Mã danh mục '{x.CategoryCode}' không hợp lệ. Giá trị hợp lệ: {string.Join(", ", ValidCategoryCodes)}.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Số lượng nhập phải lớn hơn 0.");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Đơn vị tính không được để trống.")
            .MaximumLength(50).WithMessage("Đơn vị tính không được vượt quá 50 ký tự.");

        RuleFor(x => x.ItemType)
            .NotEmpty().WithMessage("Loại vật phẩm không được để trống.")
            .Must(type => ValidItemTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
            .WithMessage(x => $"Loại vật phẩm '{x.ItemType}' không hợp lệ. Giá trị hợp lệ: {string.Join(", ", ValidItemTypes)}.");

        RuleFor(x => x.TargetGroup)
            .NotEmpty().WithMessage("Nhóm đối tượng không được để trống.")
            .Must(group => ValidTargetGroups.Contains(group, StringComparer.OrdinalIgnoreCase))
            .WithMessage(x => $"Nhóm đối tượng '{x.TargetGroup}' không hợp lệ. Giá trị hợp lệ: {string.Join(", ", ValidTargetGroups)}.");

        // Rule: ReceivedDate <= Today (can be past, cannot be future)
        RuleFor(x => x.ReceivedDate)
            .Must(date => date!.Value <= DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)))
            .When(x => x.ReceivedDate.HasValue)
            .WithMessage("Ngày nhận không được là ngày trong tương lai.");

        // Rule: ExpiredDate > ReceivedDate
        RuleFor(x => x.ExpiredDate)
            .GreaterThan(x => x.ReceivedDate!.Value)
            .When(x => x.ExpiredDate.HasValue && x.ReceivedDate.HasValue)
            .WithMessage("Ngày hết hạn phải sau ngày nhận.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Ghi chú không được vượt quá 500 ký tự.");
    }
}
